using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.TaskItems;

namespace SameieHub.Tests.Web;

public class TaskListTests : UiTestBase
{
    // The test clock starts at 2026-11-18 10:30 UTC, which is 11:30 on 18 November in Norway.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private async Task<TaskItem> RegisterAsync(string title, TaskItemDetails? details = null)
    {
        return await Tasks.RegisterAsync((details ?? new TaskItemDetails { Title = title }) with { Title = title });
    }

    private async Task<TaskItem> RegisterCompletedAsync(string title)
    {
        var task = await RegisterAsync(title);
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        return task;
    }

    private IRenderedComponent<TaskList> RenderList()
    {
        var cut = Render<TaskList>();
        cut.WaitForElement("[data-testid=quick-tab-open], [data-testid=no-tasks], [role=alert]");

        return cut;
    }

    private static IReadOnlyList<string> RowTexts(IRenderedComponent<TaskList> cut)
    {
        return cut.FindAll("[data-testid=task-row]").Select(row => row.TextContent).ToList();
    }

    private static IReadOnlyList<string> Titles(IRenderedComponent<TaskList> cut)
    {
        return cut.FindAll("[data-testid=task-title]").Select(link => link.TextContent).ToList();
    }

    private static void Select(IRenderedComponent<TaskList> cut, string label, string value)
    {
        cut.FindFieldByLabel(label).Change(value);
    }

    private static void Tab(IRenderedComponent<TaskList> cut, string key)
    {
        cut.FindByTestId($"quick-tab-{key}").Click();
    }

    private static string TabText(IRenderedComponent<TaskList> cut, string key)
    {
        return cut.FindByTestId($"quick-tab-{key}").TextContent.Trim();
    }

    // Empty and loading

    [Fact]
    public void Render_NoTasks_ShowsEmptyStateWithLinkToRegister()
    {
        var cut = RenderList();

        Assert.Contains("Ingen oppgaver registrert ennå", cut.Markup);
        Assert.Empty(cut.FindAll("table"));
        Assert.Empty(cut.FindAll("[data-testid^=quick-tab]"));
        Assert.Contains(cut.FindAll("a"), link => link.GetAttribute("href") == "/oppgaver/ny");
    }

