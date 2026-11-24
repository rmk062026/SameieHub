using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.TaskItems;

namespace SameieHub.Tests.Web;

public class TaskEditTests : UiTestBase
{
    private IRenderedComponent<TaskEdit> RenderEdit(int id)
    {
        var cut = Render<TaskEdit>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement(".empty-state, #title, [role=alert]");

        return cut;
    }

    private async Task<TaskItem> RegisterFullTaskAsync(int? housingUnitId = null, int? boardCaseId = null)
    {
        return await Tasks.RegisterAsync(new TaskItemDetails
        {
            Title = "Rense takrenner",
            Description = "Fiktiv beskrivelse.",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.High,
            DueDate = new DateOnly(2026, 12, 1),
            AssignedTo = "Test Person",
            HousingUnitId = housingUnitId,
            BoardCaseId = boardCaseId,
            Notes = "Fiktive merknader."
        });
    }

    [Fact]
    public async Task Render_ExistingTask_ShowsCurrentValues()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var task = await RegisterFullTaskAsync(unit.Id, boardCase.Id);

        var cut = RenderEdit(task.Id);

        Assert.Equal("Rense takrenner", cut.Find("#title").GetAttribute("value"));
        Assert.Equal("Fiktiv beskrivelse.", cut.Find("#description").GetAttribute("value"));
        Assert.Equal("Maintenance", cut.Find("#category").GetAttribute("value"));
        Assert.Equal("High", cut.Find("#priority").GetAttribute("value"));
        Assert.Equal("2026-12-01", cut.Find("#dueDate").GetAttribute("value"));
        Assert.Equal("Test Person", cut.Find("#assignedTo").GetAttribute("value"));
        Assert.Equal(unit.Id.ToString(), cut.SelectedHousingUnit());
        Assert.Equal(boardCase.Id.ToString(), cut.Find("#boardCaseId").GetAttribute("value"));
        Assert.Equal("Fiktive merknader.", cut.Find("#notes").GetAttribute("value"));
    }

    [Fact]
    public async Task Submit_ChangedValues_AreSavedAndGoToTheDetailPage()
    {
        var task = await RegisterFullTaskAsync();
        var cut = RenderEdit(task.Id);

        cut.Find("#title").Change("Ny tittel");
        cut.Find("#priority").Change("Low");
        cut.Find("#dueDate").Change("2026-12-24");
        cut.Find("#notes").Change("");
        cut.Find("form").Submit();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        cut.WaitForAssertion(() => Assert.Equal($"/oppgaver/{task.Id}?status=endret", CurrentPath));
        Assert.Equal("Ny tittel", stored.Title);
        Assert.Equal(TaskItemPriority.Low, stored.Priority);
        Assert.Equal(new DateOnly(2026, 12, 24), stored.DueDate);
        Assert.Null(stored.Notes);
    }

    [Fact]
    public async Task Submit_CompletedTask_KeepsStatusAndCompletionTime()
    {
        var task = await RegisterFullTaskAsync();
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        var completedAt = (await Tasks.GetByIdAsync(task.Id))!.CompletedAt;
        Clock.Advance(TimeSpan.FromHours(3));
        var cut = RenderEdit(task.Id);

        cut.Find("#title").Change("Rettet tittel");
        cut.Find("form").Submit();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal("Rettet tittel", stored.Title);
        Assert.Equal(TaskItemStatus.Completed, stored.Status);
        Assert.Equal(completedAt, stored.CompletedAt);
    }

    [Fact]
    public async Task Submit_InvalidValue_ShowsMessageAndStoresNoPartialChange()
    {
        var task = await RegisterFullTaskAsync();
        var cut = RenderEdit(task.Id);

        cut.Find("#title").Change("Ny tittel");
        cut.Find("#dueDate").Change("1999-01-01");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Fristen må være mellom", cut.Find(".validation-message").TextContent));
        Assert.Equal("Rense takrenner", (await Tasks.GetByIdAsync(task.Id))!.Title);
    }

    [Fact]
    public async Task Submit_EmptyTitle_ShowsMessageAndKeepsTheStoredTitle()
    {
        var task = await RegisterFullTaskAsync();
        var cut = RenderEdit(task.Id);

        cut.Find("#title").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Tittelen må fylles ut.", cut.Find(".validation-message").TextContent));
        Assert.Equal("Rense takrenner", (await Tasks.GetByIdAsync(task.Id))!.Title);
    }

    // Links

    [Fact]
    public async Task Render_LinkedUnitDeactivatedLater_StaysSelectedAndTheTaskCanStillBeSaved()
    {
        var linked = await Service.RegisterAsync("Storgata 1A");
        var other = await Service.RegisterAsync("Storgata 1B");
        var otherInactive = await Service.RegisterAsync("Storgata 1C");
        var task = await RegisterFullTaskAsync(linked.Id);
        await Service.DeactivateAsync(linked.Id);
        await Service.DeactivateAsync(otherInactive.Id);

        var cut = RenderEdit(task.Id);

        Assert.Equal(linked.Id.ToString(), cut.SelectedHousingUnit());
        Assert.Equal(
            ["Velg boenhet", "Storgata 1A (inaktiv)", "Storgata 1B"],
            cut.FindAll("#housingUnitId option").Select(option => option.TextContent));
        Assert.DoesNotContain(cut.FindAll("#housingUnitId option"), option => option.TextContent.Contains("1C"));

        cut.Find("#title").Change("Ny tittel");
        cut.Find("form").Submit();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal("Ny tittel", stored.Title);
        Assert.Equal(linked.Id, stored.HousingUnitId);
        Assert.Equal(other.Id, (await Service.GetByIdAsync(other.Id))!.Id);
    }

    [Fact]
    public async Task Submit_ChangeToAnotherInactiveUnitIsRejected()
    {
        var active = await Service.RegisterAsync("Storgata 1A");
        var task = await RegisterFullTaskAsync(active.Id);
        var cut = RenderEdit(task.Id);
        cut.ChooseWholeCooperative();
        var target = await Service.RegisterAsync("Storgata 1B");
        cut.ChooseHousingUnit(target.Id);
        await Service.DeactivateAsync(target.Id);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Boenheten finnes ikke eller er inaktiv", cut.Find(".validation-message").TextContent));
        Assert.Equal(active.Id, (await Tasks.GetByIdAsync(task.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task Submit_LinksCanBeRemoved()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var task = await RegisterFullTaskAsync(unit.Id, boardCase.Id);
        var cut = RenderEdit(task.Id);

        cut.ChooseWholeCooperative();
        cut.Find("#boardCaseId").Change("");
        cut.Find("form").Submit();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Null(stored.HousingUnitId);
        Assert.Null(stored.BoardCaseId);
        Assert.NotNull(await Service.GetByIdAsync(unit.Id));
        Assert.NotNull(await BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task Render_LinkedClosedBoardCase_IsSelectable()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);
        var task = await RegisterFullTaskAsync(boardCaseId: boardCase.Id);

        var cut = RenderEdit(task.Id);

        Assert.Equal(boardCase.Id.ToString(), cut.Find("#boardCaseId").GetAttribute("value"));
    }

    // Errors

    [Fact]
    public void Render_UnknownTask_ShowsNotFoundMessage()
    {
        var cut = RenderEdit(999);

        Assert.Contains("Fant ikke oppgaven", cut.Markup);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task Submit_TaskDeletedMeanwhile_ShowsNotFoundMessage()
    {
        var task = await RegisterFullTaskAsync();
        var cut = RenderEdit(task.Id);
        await Tasks.DeleteAsync(task.Id);

        cut.Find("#title").Change("Ny tittel");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Oppgaven finnes ikke lenger", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public async Task Submit_DatabaseUnavailable_ShowsErrorAndKeepsInput()
    {
        var task = await RegisterFullTaskAsync();
        var cut = RenderEdit(task.Id);
        TaskRepository.IsFailing = true;

        cut.Find("#title").Change("Ny tittel");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Ny tittel", cut.Find("#title").GetAttribute("value"));
    }

    [Fact]
    public async Task Render_DatabaseUnavailable_ShowsErrorAndRetryShowsTheForm()
    {
        var task = await RegisterFullTaskAsync();
        TaskRepository.IsFailing = true;
        var cut = Render<TaskEdit>(parameters => parameters.Add(page => page.Id, task.Id));
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente oppgaven", cut.Find("[role=alert]").TextContent));

        TaskRepository.IsFailing = false;
        cut.Find("[role=alert] button").Click();

        cut.WaitForElement("#title");
    }

    [Fact]
    public async Task Render_HasLinksBackToTheTask()
    {
        var task = await RegisterFullTaskAsync();

        var cut = RenderEdit(task.Id);

        Assert.Equal($"/oppgaver/{task.Id}", cut.Find("a.back-link").GetAttribute("href"));
        Assert.Equal($"/oppgaver/{task.Id}", cut.Find(".form-actions a").GetAttribute("href"));
    }
}
