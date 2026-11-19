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

    private IRenderedComponent<TaskList> RenderList()
    {
        var cut = Render<TaskList>();
        cut.WaitForElement(".stat-row, .empty-state, [role=alert]");

        return cut;
    }

    private static IReadOnlyList<string> RowTexts(IRenderedComponent<TaskList> cut)
    {
        return cut.FindAll("tbody tr").Select(row => row.TextContent).ToList();
    }

    private static void Select(IRenderedComponent<TaskList> cut, string label, string value)
    {
        cut.Find($"select[aria-label='{label}']").Change(value);
    }

    private static string StatValue(IRenderedComponent<TaskList> cut, string label)
    {
        var card = cut.FindAll(".stat-card").First(element => element.QuerySelector(".stat-label")!.TextContent == label);

        return card.QuerySelector(".stat-value")!.TextContent;
    }

    // Empty and loading

    [Fact]
    public void Render_NoTasks_ShowsEmptyStateWithLinkToRegister()
    {
        var cut = RenderList();

        Assert.Contains("Ingen oppgaver registrert ennå", cut.Markup);
        Assert.Empty(cut.FindAll("table"));
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

    // Statistics

    [Fact]
    public async Task Render_Tasks_ShowsStatisticsFromTheActualTasks()
    {
        await RegisterAsync("Ikke startet");
        var inProgress = await RegisterAsync("Pågår");
        await Tasks.ChangeStatusAsync(inProgress.Id, TaskItemStatus.InProgress);
        var completed = await RegisterAsync("Ferdig");
        await Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });

        var cut = RenderList();

        Assert.Equal("4", StatValue(cut, "Totalt"));
        Assert.Equal("2", StatValue(cut, "Ikke startet"));
        Assert.Equal("1", StatValue(cut, "Pågår"));
        Assert.Equal("1", StatValue(cut, "Fullført"));
        Assert.Equal("1", StatValue(cut, "Forfalt"));
    }

    [Fact]
    public async Task Render_NearMidnight_UsesTheNorwegianDateForTheOverdueCount()
    {
        await RegisterAsync("Frist 18.", new TaskItemDetails { Title = "Frist 18.", DueDate = new DateOnly(2026, 11, 18) });

        // 22:30 UTC is 23:30 in Norway, still 18 November: not overdue.
        Clock.Advance(TimeSpan.FromHours(12));
        Assert.Equal("0", StatValue(RenderList(), "Forfalt"));

        // 23:30 UTC is 00:30 on 19 November in Norway: overdue, although it is still 18 November in UTC.
        Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal("1", StatValue(RenderList(), "Forfalt"));
    }

    // Default view and rows

    [Fact]
    public async Task Render_Default_ShowsOnlyTasksThatAreNotCompleted()
    {
        await RegisterAsync("Åpen");
        var completed = await RegisterAsync("Ferdig");
        await Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);

        var cut = RenderList();

        var row = Assert.Single(RowTexts(cut));
        Assert.Contains("Åpen", row);
        Assert.Contains("Viser 1 av 2 oppgaver", cut.Find(".summary").TextContent);
    }

    [Fact]
    public async Task Filter_AllAndCompleted_ShowCompletedTasks()
    {
        await RegisterAsync("Åpen");
        var completed = await RegisterAsync("Ferdig");
        await Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);
        var cut = RenderList();

        Select(cut, "Filtrer på status", "all");
        Assert.Equal(2, cut.FindAll("tbody tr").Count);

        Select(cut, "Filtrer på status", "Completed");
        Assert.Contains("Ferdig", Assert.Single(RowTexts(cut)));
    }

    [Fact]
    public async Task Render_Task_ShowsIdTitleCategoryStatusPriorityDueDateAssignedToAndLinks()
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

        var row = Assert.Single(RowTexts(cut));
        Assert.Contains($"#{task.Id}", row);
        Assert.Contains("Rense takrenner", row);
        Assert.Contains("Vedlikehold", row);
        Assert.Contains("Ikke startet", row);
        Assert.Contains("Høy", row);
        Assert.Contains("01.12.2026", row);
        Assert.Contains("Test Person", row);
        Assert.Contains("Styresak #1 Maling av porter", row);
        Assert.Contains("Storgata 1A", row);
        Assert.Equal($"/oppgaver/{task.Id}", cut.Find("tbody a.unit-link").GetAttribute("href"));
    }

    [Fact]
    public async Task Render_TaskWithoutDueDate_ShowsNoDueDateText()
    {
        await RegisterAsync("Uten frist");

        var cut = RenderList();

        Assert.Contains("Ingen frist", Assert.Single(RowTexts(cut)));
    }

    [Fact]
    public async Task Render_Task_HasOpenAndEditLinks()
    {
        var task = await RegisterAsync("Oppgave");

        var cut = RenderList();

        var hrefs = cut.FindAll("tbody td.col-actions a").Select(link => link.GetAttribute("href")).ToList();
        Assert.Equal([$"/oppgaver/{task.Id}", $"/oppgaver/{task.Id}/rediger"], hrefs);
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
        Select(cut, "Filtrer på status", "all");

        Assert.Empty(cut.FindAll("tbody .is-overdue"));
        Assert.Equal("0", StatValue(cut, "Forfalt"));
    }

    // Search

    [Fact]
    public async Task Search_MatchesTitleDescriptionAssignedToAndNumber()
    {
        await RegisterAsync("Rense takrenner");
        await RegisterAsync("Bytt pære", new TaskItemDetails { Title = "Bytt pære", Description = "I fiktiv trapp" });
        await RegisterAsync("Klipp hekk", new TaskItemDetails { Title = "Klipp hekk", AssignedTo = "Test Person" });
        var cut = RenderList();

        cut.Find("input[type=search]").Input("TAKRENNER");
        Assert.Contains("Rense takrenner", Assert.Single(RowTexts(cut)));

        cut.Find("input[type=search]").Input("fiktiv trapp");
        Assert.Contains("Bytt pære", Assert.Single(RowTexts(cut)));

        cut.Find("input[type=search]").Input("test person");
        Assert.Contains("Klipp hekk", Assert.Single(RowTexts(cut)));

        cut.Find("input[type=search]").Input("#2");
        Assert.Contains("Bytt pære", Assert.Single(RowTexts(cut)));
    }

    [Fact]
    public async Task Search_NoMatch_ShowsEmptyMessageAndResetRestoresTheList()
    {
        await RegisterAsync("Rense takrenner");
        var cut = RenderList();

        cut.Find("input[type=search]").Input("finnes ikke");
        Assert.Contains("Ingen oppgaver funnet", cut.Markup);

        cut.FindAll("button").First(button => button.TextContent.Contains("Nullstill")).Click();

        Assert.Single(RowTexts(cut));
        Assert.Equal("", cut.Find("input[type=search]").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task Render_AllTasksCompleted_ExplainsAndOffersToShowCompleted()
    {
        var task = await RegisterAsync("Ferdig");
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var cut = RenderList();

        Assert.Contains("Ingen oppgaver funnet", cut.Markup);
        cut.FindAll("button").First(button => button.TextContent.Contains("Vis også fullførte")).Click();
        Assert.Single(RowTexts(cut));
    }

    // Filters

    [Fact]
    public async Task Filter_StatusCategoryAndPriority_AreCombined()
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

        Select(cut, "Filtrer på kategori", "Maintenance");
        Assert.Equal(2, cut.FindAll("tbody tr").Count);

        Select(cut, "Filtrer på prioritet", "High");
        Assert.Contains("Treff", Assert.Single(RowTexts(cut)));

        Select(cut, "Filtrer på status", "InProgress");
        Assert.Contains("Ingen oppgaver funnet", cut.Markup);
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

        Select(cut, "Filtrer på boenhet", unit.Id.ToString());
        Assert.Contains("Med enhet", Assert.Single(RowTexts(cut)));

        Select(cut, "Filtrer på boenhet", "");
        Select(cut, "Filtrer på styresak", boardCase.Id.ToString());
        Assert.Contains("Med sak", Assert.Single(RowTexts(cut)));
    }

    [Fact]
    public async Task Filter_OverdueOnly_ShowsOnlyOverdueTasks()
    {
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });
        await RegisterAsync("I dag", new TaskItemDetails { Title = "I dag", DueDate = Today });
        await RegisterAsync("Uten frist");
        var cut = RenderList();

        cut.Find("#overdueOnly").Change(true);

        Assert.Contains("Forfalt", Assert.Single(RowTexts(cut)));
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

        var titles = cut.FindAll("tbody a.unit-link").Select(link => link.TextContent).ToList();
        Assert.Equal(["Forfalt", "I morgen", "Neste uke", "Uten frist"], titles);
    }

    [Fact]
    public async Task Sort_NewestFirst_OrdersByCreationTime()
    {
        await RegisterAsync("Eldst", new TaskItemDetails { Title = "Eldst", DueDate = Today });
        Clock.Advance(TimeSpan.FromHours(1));
        await RegisterAsync("Nyest", new TaskItemDetails { Title = "Nyest", DueDate = Today.AddDays(9) });
        var cut = RenderList();

        Select(cut, "Sorter", "Newest");

        var titles = cut.FindAll("tbody a.unit-link").Select(link => link.TextContent).ToList();
        Assert.Equal(["Nyest", "Eldst"], titles);
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
    public async Task Render_OverdueParameterFromTheDashboard_StartsWithOnlyOverdueTasks()
    {
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });
        await RegisterAsync("Uten frist");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/oppgaver?forfalt=1");

        var cut = RenderList();

        Assert.Contains("Forfalt", Assert.Single(RowTexts(cut)));
        Assert.True((bool)cut.Find("#overdueOnly").HasAttribute("checked"));
    }

    [Fact]
    public async Task Render_WithoutOverdueParameter_ShowsAllOpenTasks()
    {
        await RegisterAsync("Forfalt", new TaskItemDetails { Title = "Forfalt", DueDate = Today.AddDays(-1) });
        await RegisterAsync("Uten frist");

        var cut = RenderList();

        Assert.Equal(2, cut.FindAll("tbody tr").Count);
    }

    [Fact]
    public void Render_HasRegisterButtonInTheHeader()
    {
        var cut = RenderList();

        Assert.Contains(cut.FindAll(".page-actions a"), link => link.GetAttribute("href") == "/oppgaver/ny");
    }
}
