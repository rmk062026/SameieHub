using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameieHub.Domain;
using SameieHub.Tests.Fakes;
using SameieHub.Web.Components.Pages.TaskItems;

namespace SameieHub.Tests.Web;

public class TaskDetailTests : UiTestBase
{
    // The test clock starts at 2026-11-18 10:30 UTC, which is 11:30 on 18 November in Norway.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private IRenderedComponent<TaskDetail> RenderDetail(int id)
    {
        var cut = Render<TaskDetail>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement("[data-testid=task-summary], .empty-state, [role=alert]");

        return cut;
    }

    private async Task<TaskItem> RegisterAsync(TaskItemDetails? details = null)
    {
        return await Tasks.RegisterAsync(details ?? new TaskItemDetails { Title = "Rense takrenner" });
    }

    private static void ClickButton(IRenderedComponent<TaskDetail> cut, string text)
    {
        cut.FindAll("button").First(button => button.TextContent.Trim() == text).Click();
    }

    private static string DetailValue(IRenderedComponent<TaskDetail> cut, string label)
    {
        var item = cut.FindAll(".detail-item").First(element => element.QuerySelector("dt")!.TextContent == label);

        return item.QuerySelector("dd")!.TextContent.Trim();
    }

    private static string Feedback(IRenderedComponent<TaskDetail> cut)
    {
        return cut.FindByTestId("action-feedback").TextContent;
    }

    // Information

    [Fact]
    public async Task Render_Task_ShowsTheMostImportantInformationFirst()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter" });
        var task = await RegisterAsync(new TaskItemDetails
        {
            Title = "Rense takrenner",
            Description = "Fiktiv beskrivelse.",
            Priority = TaskItemPriority.High,
            DueDate = new DateOnly(2026, 12, 1),
            AssignedTo = "Test Person",
            HousingUnitId = unit.Id,
            BoardCaseId = boardCase.Id
        });

        var cut = RenderDetail(task.Id);

