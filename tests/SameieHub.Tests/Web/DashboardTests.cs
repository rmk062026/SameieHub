using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameieHub.Domain;
using SameieHub.Tests.Fakes;
using SameieHub.Web.Components.Pages;

namespace SameieHub.Tests.Web;

public class DashboardTests : UiTestBase
{
    // The test clock starts at 2026-11-18 10:30 UTC, which is 11:30 on 18 November in Norway.
    private static readonly DateOnly Today = new(2026, 11, 18);

    private IRenderedComponent<Dashboard> RenderDashboard()
    {
        var cut = Render<Dashboard>();
        cut.WaitForElement(".stat-row, [role=alert]");

        return cut;
    }

    private static string StatValue(IRenderedComponent<Dashboard> cut, string label)
    {
        var card = cut.FindAll(".stat-card").First(element => element.QuerySelector(".stat-label")!.TextContent == label);

        return card.QuerySelector(".stat-value")!.TextContent;
    }

    private static string StatHref(IRenderedComponent<Dashboard> cut, string label)
    {
        return cut.FindAll(".stat-card").First(element => element.QuerySelector(".stat-label")!.TextContent == label).GetAttribute("href")!;
    }

    private static AngleSharp.Dom.IElement Section(IRenderedComponent<Dashboard> cut, string heading)
    {
        return cut.FindAll("section").First(section => section.QuerySelector("h2")!.TextContent == heading);
    }

    private Task<TaskItem> RegisterTaskAsync(string title, DateOnly? dueDate = null, TaskItemPriority priority = TaskItemPriority.Normal)
    {
        return Tasks.RegisterAsync(new TaskItemDetails { Title = title, DueDate = dueDate, Priority = priority });
    }

    // Empty database

    [Fact]
    public void Render_EmptyDatabase_ShowsRealZerosAndFriendlyEmptyStates()
    {
        var cut = RenderDashboard();

        Assert.Equal("0", StatValue(cut, "Aktive boenheter"));
        Assert.Equal("0", StatValue(cut, "Åpne styresaker"));
        Assert.Equal("0", StatValue(cut, "Åpne oppgaver"));
        Assert.Equal("0", StatValue(cut, "Forfalte oppgaver"));
        Assert.Contains("Ingen åpne oppgaver", Section(cut, "Oppgaver som krever oppfølging").TextContent);
        Assert.Contains("Ingen åpne styresaker", Section(cut, "Aktuelle styresaker").TextContent);
        Assert.Contains("Ingen kommende møter eller frister", Section(cut, "Kommende datoer").TextContent);
        Assert.Empty(cut.FindAll("table"));
        Assert.Empty(cut.FindAll(".stat-alert"));
    }

    [Fact]
    public void Render_ShowsTodaysNorwegianDateInTheHeader()
    {
        var cut = RenderDashboard();

        Assert.Contains("per 18.11.2026", cut.Find(".page-header p").TextContent);
    }

    [Fact]
    public async Task Render_NearMidnight_UsesTheNorwegianDate()
    {
        // 23:30 UTC on 18 November is 00:30 on 19 November in Norway.
        Clock.Advance(TimeSpan.FromHours(13));
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Frist 18.", DueDate = Today });

        var cut = RenderDashboard();

        Assert.Contains("per 19.11.2026", cut.Find(".page-header p").TextContent);
        Assert.Equal("1", StatValue(cut, "Forfalte oppgaver"));
        Assert.Contains("Forfalt", Section(cut, "Oppgaver som krever oppfølging").TextContent);
    }

    // Counts

    [Fact]
    public async Task Render_CountsOnlyActiveHousingUnits()
    {
        await Service.RegisterAsync("Storgata 1A");
        await Service.RegisterAsync("Storgata 1B");
        var inactive = await Service.RegisterAsync("Storgata 1C");
        await Service.DeactivateAsync(inactive.Id);

        var cut = RenderDashboard();

        Assert.Equal("2", StatValue(cut, "Aktive boenheter"));
    }