    [Fact]
    public void Render_DatabaseUnavailable_ShowsErrorAndRetryShowsTasks()
    {
        TaskRepository.IsFailing = true;
        var cut = Render<TaskList>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente oppgavene", cut.Find("[role=alert]").TextContent));

        TaskRepository.IsFailing = false;
        cut.Find("[role=alert] button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Ingen oppgaver registrert ennå", cut.Markup));
    }

    // Quick choices and their counts

    [Fact]
    public async Task Render_Tasks_ShowsCountsOnTheQuickChoicesFromTheActualTasks()
    {
        await RegisterAsync("Ikke startet");
        var inProgress = await RegisterAsync("Pågår");
        await Tasks.ChangeStatusAsync(inProgress.Id, TaskItemStatus.InProgress);
        await RegisterCompletedAsync("Ferdig");
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });

        var cut = RenderList();

        Assert.Equal("Åpne 3", TabText(cut, "open"));
        Assert.Equal("Forfalte 1", TabText(cut, "overdue"));
        Assert.Equal("Fullførte 1", TabText(cut, "done"));
        Assert.Equal("true", cut.FindByTestId("quick-tab-open").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task Render_NearMidnight_UsesTheNorwegianDateForTheOverdueCount()
    {
        await RegisterAsync("Frist 18.", new TaskItemDetails { Title = "Frist 18.", DueDate = new DateOnly(2026, 11, 18) });

        // 22:30 UTC is 23:30 in Norway, still 18 November: not overdue.
        Clock.Advance(TimeSpan.FromHours(12));
        Assert.Equal("Forfalte 0", TabText(RenderList(), "overdue"));

        // 23:30 UTC is 00:30 on 19 November in Norway: overdue, although it is still 18 November in UTC.
        Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal("Forfalte 1", TabText(RenderList(), "overdue"));
    }

    [Fact]
    public async Task Render_Default_ShowsTasksThatAreNotCompleted_IncludingInProgress()
    {
        await RegisterAsync("Ikke startet");
        var inProgress = await RegisterAsync("Pågår");
        await Tasks.ChangeStatusAsync(inProgress.Id, TaskItemStatus.InProgress);
        await RegisterCompletedAsync("Ferdig");

        var cut = RenderList();

        Assert.Equal(2, Titles(cut).Count);
        Assert.Contains("Ikke startet", Titles(cut));
        Assert.Contains("Pågår", Titles(cut));
        Assert.DoesNotContain("Ferdig", Titles(cut));
        Assert.Contains("Viser 2 av 3 oppgaver", cut.FindByTestId("list-summary").TextContent);
    }

    [Fact]
    public async Task QuickChoice_DoneShowsOnlyCompletedTasks_AndOverdueShowsOnlyOverdueOpenTasks()
    {
        await RegisterAsync("Åpen");
        await RegisterCompletedAsync("Ferdig");
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });
        var cut = RenderList();

        Tab(cut, "done");
        Assert.Equal(["Ferdig"], Titles(cut));
        Assert.Equal("true", cut.FindByTestId("quick-tab-done").GetAttribute("aria-pressed"));

        Tab(cut, "overdue");
        Assert.Equal(["Forfalt"], Titles(cut));
    }

    [Fact]
    public async Task QuickChoice_Overdue_IncludesOverdueTasksInProgress_AndNeverCompletedOnes()
    {
        var late = await RegisterAsync("Forsinket", new TaskItemDetails { Title = "Forsinket", DueDate = Today.AddDays(-5) });
        await Tasks.ChangeStatusAsync(late.Id, TaskItemStatus.InProgress);
        var done = await RegisterAsync("Gammel og ferdig", new TaskItemDetails { Title = "Gammel og ferdig", DueDate = Today.AddDays(-30) });
        await Tasks.ChangeStatusAsync(done.Id, TaskItemStatus.Completed);
        var cut = RenderList();

        Tab(cut, "overdue");

        Assert.Equal(["Forsinket"], Titles(cut));
        Assert.Equal("Forfalte 1", TabText(cut, "overdue"));
    }

    // Row content

    [Fact]
    public async Task Render_Task_ShowsTitleAssigneeDueDateStatusAndWhatItBelongsTo()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter" });
        var task = await RegisterAsync("Rense takrenner", new TaskItemDetails
        {
            Title = "Rense takrenner",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.High,
            DueDate = new DateOnly(2026, 12, 1),
            AssignedTo = "Test Person",
            HousingUnitId = unit.Id,
            BoardCaseId = boardCase.Id
        });

        var cut = RenderList();

        var row = cut.FindByTestId("task-row");
        Assert.Equal("Rense takrenner", row.QuerySelector("[data-testid=task-title]")!.TextContent);
        Assert.Equal($"/oppgaver/{task.Id}", row.QuerySelector("[data-testid=task-title]")!.GetAttribute("href"));
        Assert.Equal("Test Person", row.QuerySelector("[data-testid=task-assignee]")!.TextContent.Trim());
        Assert.Contains("01.12.2026", row.QuerySelector("[data-testid=task-due]")!.TextContent);
        Assert.Contains("Ikke startet", row.TextContent);
        Assert.Equal("Høy prioritet", row.QuerySelector("[data-testid=high-priority]")!.TextContent);
        var scopeLinks = row.QuerySelectorAll("[data-testid=task-scope] a").ToList();
        Assert.Equal(["Styresak #1 Maling av porter", "Storgata 1A"], scopeLinks.Select(link => link.TextContent).ToList());
        Assert.Equal([$"/styresaker/{boardCase.Id}", $"/boenheter/{unit.Id}"], scopeLinks.Select(link => link.GetAttribute("href")).ToList());
    }

    [Fact]
    public async Task Render_Task_DoesNotShowCategoryNormalPriorityOrTaskNumberInTheRow()
    {
        await RegisterAsync("Vanlig", new TaskItemDetails { Title = "Vanlig", Category = TaskItemCategory.Maintenance });

        var cut = RenderList();

        var row = cut.FindByTestId("task-row").TextContent;
        Assert.DoesNotContain("Vedlikehold", row);
        Assert.DoesNotContain("Normal", row);
        Assert.DoesNotContain("#1", row);
        Assert.False(cut.HasTestId("high-priority"));
    }

    [Fact]
    public async Task Render_TaskWithoutAssigneeDueDateOrLinks_SaysSo()
    {
        await RegisterAsync("Uten alt");

        var cut = RenderList();

        var row = cut.FindByTestId("task-row");
        Assert.Equal("Ikke tildelt", row.QuerySelector("[data-testid=task-assignee]")!.TextContent.Trim());
        Assert.Contains("Ingen frist", row.TextContent);
        Assert.Contains("Hele sameiet", row.QuerySelector("[data-testid=task-scope]")!.TextContent);
    }

    [Fact]
    public async Task Render_Task_HasOnlyTheTitleLinkAndTheCompleteButtonInTheRow()
    {
        var task = await RegisterAsync("Oppgave");

        var cut = RenderList();

        var row = cut.FindByTestId("task-row");
        Assert.Equal([$"/oppgaver/{task.Id}"], row.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList());
        Assert.Equal(["Fullfør"], row.QuerySelectorAll("button").Select(button => button.TextContent.Trim()).ToList());
        Assert.Equal("Fullfør oppgaven «Oppgave»", row.QuerySelector("button")!.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Render_TableCells_HaveLabelsSoTheRowCanBecomeACardOnASmallScreen()
    {
        await RegisterAsync("Oppgave");

        var cut = RenderList();

        var labels = cut.FindByTestId("task-row").QuerySelectorAll("td").Select(cell => cell.GetAttribute("data-label")).ToList();
        Assert.Equal(["Oppgave", "Ansvarlig", "Frist", "Status", ""], labels);
        Assert.Contains("table-cards", cut.FindByTestId("task-table").ClassName);
    }

    // Due dates

    [Fact]
    public async Task Render_OverdueTask_ShowsOverdueTextNotOnlyColor()
    {
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-2) });

        var cut = RenderList();

        Assert.Equal("Forfalt", cut.Find("tbody .status-badge.is-overdue").TextContent);
        Assert.Contains("16.11.2026", RowTexts(cut)[0]);
    }

    [Fact]
    public async Task Render_DueToday_ShowsDueTodayAndNotOverdue()
    {
        await RegisterAsync("I dag", new TaskItemDetails { Title = "I dag", DueDate = Today });

        var cut = RenderList();

        Assert.Equal("Frist i dag", cut.Find("tbody .status-badge.is-due-today").TextContent);
        Assert.Empty(cut.FindAll("tbody .is-overdue"));
    }

    [Fact]
    public async Task Render_FutureDueDate_ShowsNoMarker()
    {
        await RegisterAsync("Fremtid", new TaskItemDetails { Title = "Fremtid", DueDate = Today.AddDays(5) });

        var cut = RenderList();

        Assert.Empty(cut.FindAll("tbody .is-overdue, tbody .is-due-today"));
    }

    [Fact]
    public async Task Render_CompletedTaskWithOldDueDate_IsNeverOverdue()
    {
        var task = await RegisterAsync("Gammel", new TaskItemDetails { Title = "Gammel", DueDate = Today.AddDays(-30) });
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var cut = RenderList();
        Tab(cut, "done");

        Assert.Empty(cut.FindAll("tbody .is-overdue"));
        Assert.Equal("Forfalte 0", TabText(cut, "overdue"));
    }

    // Search and the combination with other choices

    [Fact]
    public async Task Search_MatchesTitleDescriptionAssignedToAndNumber()
    {
        await RegisterAsync("Rense takrenner");
        await RegisterAsync("Bytt pære", new TaskItemDetails { Title = "Bytt pære", Description = "I fiktiv trapp" });
        await RegisterAsync("Klipp hekk", new TaskItemDetails { Title = "Klipp hekk", AssignedTo = "Test Person" });
        var cut = RenderList();

        cut.FindByTestId("task-search").Input("TAKRENNER");
        Assert.Equal(["Rense takrenner"], Titles(cut));

        cut.FindByTestId("task-search").Input("fiktiv trapp");
        Assert.Equal(["Bytt pære"], Titles(cut));

        cut.FindByTestId("task-search").Input("test person");
        Assert.Equal(["Klipp hekk"], Titles(cut));

        cut.FindByTestId("task-search").Input("#2");
        Assert.Equal(["Bytt pære"], Titles(cut));
    }

    [Fact]
    public async Task Search_IsCombinedWithTheSelectedQuickChoice()
    {
        await RegisterAsync("Rense åpen takrenne");
        await RegisterCompletedAsync("Rense ferdig takrenne");
        await RegisterAsync("Noe annet");
        var cut = RenderList();

        cut.FindByTestId("task-search").Input("takrenne");
        Assert.Equal(["Rense åpen takrenne"], Titles(cut));

        Tab(cut, "done");
        Assert.Equal(["Rense ferdig takrenne"], Titles(cut));
        Assert.Contains("Søk: «takrenne»", cut.FindByTestId("active-filters").TextContent);
    }

    [Fact]
    public async Task Search_NoMatch_ExplainsThatTasksExistAndResetRestoresTheList()
    {
        await RegisterAsync("Rense takrenner");
        var cut = RenderList();

        cut.FindByTestId("task-search").Input("finnes ikke");

        var empty = cut.FindByTestId("empty-list").TextContent;
        Assert.Contains("Ingen oppgaver funnet", empty);
        Assert.Contains("Sameiet har 1 oppgave i alt", empty);
        Assert.Contains("Søk: «finnes ikke»", cut.FindByTestId("active-filters").TextContent);

        cut.ClickButton("Nullstill søk og filtre");

        Assert.Single(RowTexts(cut));
        Assert.Equal("", cut.FindByTestId("task-search").GetAttribute("value") ?? "");
        Assert.False(cut.HasTestId("active-filters"));
    }

    [Fact]
    public async Task Render_NoOpenTasks_ExplainsAndOffersToShowCompleted()
    {
        await RegisterCompletedAsync("Ferdig");

        var cut = RenderList();

        Assert.Contains("Ingen oppgaver funnet", cut.Markup);
        cut.ClickButton("Vis også fullførte");

        Assert.Single(RowTexts(cut));
        Assert.Contains("Status: Alle", cut.FindByTestId("active-filters").TextContent);
    }

    // More filters

    [Fact]
    public async Task Filter_AnyStatusCanBeChosen_ReplacesTheQuickChoiceAndShowsAnActiveFilter()
    {
        await RegisterAsync("Ikke startet");
        var inProgress = await RegisterAsync("Pågår");
        await Tasks.ChangeStatusAsync(inProgress.Id, TaskItemStatus.InProgress);
        await RegisterCompletedAsync("Ferdig");
        var cut = RenderList();

        Select(cut, "Status", "InProgress");
        Assert.Equal(["Pågår"], Titles(cut));
        Assert.Contains("Status: Pågår", cut.FindByTestId("active-filters").TextContent);
        Assert.Equal("false", cut.FindByTestId("quick-tab-open").GetAttribute("aria-pressed"));

        Select(cut, "Status", "Completed");
        Assert.Equal(["Ferdig"], Titles(cut));

        Select(cut, "Status", "all");
        Assert.Equal(3, cut.FindAll("[data-testid=task-row]").Count);

        Select(cut, "Status", "");
        Assert.Equal(2, cut.FindAll("[data-testid=task-row]").Count);
        Assert.Equal("true", cut.FindByTestId("quick-tab-open").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task Filter_CategoryAndPriority_AreCombinedAndShownAsActiveFilters()
    {
        await RegisterAsync("Treff", new TaskItemDetails
        {
            Title = "Treff",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.High
        });
        await RegisterAsync("Feil kategori", new TaskItemDetails { Title = "Feil kategori", Priority = TaskItemPriority.High });
        await RegisterAsync("Feil prioritet", new TaskItemDetails { Title = "Feil prioritet", Category = TaskItemCategory.Maintenance });
        var cut = RenderList();

        Select(cut, "Kategori", "Maintenance");
        Assert.Equal(2, cut.FindAll("[data-testid=task-row]").Count);

        Select(cut, "Prioritet", "High");
        Assert.Equal(["Treff"], Titles(cut));
        var chips = cut.FindByTestId("active-filters").TextContent;
        Assert.Contains("Kategori: Vedlikehold", chips);
        Assert.Contains("Prioritet: Høy", chips);

        Select(cut, "Status", "InProgress");
        Assert.Contains("Ingen oppgaver funnet", cut.Markup);
    }

    [Fact]
    public async Task Filter_ActiveFilterCanBeRemovedOneByOne()
    {
        await RegisterAsync("Treff", new TaskItemDetails { Title = "Treff", Priority = TaskItemPriority.High });
        await RegisterAsync("Annen");
        var cut = RenderList();
        Select(cut, "Prioritet", "High");
        Assert.Single(RowTexts(cut));

        cut.Find("[aria-label='Fjern filteret Prioritet: Høy']").Click();

        Assert.Equal(2, RowTexts(cut).Count);
        Assert.False(cut.HasTestId("active-filters"));
    }

    [Fact]
    public async Task Filter_HousingUnitAndBoardCase_ShowOnlyLinkedTasks()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter" });
        await RegisterAsync("Med enhet", new TaskItemDetails { Title = "Med enhet", HousingUnitId = unit.Id });
        await RegisterAsync("Med sak", new TaskItemDetails { Title = "Med sak", BoardCaseId = boardCase.Id });
        await RegisterAsync("Uten");
        var cut = RenderList();

        Select(cut, "Boenhet", unit.Id.ToString());
        Assert.Equal(["Med enhet"], Titles(cut));
        Assert.Contains("Boenhet: Storgata 1A", cut.FindByTestId("active-filters").TextContent);

        Select(cut, "Boenhet", "");
        Select(cut, "Styresak", boardCase.Id.ToString());
        Assert.Equal(["Med sak"], Titles(cut));
    }

    [Fact]
    public async Task Filter_MoreFiltersStartClosedButTellWhatIsInside()
    {
        await RegisterAsync("Oppgave");

        var cut = RenderList();

        var details = cut.Find("details.disclosure");
        Assert.False(details.HasAttribute("open"));
        Assert.Contains("Flere filtre", details.QuerySelector("summary")!.TextContent);
        Assert.Contains("Sortering", details.QuerySelector(".disclosure-hint")!.TextContent);
    }

    // Sorting

    [Fact]
    public async Task Sort_Default_PutsOverdueFirstThenNearestDueDateAndTasksWithoutDueDateLast()
    {
        await RegisterAsync("Uten frist");
        await RegisterAsync("Neste uke", new TaskItemDetails { Title = "Neste uke", DueDate = Today.AddDays(7) });
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-3) });
        await RegisterAsync("I morgen", new TaskItemDetails { Title = "I morgen", DueDate = Today.AddDays(1) });

        var cut = RenderList();

        Assert.Equal(["Forfalt", "I morgen", "Neste uke", "Uten frist"], Titles(cut));
    }

    [Fact]
    public async Task Sort_NewestFirst_OrdersByCreationTime()
    {
        await RegisterAsync("Eldst", new TaskItemDetails { Title = "Eldst", DueDate = Today });
        Clock.Advance(TimeSpan.FromHours(1));
        await RegisterAsync("Nyest", new TaskItemDetails { Title = "Nyest", DueDate = Today.AddDays(9) });
        var cut = RenderList();

        Select(cut, "Sortering", "Newest");

        Assert.Equal(["Nyest", "Eldst"], Titles(cut));
    }

    // Completing from the list

    [Fact]
    public async Task Complete_FromTheList_ShowsTheResultAtTheTaskAndKeepsTheRowUntilTheViewChanges()
    {
        var task = await RegisterAsync("Bestille service");
        await RegisterAsync("Annen");
        var cut = RenderList();

        cut.FindRowContaining("Bestille service").QuerySelector("[data-testid=complete-task]")!.Click();

        var row = cut.FindRowContaining("Bestille service");
        Assert.Equal(TaskItemStatus.Completed, (await Tasks.GetByIdAsync(task.Id))!.Status);
        var feedback = row.QuerySelector("[data-testid=action-feedback]")!;
        Assert.Contains("«Bestille service» er fullført.", feedback.TextContent);
        Assert.Equal("status", feedback.GetAttribute("role"));
        Assert.Contains("Fullført", row.QuerySelector(".status-badge")!.TextContent);
        Assert.Equal("Åpne 1", TabText(cut, "open"));
        Assert.Equal("Fullførte 1", TabText(cut, "done"));

        Tab(cut, "done");
        Assert.False(cut.HasTestId("action-feedback"));
        Assert.Equal(["Bestille service"], Titles(cut));
    }

    [Fact]
    public async Task Complete_FromTheList_KeepsTheRowInTheSamePlaceSoTheMessageIsWhereTheClickWas()
    {
        await RegisterAsync("Første", new TaskItemDetails { Title = "Første", DueDate = Today.AddDays(1) });
        await RegisterAsync("Midterste", new TaskItemDetails { Title = "Midterste", DueDate = Today.AddDays(2) });
        await RegisterAsync("Siste", new TaskItemDetails { Title = "Siste", DueDate = Today.AddDays(3) });
        var cut = RenderList();

        cut.FindRowContaining("Midterste").QuerySelector("[data-testid=complete-task]")!.Click();

        Assert.Equal(["Første", "Midterste", "Siste"], Titles(cut));
    }

    [Fact]
    public async Task Complete_ThenUndo_RestoresTheStatusTheTaskHadAndSaysSo()
    {
        var task = await RegisterAsync("Pågående");
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);
        var cut = RenderList();

        cut.FindByTestId("complete-task").Click();
        cut.FindByTestId("feedback-action").Click();

        var reloaded = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal(TaskItemStatus.InProgress, reloaded.Status);
        Assert.Null(reloaded.CompletedAt);
        Assert.Contains("Fullføringen er angret", cut.FindByTestId("action-feedback").TextContent);
        Assert.Contains("«Pågår»", cut.FindByTestId("action-feedback").TextContent);
        Assert.False(cut.HasTestId("feedback-action"));
        Assert.Equal("Åpne 1", TabText(cut, "open"));
    }

    [Fact]
    public async Task Reopen_FromTheDoneView_SetsInProgressAndExplainsTheNewStatus()
    {
        var task = await RegisterCompletedAsync("Ferdig");
        var cut = RenderList();
        Tab(cut, "done");

        cut.FindByTestId("reopen-task").Click();

        Assert.Equal(TaskItemStatus.InProgress, (await Tasks.GetByIdAsync(task.Id))!.Status);
        Assert.Contains("gjenåpnet og står nå som «Pågår»", cut.FindByTestId("action-feedback").TextContent);
        Assert.False(cut.HasTestId("reopen-task"));
        Assert.True(cut.HasTestId("complete-task"));
    }

    [Fact]
    public async Task Complete_DoubleClick_ChangesTheStatusOnlyOnce()
    {
        var task = await RegisterAsync("Dobbeltklikk");
        var cut = RenderList();
        TaskRepository.UpdateBlocker = new TaskCompletionSource();

        var first = cut.FindByTestId("complete-task").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=task-row] button").HasAttribute("disabled")));
        await cut.Find("[data-testid=task-row] button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        TaskRepository.UpdateBlocker.SetResult();
        await first;

        Assert.Equal(1, TaskRepository.UpdateCount);
        Assert.Equal(TaskItemStatus.Completed, (await Tasks.GetByIdAsync(task.Id))!.Status);
        Assert.Single(cut.FindAll("[data-testid=action-feedback]"));
    }

    [Fact]
    public async Task Complete_WhenSavingFails_ShowsAnErrorAtTheTaskThatStaysUntilClosed()
    {
        var task = await RegisterAsync("Skal feile");
        var cut = RenderList();
        TaskRepository.IsFailing = true;

        cut.FindByTestId("complete-task").Click();

        var alert = cut.FindByTestId("action-feedback");
        Assert.Equal("alert", alert.GetAttribute("role"));
        Assert.Contains("Kunne ikke lagre endringen av «Skal feile»", alert.TextContent);

        TaskRepository.IsFailing = false;
        Tab(cut, "done");
        Tab(cut, "open");
        Assert.True(cut.HasTestId("action-feedback"));

        cut.FindByTestId("feedback-dismiss").Click();
        Assert.False(cut.HasTestId("action-feedback"));
        Assert.Equal(TaskItemStatus.NotStarted, (await Tasks.GetByIdAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task Complete_ClickingTheButton_DoesNotNavigate()
    {
        await RegisterAsync("Bli på siden");
        var cut = RenderList();
        var before = CurrentPath;

        cut.FindByTestId("complete-task").Click();

        Assert.Equal(before, CurrentPath);
    }

    // Navigation and messages

    [Fact]
    public void Render_DeletedStatusInUrl_ShowsSuccessMessage()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/oppgaver?status=slettet");

        var cut = RenderList();

        Assert.Equal("Oppgaven er slettet permanent.", cut.Find(".alert-success").TextContent);
    }

    [Fact]
    public async Task Render_OverdueParameterFromTheDashboard_StartsOnTheOverdueChoice()
    {
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });
        await RegisterAsync("Uten frist");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/oppgaver?forfalt=1");

        var cut = RenderList();

        Assert.Equal(["Forfalt"], Titles(cut));
        Assert.Equal("true", cut.FindByTestId("quick-tab-overdue").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task Render_WithoutOverdueParameter_ShowsAllOpenTasks()
    {
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });
        await RegisterAsync("Uten frist");

        var cut = RenderList();

        Assert.Equal(2, cut.FindAll("[data-testid=task-row]").Count);
    }

    [Fact]
    public void Render_HasNewTaskAsThePrimaryActionInTheHeader()
    {
        var cut = RenderList();

        var primary = cut.FindByTestId("primary-action");
        Assert.Equal("Ny oppgave", primary.TextContent);
        Assert.Equal("/oppgaver/ny", primary.QuerySelector("a")!.GetAttribute("href"));
    }
}