        Assert.Equal("Rense takrenner", cut.FindByTestId("page-title").TextContent);
        var summary = cut.FindByTestId("task-summary");
        Assert.Contains("Ikke startet", summary.TextContent);
        Assert.Equal("Høy prioritet", summary.QuerySelector("[data-testid=high-priority]")!.TextContent);
        Assert.Equal("01.12.2026", DetailValue(cut, "Frist"));
        Assert.Equal("Test Person", DetailValue(cut, "Ansvarlig"));
        Assert.Equal("Storgata 1A", DetailValue(cut, "Gjelder"));
        Assert.Equal($"#{boardCase.Id} Maling av porter", DetailValue(cut, "Styresak"));
        Assert.Contains("Fiktiv beskrivelse.", cut.FindByTestId("task-description").TextContent);
    }

    [Fact]
    public async Task Render_Task_KeepsRarelyUsedInformationInAClosedDetailsSection()
    {
        var task = await RegisterAsync(new TaskItemDetails
        {
            Title = "Rense takrenner",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.Low,
            Notes = "Fiktive merknader."
        });

        var cut = RenderDetail(task.Id);

        var details = cut.Find("details.disclosure");
        Assert.False(details.HasAttribute("open"));
        Assert.Equal("Detaljer", details.QuerySelector(".disclosure-label")!.TextContent);
        Assert.Equal($"#{task.Id}", DetailValue(cut, "Oppgavenummer"));
        Assert.Equal("Vedlikehold", DetailValue(cut, "Kategori"));
        Assert.Equal("Lav", DetailValue(cut, "Prioritet"));
        Assert.Equal("Fiktive merknader.", DetailValue(cut, "Merknader"));
        Assert.Equal("18.11.2026 11:30", DetailValue(cut, "Opprettet"));
        Assert.Equal("18.11.2026 11:30", DetailValue(cut, "Sist oppdatert"));
        Assert.False(cut.HasTestId("high-priority"));
    }

    [Fact]
    public async Task Render_TaskWithLinks_HasLinksToTheUnitAndTheBoardCase()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", HousingUnitId = unit.Id, BoardCaseId = boardCase.Id });

        var cut = RenderDetail(task.Id);

        var hrefs = cut.FindByTestId("task-summary").QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList();
        Assert.Contains($"/boenheter/{unit.Id}", hrefs);
        Assert.Contains($"/styresaker/{boardCase.Id}", hrefs);
    }

    [Fact]
    public async Task Render_TaskWithoutOptionalValues_SaysWhatIsMissing()
    {
        var task = await RegisterAsync();

        var cut = RenderDetail(task.Id);

        Assert.Equal("Ingen frist", DetailValue(cut, "Frist"));
        Assert.Equal("Ikke tildelt", DetailValue(cut, "Ansvarlig"));
        Assert.Equal("Hele sameiet", DetailValue(cut, "Gjelder"));
        Assert.Equal("Ingen styresak", DetailValue(cut, "Styresak"));
        Assert.Contains("Ikke registrert", cut.FindByTestId("task-description").TextContent);
        Assert.DoesNotContain(cut.FindAll(".detail-item dt"), term => term.TextContent == "Fullført");
    }

    [Fact]
    public async Task Render_LinkedUnitDeactivated_StillShowsTheUnitAsInactive()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = RenderDetail(task.Id);

        Assert.Equal("Storgata 1A (inaktiv)", DetailValue(cut, "Gjelder"));
    }

    [Fact]
    public async Task Render_StatusInUrl_ShowsSuccessMessage()
    {
        var task = await RegisterAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/oppgaver/{task.Id}?status=opprettet");

        var cut = RenderDetail(task.Id);

        Assert.Contains("Oppgaven er registrert.", Feedback(cut));
        Assert.Equal("status", cut.FindByTestId("action-feedback").GetAttribute("role"));
    }

    [Fact]
    public void Render_UnknownTask_ShowsNotFoundMessage()
    {
        var cut = RenderDetail(999);

        Assert.Contains("Fant ikke oppgaven", cut.Markup);
    }

    [Fact]
    public async Task Render_DatabaseUnavailable_ShowsErrorAndRetryShowsTheTask()
    {
        var task = await RegisterAsync();
        TaskRepository.IsFailing = true;
        var cut = Render<TaskDetail>(parameters => parameters.Add(page => page.Id, task.Id));
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente oppgaven", cut.Find("[role=alert]").TextContent));

        TaskRepository.IsFailing = false;
        cut.Find("[role=alert] button").Click();

        cut.WaitForElement("[data-testid=task-summary]");
    }

    [Fact]
    public async Task Render_Task_HasEditLinkBackLinkAndTheMainActionInTheHeader()
    {
        var task = await RegisterAsync();

        var cut = RenderDetail(task.Id);

        Assert.Equal($"/oppgaver/{task.Id}/rediger", cut.FindByTestId("page-actions").QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("/oppgaver", cut.Find("a.back-link").GetAttribute("href"));
        Assert.Equal("Marker som fullført", cut.FindByTestId("primary-action").TextContent.Trim());
    }

    // Due dates

    [Fact]
    public async Task Render_OverdueTask_ShowsOverdueTextNextToTheDueDate()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });

        var cut = RenderDetail(task.Id);

        var badge = Assert.Single(cut.FindAll(".is-overdue"));
        Assert.Equal("Forfalt", badge.TextContent);
        Assert.Contains("17.11.2026", DetailValue(cut, "Frist"));
    }

    [Fact]
    public async Task Render_DueToday_ShowsDueTodayAndNotOverdue()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today });

        var cut = RenderDetail(task.Id);

        Assert.Equal("Frist i dag", cut.Find(".is-due-today").TextContent);
        Assert.Empty(cut.FindAll(".is-overdue"));
    }

    [Fact]
    public async Task Render_CompletedTaskWithOldDueDate_IsNotOverdue()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-10) });
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var cut = RenderDetail(task.Id);

        Assert.Empty(cut.FindAll(".is-overdue, .is-due-today"));
    }

    // Status

    [Fact]
    public async Task Start_ChangesStatusToInProgressFromTheMoreActionsMenu()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        Assert.False(cut.HasTestId("set-status-NotStarted"));

        cut.FindByTestId("set-status-InProgress").Click();

        Assert.Equal(TaskItemStatus.InProgress, (await Tasks.GetByIdAsync(task.Id))!.Status);
        Assert.Contains("Statusen er endret til «Pågår».", Feedback(cut));
        Assert.Contains("Pågår", cut.FindByTestId("task-summary").TextContent);
        Assert.False(cut.HasTestId("set-status-InProgress"));
        Assert.True(cut.HasTestId("set-status-NotStarted"));
    }

    [Fact]
    public async Task MoreActions_StartsClosedAndClosesAgainAfterAnActionHasBeenUsed()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        Assert.False(cut.FindByTestId("more-menu").HasAttribute("open"));

        cut.FindByTestId("set-status-InProgress").Click();

        Assert.False(cut.FindByTestId("more-menu").HasAttribute("open"));
    }

    [Fact]
    public async Task Complete_SetsCompletedAndShowsTheResultAtTheAction()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });
        Clock.Advance(TimeSpan.FromHours(2));
        var cut = RenderDetail(task.Id);
        Assert.DoesNotContain(cut.FindAll(".detail-item dt"), term => term.TextContent == "Fullført");

        cut.FindByTestId("complete-task").Click();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal(TaskItemStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAt);
        Assert.Contains("Oppgaven er markert som fullført.", Feedback(cut));
        Assert.Equal("18.11.2026 13:30", DetailValue(cut, "Fullført"));
        Assert.Contains("Fullført", cut.FindByTestId("task-summary").TextContent);
    }

    [Fact]
    public async Task Complete_OverdueTask_RemovesTheOverdueMarkers()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });
        var cut = RenderDetail(task.Id);
        Assert.NotEmpty(cut.FindAll(".is-overdue"));

        cut.FindByTestId("complete-task").Click();

        Assert.Empty(cut.FindAll(".is-overdue"));
    }

    [Fact]
    public async Task Complete_ThenUndo_RestoresTheStatusTheTaskHadAndSaysSo()
    {
        var task = await RegisterAsync();
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);
        var cut = RenderDetail(task.Id);

        cut.FindByTestId("complete-task").Click();
        cut.FindByTestId("feedback-action").Click();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal(TaskItemStatus.InProgress, stored.Status);
        Assert.Null(stored.CompletedAt);
        Assert.Contains("Fullføringen er angret. Oppgaven står igjen som «Pågår».", Feedback(cut));
        Assert.False(cut.HasTestId("feedback-action"));
        Assert.True(cut.HasTestId("complete-task"));
    }

    [Fact]
    public async Task Reopen_ClearsCompletionTimeAndTheTaskIsOverdueAgain()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        var cut = RenderDetail(task.Id);
        Assert.Empty(cut.FindAll(".is-overdue"));

        cut.FindByTestId("reopen-task").Click();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal(TaskItemStatus.InProgress, stored.Status);
        Assert.Null(stored.CompletedAt);
        Assert.Contains("Oppgaven er gjenåpnet og står nå som «Pågår».", Feedback(cut));
        Assert.False(cut.HasTestId("feedback-action"));
        Assert.DoesNotContain(cut.FindAll(".detail-item dt"), term => term.TextContent == "Fullført");
        Assert.NotEmpty(cut.FindAll(".is-overdue"));
    }

    [Fact]
    public async Task StatusButtons_CompletedTaskOffersReopenNotComplete_AndAllOtherStatusesRemainReachable()
    {
        var task = await RegisterAsync();
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var cut = RenderDetail(task.Id);

        Assert.Equal("Gjenåpne oppgaven", cut.FindByTestId("primary-action").TextContent.Trim());
        Assert.False(cut.HasTestId("complete-task"));
        Assert.True(cut.HasTestId("set-status-NotStarted"));
        Assert.True(cut.HasTestId("set-status-InProgress"));
    }

    [Fact]
    public async Task Complete_TaskDeletedMeanwhile_ShowsNotFoundMessage()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        await Tasks.DeleteAsync(task.Id);

        cut.FindByTestId("complete-task").Click();

        Assert.Contains("Oppgaven finnes ikke lenger", cut.Find("[role=alert]").TextContent);
    }

    [Fact]
    public async Task Complete_DatabaseUnavailable_ShowsErrorUntilClosedAndDoesNotLogTaskText()
    {
        var logger = new CapturingLoggerProvider();
        Services.AddLogging(builder => builder.AddProvider(logger));
        var task = await RegisterAsync(new TaskItemDetails { Title = "Hemmelig tittel", Notes = "Hemmelige merknader" });
        var cut = RenderDetail(task.Id);
        TaskRepository.IsFailing = true;

        cut.FindByTestId("complete-task").Click();

        Assert.Contains("Kunne ikke lagre endringen", cut.Find("[role=alert]").TextContent);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("Hemmelig"));

        cut.FindByTestId("feedback-dismiss").Click();
        Assert.Empty(cut.FindAll("[role=alert]"));
    }

    [Fact]
    public async Task Complete_WhileSaving_DisablesTheButtonSoItCannotBeClickedTwice()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        TaskRepository.UpdateBlocker = new TaskCompletionSource();

        var first = cut.FindByTestId("complete-task").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => Assert.True(cut.FindByTestId("primary-action").QuerySelector("button")!.HasAttribute("disabled")));
        await cut.FindByTestId("primary-action").QuerySelector("button")!.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        TaskRepository.UpdateBlocker.SetResult();
        await first;

        Assert.Equal(1, TaskRepository.UpdateCount);
    }

    // Deletion

    [Fact]
    public async Task Render_Task_ShowsDeleteButtonButNoConfirmationAndDeletesNothing()
    {
        var task = await RegisterAsync();

        var cut = RenderDetail(task.Id);

        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Trim() == "Slett oppgaven …");
        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.NotNull(await Tasks.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task Delete_ClickingDelete_AsksForConfirmationWithTitleAndWarningAndDeletesNothing()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);

        ClickButton(cut, "Slett oppgaven …");

        var dialog = cut.Find("[role=alertdialog]");
        Assert.Contains("«Rense takrenner»", dialog.TextContent);
        Assert.Contains("Dette kan ikke angres", dialog.TextContent);
        Assert.Contains("ikke slettet", dialog.TextContent);
        Assert.NotNull(await Tasks.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task Delete_Cancel_ClosesConfirmationAndDeletesNothing()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        ClickButton(cut, "Slett oppgaven …");

        ClickButton(cut, "Avbryt");

        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.NotNull(await Tasks.GetByIdAsync(task.Id));
        Assert.Contains(cut.FindAll("button"), button => button.TextContent.Trim() == "Slett oppgaven …");
    }

    [Fact]
    public async Task Delete_Confirm_DeletesOnlyTheTaskAndGoesToListWithMessage()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", HousingUnitId = unit.Id, BoardCaseId = boardCase.Id });
        var other = await RegisterAsync(new TaskItemDetails { Title = "Annen oppgave" });
        var cut = RenderDetail(task.Id);
        ClickButton(cut, "Slett oppgaven …");

        ClickButton(cut, "Ja, slett permanent");

        Assert.Null(await Tasks.GetByIdAsync(task.Id));
        Assert.NotNull(await Tasks.GetByIdAsync(other.Id));
        Assert.NotNull(await Service.GetByIdAsync(unit.Id));
        Assert.NotNull(await BoardCases.GetByIdAsync(boardCase.Id));
        Assert.Equal("/oppgaver?status=slettet", CurrentPath);
    }

    [Fact]
    public async Task Delete_DatabaseUnavailable_ShowsErrorAndKeepsTheTask()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        ClickButton(cut, "Slett oppgaven …");
        TaskRepository.IsFailing = true;

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke slette oppgaven", cut.Find("[role=alert]").TextContent));
        TaskRepository.IsFailing = false;
        Assert.NotNull(await Tasks.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task Delete_TaskAlreadyDeleted_ShowsNotFoundMessage()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        ClickButton(cut, "Slett oppgaven …");
        await Tasks.DeleteAsync(task.Id);

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("Oppgaven finnes ikke lenger", cut.Find("[role=alert]").TextContent));
    }
}
