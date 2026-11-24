using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.BoardCases;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class TaskSectionOnOtherPagesTests : UiTestBase
{
    // The test clock starts at 2026-11-18 10:30 UTC, which is 11:30 on 18 November in Norway.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private IRenderedComponent<BoardCaseDetail> RenderBoardCase(int id)
    {
        var cut = Render<BoardCaseDetail>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement("[data-testid=case-summary]");

        return cut;
    }

    private IRenderedComponent<HousingUnitDetail> RenderUnit(int id)
    {
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement(".detail-grid");

        return cut;
    }

    private static AngleSharp.Dom.IElement TasksSection<T>(IRenderedComponent<T> cut) where T : Microsoft.AspNetCore.Components.IComponent
    {
        return cut.FindAll("section").First(section => section.QuerySelector("h2")?.TextContent == "Oppgaver");
    }

    // Board case page

    [Fact]
    public async Task BoardCase_WithoutTasks_ShowsEmptyTextAndRegisterShortcut()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });

        var cut = RenderBoardCase(boardCase.Id);

        var section = cut.FindByTestId("case-tasks");
        Assert.Contains("Ingen oppgaver er knyttet til denne saken", section.TextContent);
        Assert.Equal($"/oppgaver/ny?styresak={boardCase.Id}", section.QuerySelector("a.btn")!.GetAttribute("href"));
        Assert.Empty(section.QuerySelectorAll("li"));
    }

    [Fact]
    public async Task BoardCase_WithTasks_ShowsNumberTitleStatusAndDueDateOfOnlyItsOwnTasks()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var otherCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Annen sak" });
        var linked = await Tasks.RegisterAsync(new TaskItemDetails
        {
            Title = "Kjøpe maling",
            BoardCaseId = boardCase.Id,
            DueDate = Today.AddDays(-2)
        });
        await Tasks.ChangeStatusAsync(linked.Id, TaskItemStatus.InProgress);
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Annen oppgave", BoardCaseId = otherCase.Id });
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Uten sak" });

        var cut = RenderBoardCase(boardCase.Id);

        var row = Assert.Single(cut.FindByTestId("case-tasks").QuerySelectorAll("[data-testid=case-task]"));
        Assert.Contains("Kjøpe maling", row.TextContent);
        Assert.Contains("Pågår", row.TextContent);
        Assert.Contains("16.11.2026", row.TextContent);
        Assert.Contains("Forfalt", row.TextContent);
        Assert.Equal($"/oppgaver/{linked.Id}", row.QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public async Task BoardCase_ClosedCase_StillOffersTheRegisterShortcutAndShowsCompletedTasks()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);
        var task = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Ferdig oppgave", BoardCaseId = boardCase.Id });
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var cut = RenderBoardCase(boardCase.Id);

        var section = cut.FindByTestId("case-tasks");
        Assert.Contains("Ferdig oppgave", section.TextContent);
        Assert.NotNull(section.QuerySelector("a.btn"));
    }

    [Fact]
    public async Task BoardCase_TasksUnavailable_ShowsLoadError()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        TaskRepository.IsFailing = true;

        var cut = Render<BoardCaseDetail>(parameters => parameters.Add(page => page.Id, boardCase.Id));

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente styresaken", cut.Find("[role=alert]").TextContent));
    }

    // Housing unit page

    [Fact]
    public async Task HousingUnit_ActiveWithoutTasks_ShowsEmptyTextAndRegisterShortcut()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = RenderUnit(unit.Id);

        var section = TasksSection(cut);
        Assert.Contains("Ingen oppgaver er knyttet til denne boenheten", section.TextContent);
        Assert.Equal($"/oppgaver/ny?boenhet={unit.Id}", section.QuerySelector("a.btn")!.GetAttribute("href"));
    }

    [Fact]
    public async Task HousingUnit_WithTasks_ShowsOnlyItsOwnTasks()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var other = await Service.RegisterAsync("Storgata 1B");
        var linked = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Rense takrenner", HousingUnitId = unit.Id, DueDate = Today });
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Annen enhet", HousingUnitId = other.Id });

        var cut = RenderUnit(unit.Id);

        var row = Assert.Single(TasksSection(cut).QuerySelectorAll("tbody tr"));
        Assert.Contains($"#{linked.Id}", row.TextContent);
        Assert.Contains("Rense takrenner", row.TextContent);
        Assert.Contains("Ikke startet", row.TextContent);
        Assert.Contains("Frist i dag", row.TextContent);
    }

    [Fact]
    public async Task HousingUnit_Inactive_ShowsExistingTasksButOffersNoRegisterShortcut()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Rense takrenner", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = RenderUnit(unit.Id);

        var section = TasksSection(cut);
        Assert.Contains("Rense takrenner", section.TextContent);
        Assert.Empty(section.QuerySelectorAll("a.btn"));
    }

    [Fact]
    public async Task HousingUnit_DeleteText_StillWarnsAboutTasks()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = RenderUnit(unit.Id);

        cut.FindAll("button").First(button => button.TextContent.Contains("Slett boenheten")).Click();

        Assert.Contains("oppgaver", cut.Find("[role=alertdialog]").TextContent);
    }
}