    [Fact]
    public async Task Render_CountsBoardCasesThatAreNotClosedAsOpen()
    {
        await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Ny" });
        var inProgress = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Under behandling" });
        await BoardCases.ChangeStatusAsync(inProgress.Id, BoardCaseStatus.InProgress);
        var closed = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Avsluttet" });
        await BoardCases.ChangeStatusAsync(closed.Id, BoardCaseStatus.Closed);

        var cut = RenderDashboard();

        Assert.Equal("2", StatValue(cut, "Åpne styresaker"));
    }

    [Fact]
    public async Task Render_CountsOpenAndOverdueTasksAndIgnoresCompletedOnes()
    {
        await RegisterTaskAsync("Forfalt", Today.AddDays(-1));
        await RegisterTaskAsync("Frist i dag", Today);
        await RegisterTaskAsync("Uten frist");
        var completed = await RegisterTaskAsync("Fullført gammel", Today.AddDays(-30));
        await Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);

        var cut = RenderDashboard();

        Assert.Equal("3", StatValue(cut, "Åpne oppgaver"));
        Assert.Equal("1", StatValue(cut, "Forfalte oppgaver"));
        Assert.Single(cut.FindAll(".stat-alert"));
    }

    [Fact]
    public async Task Render_TaskDueTodayIsNotOverdueButShowsDueTodayInFollowUp()
    {
        await RegisterTaskAsync("Frist i dag", Today);

        var cut = RenderDashboard();

        Assert.Equal("0", StatValue(cut, "Forfalte oppgaver"));
        var section = Section(cut, "Oppgaver som krever oppfølging");
        Assert.Contains("Frist i dag", section.QuerySelector(".is-due-today")!.TextContent);
        Assert.Empty(section.QuerySelectorAll(".is-overdue"));
    }

    // Navigation

    [Fact]
    public void Render_StatisticsCardsLinkToTheModules()
    {
        var cut = RenderDashboard();

        Assert.Equal("/boenheter", StatHref(cut, "Aktive boenheter"));
        Assert.Equal("/styresaker", StatHref(cut, "Åpne styresaker"));
        Assert.Equal("/oppgaver", StatHref(cut, "Åpne oppgaver"));
        Assert.Equal("/oppgaver?forfalt=1", StatHref(cut, "Forfalte oppgaver"));
    }

    [Fact]
    public void Render_QuickActionsAreOrdinaryLinksToTheRegisterPages()
    {
        var cut = RenderDashboard();

        var links = Section(cut, "Hurtighandlinger").QuerySelectorAll("a").ToDictionary(link => link.TextContent, link => link.GetAttribute("href"));
        Assert.Equal("/boenheter/ny", links["Registrer boenhet"]);
        Assert.Equal("/styresaker/ny", links["Registrer styresak"]);
        Assert.Equal("/oppgaver/ny", links["Registrer oppgave"]);
    }

    [Fact]
    public void Render_SectionsLinkToTheFullLists()
    {
        var cut = RenderDashboard();

        Assert.Equal("/oppgaver", Section(cut, "Oppgaver som krever oppfølging").QuerySelector("a.btn")!.GetAttribute("href"));
        Assert.Equal("/styresaker", Section(cut, "Aktuelle styresaker").QuerySelector("a.btn")!.GetAttribute("href"));
    }

    // Follow-up tasks

    [Fact]
    public async Task FollowUp_ShowsOverdueFirstThenTodayThenNearDeadlinesWithClickableTitles()
    {
        await RegisterTaskAsync("Om tre dager", Today.AddDays(3));
        await RegisterTaskAsync("I dag", Today);
        var overdue = await RegisterTaskAsync("Forfalt", Today.AddDays(-2), TaskItemPriority.High);

        var cut = RenderDashboard();

        var rows = Section(cut, "Oppgaver som krever oppfølging").QuerySelectorAll("tbody tr").ToList();
        Assert.Equal(3, rows.Count);
        Assert.Contains("Forfalt", rows[0].TextContent);
        Assert.Contains("I dag", rows[1].TextContent);
        Assert.Contains("Om tre dager", rows[2].TextContent);
        Assert.Contains("16.11.2026", rows[0].TextContent);
        Assert.Contains("Høy", rows[0].TextContent);
        Assert.Contains("Ikke startet", rows[0].TextContent);
        Assert.Equal($"/oppgaver/{overdue.Id}", rows[0].QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public async Task FollowUp_DoesNotShowCompletedTasksOrTasksWithoutNearDeadline()
    {
        var completed = await RegisterTaskAsync("Fullført forfalt", Today.AddDays(-5));
        await Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);
        await RegisterTaskAsync("Uten frist");
        await RegisterTaskAsync("Langt frem", Today.AddDays(40));

        var cut = RenderDashboard();

        var section = Section(cut, "Oppgaver som krever oppfølging");
        Assert.Empty(section.QuerySelectorAll("table"));
        Assert.Contains("Ingen oppgaver er forfalt eller har frist de neste 7 dagene", section.TextContent);
    }

    [Fact]
    public async Task FollowUp_IsLimitedToTheMostRelevantTasks()
    {
        for (var number = 1; number <= 12; number++)
        {
            await RegisterTaskAsync($"Oppgave {number}", Today.AddDays(1));
        }

        var cut = RenderDashboard();

        Assert.Equal(DashboardOverview.MaxFollowUpTasks, Section(cut, "Oppgaver som krever oppfølging").QuerySelectorAll("tbody tr").Length);
        Assert.Equal("12", StatValue(cut, "Åpne oppgaver"));
    }

    // Board cases

    [Fact]
    public async Task BoardCases_ShowsOpenCasesWithNumberTitleStatusPriorityAndMeetingDate()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var withMeeting = await BoardCases.RegisterAsync(new BoardCaseDetails
        {
            Title = "Maling av porter",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(2026, 12, 3),
            HousingUnitId = unit.Id
        });
        await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Uten møte" });
        var closed = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Avsluttet sak" });
        await BoardCases.ChangeStatusAsync(closed.Id, BoardCaseStatus.Closed);

        var cut = RenderDashboard();

        var section = Section(cut, "Aktuelle styresaker");
        var rows = section.QuerySelectorAll("tbody tr").ToList();
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain("Avsluttet sak", section.TextContent);
        var meetingRow = rows.First(row => row.TextContent.Contains("Maling av porter"));
        Assert.Contains($"#{withMeeting.Id}", meetingRow.TextContent);
        Assert.Contains("Ny", meetingRow.TextContent);
        Assert.Contains("Høy", meetingRow.TextContent);
        Assert.Contains("03.12.2026", meetingRow.TextContent);
        Assert.Equal($"/styresaker/{withMeeting.Id}", meetingRow.QuerySelector("a")!.GetAttribute("href"));
        Assert.Contains("Ingen møtedato", rows.First(row => row.TextContent.Contains("Uten møte")).TextContent);
    }

    [Fact]
    public async Task BoardCases_ShowsLatestUpdatedFirstAndIsLimited()
    {
        for (var number = 1; number <= 7; number++)
        {
            await BoardCases.RegisterAsync(new BoardCaseDetails { Title = $"Sak {(char)('A' + number)}" });
            Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var cut = RenderDashboard();

        var titles = Section(cut, "Aktuelle styresaker").QuerySelectorAll("tbody a").Select(link => link.TextContent).ToList();
        Assert.Equal(DashboardOverview.MaxBoardCases, titles.Count);
        Assert.Equal("Sak H", titles[0]);
        Assert.Equal("7", StatValue(cut, "Åpne styresaker"));
    }

    // Upcoming dates

    [Fact]
    public async Task Upcoming_ShowsMeetingsAndFarDeadlinesWithLinksAndNorwegianDates()
    {
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Årsmøte", MeetingDate = new DateOnly(2026, 12, 3) });
        var task = await RegisterTaskAsync("Rense takrenner", new DateOnly(2026, 12, 20));

        var cut = RenderDashboard();

        var items = Section(cut, "Kommende datoer").QuerySelectorAll("li").ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains("03.12.2026", items[0].TextContent);
        Assert.Contains("Styremøte", items[0].TextContent);
        Assert.Equal($"/styresaker/{boardCase.Id}", items[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Contains("20.12.2026", items[1].TextContent);
        Assert.Contains("Frist", items[1].TextContent);
        Assert.Equal($"/oppgaver/{task.Id}", items[1].QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public async Task Upcoming_DoesNotRepeatTasksThatAreAlreadyInTheFollowUpList()
    {
        await RegisterTaskAsync("Nær frist", Today.AddDays(2));

        var cut = RenderDashboard();

        Assert.Contains("Nær frist", Section(cut, "Oppgaver som krever oppfølging").TextContent);
        Assert.DoesNotContain("Nær frist", Section(cut, "Kommende datoer").TextContent);
    }

    [Fact]
    public async Task Upcoming_PastMeetingsAndClosedCasesAreNotShown()
    {
        await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Møte i går", MeetingDate = Today.AddDays(-1) });
        var closed = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Avsluttet møte", MeetingDate = Today.AddDays(5) });
        await BoardCases.ChangeStatusAsync(closed.Id, BoardCaseStatus.Closed);

        var cut = RenderDashboard();

        Assert.Empty(Section(cut, "Kommende datoer").QuerySelectorAll("li"));
    }

    // Errors and refresh

    [Theory]
    [InlineData("housing")]
    [InlineData("boardcase")]
    [InlineData("task")]
    public void Render_AnyDataSourceUnavailable_ShowsErrorWithoutNumbersAndRetryShowsTheDashboard(string failing)
    {
        SetFailing(failing, true);
        var cut = Render<Dashboard>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente oversikten", cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll(".stat-card"));

        SetFailing(failing, false);
        cut.Find("[role=alert] button").Click();

        cut.WaitForAssertion(() => Assert.Equal("0", StatValue(cut, "Aktive boenheter")));
    }

    private void SetFailing(string source, bool value)
    {
        if (source == "housing")
        {
            Repository.IsFailing = value;
        }
        else if (source == "boardcase")
        {
            BoardCaseRepository.IsFailing = value;
        }
        else
        {
            TaskRepository.IsFailing = value;
        }
    }

    [Fact]
    public async Task Refresh_ShowsNewNumbersAfterChangesInTheOtherModules()
    {
        var cut = RenderDashboard();
        Assert.Equal("0", StatValue(cut, "Åpne oppgaver"));

        await Service.RegisterAsync("Storgata 1A");
        await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling" });
        var task = await RegisterTaskAsync("Forfalt", Today.AddDays(-1));
        cut.FindAll("button").First(button => button.TextContent == "Oppdater oversikten").Click();

        cut.WaitForAssertion(() => Assert.Equal("1", StatValue(cut, "Åpne oppgaver")));
        Assert.Equal("1", StatValue(cut, "Aktive boenheter"));
        Assert.Equal("1", StatValue(cut, "Åpne styresaker"));
        Assert.Equal("1", StatValue(cut, "Forfalte oppgaver"));

        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        cut.FindAll("button").First(button => button.TextContent == "Oppdater oversikten").Click();

        cut.WaitForAssertion(() => Assert.Equal("0", StatValue(cut, "Åpne oppgaver")));
        Assert.Equal("0", StatValue(cut, "Forfalte oppgaver"));
    }

    [Fact]
    public async Task Render_DoesNotChangeAnyData()
    {
        var task = await RegisterTaskAsync("Oppgave", Today.AddDays(-1));
        var updatedAt = task.UpdatedAt;
        Clock.Advance(TimeSpan.FromHours(2));

        RenderDashboard();

        var stored = (await Tasks.GetByIdAsync(task.Id))!;
        Assert.Equal(updatedAt, stored.UpdatedAt);
        Assert.Equal(TaskItemStatus.NotStarted, stored.Status);
    }

    [Fact]
    public async Task Render_DatabaseFailure_DoesNotLogTextFromTheData()
    {
        var logger = new CapturingLoggerProvider();
        Services.AddLogging(builder => builder.AddProvider(logger));
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Hemmelig tittel", Notes = "Hemmelige merknader" });
        TaskRepository.IsFailing = true;

        var cut = Render<Dashboard>();
        cut.WaitForElement("[role=alert]");

        Assert.NotEmpty(logger.Messages);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("Hemmelig"));
    }
}
