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
        cut.WaitForElement(".detail-grid, .empty-state, [role=alert]");

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

    // Information

    [Fact]
    public async Task Render_Task_ShowsAllInformationInNorwegian()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter" });
        var task = await RegisterAsync(new TaskItemDetails
        {
            Title = "Rense takrenner",
            Description = "Fiktiv beskrivelse.",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.High,
            DueDate = new DateOnly(2026, 12, 1),
            AssignedTo = "Test Person",
            HousingUnitId = unit.Id,
            BoardCaseId = boardCase.Id,
            Notes = "Fiktive merknader."
        });

        var cut = RenderDetail(task.Id);

        Assert.Equal("Rense takrenner", cut.Find("h1").TextContent);
        Assert.Contains($"Oppgave #{task.Id}", cut.Find(".detail-meta").TextContent);
        Assert.Equal("Vedlikehold", DetailValue(cut, "Kategori"));
        Assert.Equal("Ikke startet", DetailValue(cut, "Status"));
        Assert.Equal("Høy", DetailValue(cut, "Prioritet"));
        Assert.Equal("01.12.2026", DetailValue(cut, "Frist"));
        Assert.Equal("Test Person", DetailValue(cut, "Ansvarlig"));
        Assert.Equal("Storgata 1A", DetailValue(cut, "Boenhet"));
        Assert.Equal($"#{boardCase.Id} Maling av porter", DetailValue(cut, "Styresak"));
        Assert.Equal("18.11.2026 11:30", DetailValue(cut, "Opprettet"));
        Assert.Equal("18.11.2026 11:30", DetailValue(cut, "Sist oppdatert"));
        Assert.Contains("Fiktiv beskrivelse.", cut.Markup);
        Assert.Contains("Fiktive merknader.", cut.Markup);
    }

    [Fact]
    public async Task Render_TaskWithLinks_HasLinksToTheUnitAndTheBoardCase()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", HousingUnitId = unit.Id, BoardCaseId = boardCase.Id });

        var cut = RenderDetail(task.Id);

        var hrefs = cut.FindAll(".detail-list a").Select(link => link.GetAttribute("href")).ToList();
        Assert.Contains($"/boenheter/{unit.Id}", hrefs);
        Assert.Contains($"/styresaker/{boardCase.Id}", hrefs);
    }

    [Fact]
    public async Task Render_TaskWithoutOptionalValues_ShowsNothingRegistered()
    {
        var task = await RegisterAsync();

        var cut = RenderDetail(task.Id);

        Assert.Equal("Ingen frist", DetailValue(cut, "Frist"));
        Assert.Equal("Ikke registrert", DetailValue(cut, "Ansvarlig"));
        Assert.Equal("Ingen boenhet", DetailValue(cut, "Boenhet"));
        Assert.Equal("Ingen styresak", DetailValue(cut, "Styresak"));
        Assert.DoesNotContain(cut.FindAll(".detail-item dt"), term => term.TextContent == "Fullført");
    }

    [Fact]
    public async Task Render_LinkedUnitDeactivated_StillShowsTheUnitAsInactive()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = RenderDetail(task.Id);

        Assert.Equal("Storgata 1A (inaktiv)", DetailValue(cut, "Boenhet"));
    }

    [Fact]
    public async Task Render_StatusInUrl_ShowsSuccessMessage()
    {
        var task = await RegisterAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/oppgaver/{task.Id}?status=opprettet");

        var cut = RenderDetail(task.Id);

        Assert.Equal("Oppgaven er registrert.", cut.Find(".alert-success").TextContent);
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

        cut.WaitForElement(".detail-grid");
    }

    [Fact]
    public async Task Render_Task_HasEditAndBackLinks()
    {
        var task = await RegisterAsync();

        var cut = RenderDetail(task.Id);

        Assert.Equal($"/oppgaver/{task.Id}/rediger", cut.Find(".page-actions a").GetAttribute("href"));
        Assert.Equal("/oppgaver", cut.Find("a.back-link").GetAttribute("href"));
    }

    // Due dates

    [Fact]
    public async Task Render_OverdueTask_ShowsOverdueTextInHeaderAndDueDate()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });

        var cut = RenderDetail(task.Id);

        Assert.Equal(2, cut.FindAll(".is-overdue").Count);
        Assert.All(cut.FindAll(".is-overdue"), badge => Assert.Equal("Forfalt", badge.TextContent));
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
    public async Task Start_ChangesStatusToInProgressThroughTheStatusSelect()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);

        cut.Find("#newStatus").Change("InProgress");
        ClickButton(cut, "Endre status");

        Assert.Equal(TaskItemStatus.InProgress, (await Tasks.GetByIdAsync(task.Id))!.Status);
        cut.WaitForAssertion(() => Assert.Equal("Statusen er endret.", cut.Find(".alert-success").TextContent));
        Assert.Equal("Pågår", DetailValue(cut, "Status"));
    }

    [Fact]
    public async Task ChangeStatusButton_SameStatusSelected_IsDisabled()
    {
        var task = await RegisterAsync();

        var cut = RenderDetail(task.Id);

        Assert.True(cut.FindAll("button").First(button => button.TextContent.Trim() == "Endre status").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Complete_SetsCompletedAndShowsTheCompletionTime()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });
        Clock.Advance(TimeSpan.FromHours(2));
        var cut = RenderDetail(task.Id);
        Assert.DoesNotContain(cut.FindAll(".detail-item dt"), term => term.TextContent == "Fullført");

        ClickButton(cut, "Marker som fullført");

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal(TaskItemStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAt);
        cut.WaitForAssertion(() => Assert.Equal("Oppgaven er markert som fullført.", cut.Find(".alert-success").TextContent));
        Assert.Equal("18.11.2026 13:30", DetailValue(cut, "Fullført"));
        Assert.Equal("Fullført", DetailValue(cut, "Status"));
    }

    [Fact]
    public async Task Complete_OverdueTask_RemovesTheOverdueMarkers()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });
        var cut = RenderDetail(task.Id);
        Assert.NotEmpty(cut.FindAll(".is-overdue"));

        ClickButton(cut, "Marker som fullført");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".is-overdue")));
    }

    [Fact]
    public async Task Reopen_ClearsCompletionTimeAndTheTaskIsOverdueAgain()
    {
        var task = await RegisterAsync(new TaskItemDetails { Title = "Oppgave", DueDate = Today.AddDays(-1) });
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        var cut = RenderDetail(task.Id);
        Assert.Empty(cut.FindAll(".is-overdue"));

        ClickButton(cut, "Gjenåpne oppgaven");

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal(TaskItemStatus.InProgress, stored.Status);
        Assert.Null(stored.CompletedAt);
        cut.WaitForAssertion(() => Assert.Equal("Oppgaven er gjenåpnet.", cut.Find(".alert-success").TextContent));
        Assert.DoesNotContain(cut.FindAll(".detail-item dt"), term => term.TextContent == "Fullført");
        Assert.NotEmpty(cut.FindAll(".is-overdue"));
    }

    [Fact]
    public async Task StatusButtons_CompletedTaskOffersReopenNotComplete()
    {
        var task = await RegisterAsync();
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var cut = RenderDetail(task.Id);

        var buttons = cut.FindAll("button").Select(button => button.TextContent.Trim()).ToList();
        Assert.Contains("Gjenåpne oppgaven", buttons);
        Assert.DoesNotContain("Marker som fullført", buttons);
    }

    [Fact]
    public async Task Complete_TaskDeletedMeanwhile_ShowsNotFoundMessage()
    {
        var task = await RegisterAsync();
        var cut = RenderDetail(task.Id);
        await Tasks.DeleteAsync(task.Id);

        ClickButton(cut, "Marker som fullført");

        cut.WaitForAssertion(() => Assert.Contains("Oppgaven finnes ikke lenger", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public async Task Complete_DatabaseUnavailable_ShowsErrorAndDoesNotLogTaskText()
    {
        var logger = new CapturingLoggerProvider();
        Services.AddLogging(builder => builder.AddProvider(logger));
        var task = await RegisterAsync(new TaskItemDetails { Title = "Hemmelig tittel", Notes = "Hemmelige merknader" });
        var cut = RenderDetail(task.Id);
        TaskRepository.IsFailing = true;

        ClickButton(cut, "Marker som fullført");

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre endringen", cut.Find("[role=alert]").TextContent));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("Hemmelig"));
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
