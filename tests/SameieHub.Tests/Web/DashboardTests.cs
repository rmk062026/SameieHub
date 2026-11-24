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
        cut.WaitForElement("[data-testid=status-summary], [data-testid=getting-started], [role=alert]");

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

    private static IReadOnlyList<string> FollowUpTitles(IRenderedComponent<Dashboard> cut)
    {
        return cut.FindAll("[data-testid=follow-up-item] a").Select(link => link.TextContent).ToList();
    }

    private Task<TaskItem> RegisterTaskAsync(string title, DateOnly? dueDate = null, string? assignedTo = null)
    {
        return Tasks.RegisterAsync(new TaskItemDetails { Title = title, DueDate = dueDate, AssignedTo = assignedTo });
    }

    private Task<BoardCase> RegisterCaseAsync(string title, DateOnly? meetingDate)
    {
        return BoardCases.RegisterAsync(new BoardCaseDetails { Title = title, MeetingDate = meetingDate });
    }

    // Empty database and first use

    [Fact]
    public void Render_EmptyDatabase_ShowsAGettingStartedGuideInsteadOfZeros()
    {
        var cut = RenderDashboard();

        var guide = cut.FindByTestId("getting-started");
        Assert.Contains("Kom i gang", guide.TextContent);
        var hrefs = guide.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList();
        Assert.Contains("/boenheter/ny", hrefs);
        Assert.Contains("/styresaker/ny", hrefs);
        Assert.Contains("/oppgaver/ny", hrefs);
        Assert.Contains("/innstillinger/sikkerhetskopi", hrefs);
        Assert.Empty(cut.FindAll(".stat-card"));
        Assert.False(cut.HasTestId("follow-up"));
    }

    [Fact]
    public void Render_EmptyDatabase_ExplainsTheDifferenceBetweenABoardCaseAndATask()
    {
        var cut = RenderDashboard();

        var text = cut.FindByTestId("getting-started").TextContent;
        Assert.Contains("noe styret skal behandle og eventuelt vedta", text);
        Assert.Contains("noe noen skal gjøre", text);
    }

    [Fact]
    public async Task Render_AfterTheFirstRegistration_TheGuideIsGoneAndTheOverviewIsShown()
    {
        await Service.RegisterAsync("Storgata 1A");

        var cut = RenderDashboard();

        Assert.False(cut.HasTestId("getting-started"));
        Assert.Equal("0", StatValue(cut, "Åpne oppgaver"));
        Assert.Contains("Ingen åpne oppgaver", cut.FindByTestId("follow-up").TextContent);
        Assert.Contains("Ingen åpne styresaker har en registrert møtedato", cut.FindByTestId("upcoming-cases").TextContent);
    }

    // Header

    [Fact]
    public void Render_ShowsTheTitleOversiktAndTodaysNorwegianDate()
    {
        var cut = RenderDashboard();

        Assert.Equal("Oversikt", cut.FindByTestId("page-title").TextContent);
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
        Assert.Contains("Forfalt", cut.FindByTestId("follow-up").TextContent);
    }

    // Status numbers

    [Fact]
    public async Task Status_ShowsExactlyThreeNumbers_InThePrioritisedOrder()
    {
        await Service.RegisterAsync("Storgata 1A");

        var cut = RenderDashboard();

        var labels = cut.FindAll(".stat-card .stat-label").Select(label => label.TextContent).ToList();
        Assert.Equal(["Forfalte oppgaver", "Åpne oppgaver", "Åpne styresaker"], labels);
    }

    [Fact]
    public async Task Status_CountsBoardCasesThatAreNotClosedAsOpen()
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
    public async Task Status_CountsOpenAndOverdueTasksLikeTheTaskList_AndIgnoresCompletedOnes()
    {
        await RegisterTaskAsync("Forfalt", Today.AddDays(-1));
        var inProgress = await RegisterTaskAsync("Pågår og forfalt", Today.AddDays(-3));
        await Tasks.ChangeStatusAsync(inProgress.Id, TaskItemStatus.InProgress);
        await RegisterTaskAsync("Frist i dag", Today);
        await RegisterTaskAsync("Uten frist");
        var completed = await RegisterTaskAsync("Fullført gammel", Today.AddDays(-30));
        await Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);

        var cut = RenderDashboard();

        Assert.Equal("4", StatValue(cut, "Åpne oppgaver"));
        Assert.Equal("2", StatValue(cut, "Forfalte oppgaver"));
        Assert.Single(cut.FindAll(".stat-alert"));
        var list = Render<SameieHub.Web.Components.Pages.TaskItems.TaskList>();
        list.WaitForElement("[data-testid=quick-tab-open]");
        Assert.Equal("Åpne 4", list.FindByTestId("quick-tab-open").TextContent.Trim());
        Assert.Equal("Forfalte 2", list.FindByTestId("quick-tab-overdue").TextContent.Trim());
    }

    [Fact]
    public async Task Status_NoOverdueTasks_HasNoAlertStyle()
    {
        await RegisterTaskAsync("Frist i dag", Today);

        var cut = RenderDashboard();

        Assert.Equal("0", StatValue(cut, "Forfalte oppgaver"));
        Assert.Empty(cut.FindAll(".stat-alert"));
    }

    [Fact]
    public async Task Status_NumbersLinkToTheMatchingPages()
    {
        await Service.RegisterAsync("Storgata 1A");

        var cut = RenderDashboard();

        Assert.Equal("/oppgaver?forfalt=1", StatHref(cut, "Forfalte oppgaver"));
        Assert.Equal("/oppgaver", StatHref(cut, "Åpne oppgaver"));
        Assert.Equal("/styresaker", StatHref(cut, "Åpne styresaker"));
    }

    // New

    [Fact]
    public void Render_HasOneNewActionWithLinksToTheThreeRegisterPages()
    {
        var cut = RenderDashboard();

        var primary = cut.FindByTestId("primary-action");
        Assert.Equal("+ Ny", primary.QuerySelector("summary")!.TextContent.Trim());
        var links = primary.QuerySelectorAll("a").ToDictionary(link => link.TextContent, link => link.GetAttribute("href"));
        Assert.Equal("/oppgaver/ny", links["Ny oppgave"]);
        Assert.Equal("/styresaker/ny", links["Ny styresak"]);
        Assert.Equal("/boenheter/ny", links["Ny boenhet"]);
        Assert.Equal(3, links.Count);
    }

    // Must be followed up

    [Fact]
    public async Task FollowUp_ShowsOverdueFirstThenTodayThenNearDeadlines_WithAssigneeDueDateAndLink()
    {
        await RegisterTaskAsync("Om tre dager", Today.AddDays(3), "Ola Eksempel");
        await RegisterTaskAsync("I dag", Today);
        var overdue = await RegisterTaskAsync("Forfalt", Today.AddDays(-2), "Lise Prøve");

        var cut = RenderDashboard();

        Assert.Equal(["Forfalt", "I dag", "Om tre dager"], FollowUpTitles(cut));
        var rows = cut.FindAll("[data-testid=follow-up-item]");
        Assert.Contains("16.11.2026", rows[0].TextContent);
        Assert.Equal("Forfalt", rows[0].QuerySelector(".is-overdue")!.TextContent);
        Assert.Equal("Lise Prøve", rows[0].QuerySelector("[data-testid=follow-up-assignee]")!.TextContent);
        Assert.Equal("Ikke tildelt", rows[1].QuerySelector("[data-testid=follow-up-assignee]")!.TextContent);
        Assert.Contains("Frist i dag", rows[1].QuerySelector(".is-due-today")!.TextContent);
        Assert.Equal($"/oppgaver/{overdue.Id}", rows[0].QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public async Task FollowUp_IncludesOverdueTasksThatAreInProgress()
    {
        var task = await RegisterTaskAsync("Pågår og forfalt", Today.AddDays(-4));
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);

        var cut = RenderDashboard();

        Assert.Equal(["Pågår og forfalt"], FollowUpTitles(cut));
    }

    [Fact]
    public async Task FollowUp_DoesNotShowCompletedTasksOrTasksWithoutNearDeadline()
    {
        var completed = await RegisterTaskAsync("Fullført forfalt", Today.AddDays(-5));
        await Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);
        await RegisterTaskAsync("Uten frist");
        await RegisterTaskAsync("Langt frem", Today.AddDays(40));

        var cut = RenderDashboard();

        Assert.Empty(FollowUpTitles(cut));
        Assert.Contains("Ingen oppgaver er forfalt eller har frist de neste 7 dagene", cut.FindByTestId("follow-up").TextContent);
    }

    [Fact]
    public async Task FollowUp_ShowsAtMostFiveTasks_AndSaysHowManyThereAre()
    {
        for (var number = 1; number <= 7; number++)
        {
            await RegisterTaskAsync($"Oppgave {number}", Today.AddDays(number));
        }

        var cut = RenderDashboard();

        Assert.Equal(5, FollowUpTitles(cut).Count);
        Assert.Equal(["Oppgave 1", "Oppgave 2", "Oppgave 3", "Oppgave 4", "Oppgave 5"], FollowUpTitles(cut));
        Assert.Contains("Viser 5 av 7 oppgaver som haster", cut.FindByTestId("follow-up-more").TextContent);
        Assert.Equal("7", StatValue(cut, "Åpne oppgaver"));
    }

    [Fact]
    public async Task FollowUp_ExactlyFiveTasks_HasNoMoreNote()
    {
        for (var number = 1; number <= 5; number++)
        {
            await RegisterTaskAsync($"Oppgave {number}", Today.AddDays(1));
        }

        var cut = RenderDashboard();

        Assert.Equal(5, FollowUpTitles(cut).Count);
        Assert.False(cut.HasTestId("follow-up-more"));
    }

    [Fact]
    public async Task FollowUp_LinksToTheFullTaskList_AndDoesNotRepeatATaskElsewhere()
    {
        await RegisterTaskAsync("Nær frist", Today.AddDays(2));

        var cut = RenderDashboard();

        Assert.Equal("/oppgaver", cut.FindByTestId("follow-up").QuerySelector("a.btn")!.GetAttribute("href"));
        Assert.Equal(1, cut.Markup.Split("Nær frist").Length - 1);
    }

    // Upcoming cases with a meeting date

    [Fact]
    public async Task Upcoming_ShowsOpenCasesWithAMeetingDate_NearestFirst_WithStatusAndLink()
    {
        var later = await RegisterCaseAsync("Maling av porter", new DateOnly(2026, 12, 3));
        var sooner = await RegisterCaseAsync("Årsmøte", new DateOnly(2026, 11, 26));
        await RegisterCaseAsync("Uten dato", null);

        var cut = RenderDashboard();

        var items = cut.FindAll("[data-testid=upcoming-case]");
        Assert.Equal(2, items.Count);
        Assert.Contains("26.11.2026", items[0].TextContent);
        Assert.Contains("Årsmøte", items[0].TextContent);
        Assert.Contains("Ny", items[0].QuerySelector(".status-badge")!.TextContent);
        Assert.Equal($"/styresaker/{sooner.Id}", items[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal($"/styresaker/{later.Id}", items[1].QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public async Task Upcoming_TodayIsIncluded_PastDatesAndClosedCasesAreNot()
    {
        await RegisterCaseAsync("I dag", Today);
        await RegisterCaseAsync("Møte i går", Today.AddDays(-1));
        var closed = await RegisterCaseAsync("Avsluttet møte", Today.AddDays(5));
        await BoardCases.ChangeStatusAsync(closed.Id, BoardCaseStatus.Closed);

        var cut = RenderDashboard();

        var titles = cut.FindAll("[data-testid=upcoming-case] a").Select(link => link.TextContent).ToList();
        Assert.Equal(["I dag"], titles);
    }

    [Fact]
    public async Task Upcoming_ShowsAtMostFiveCases()
    {
        for (var number = 1; number <= 8; number++)
        {
            await RegisterCaseAsync($"Sak {number}", Today.AddDays(number));
        }

        var cut = RenderDashboard();

        Assert.Equal(5, cut.FindAll("[data-testid=upcoming-case]").Count);
        Assert.Equal("8", StatValue(cut, "Åpne styresaker"));
    }

    [Fact]
    public async Task Upcoming_ExplainsThatADateIsNotAConfirmedMeeting_AndNeverCallsItTheNextBoardMeeting()
    {
        await RegisterCaseAsync("Årsmøte", new DateOnly(2026, 11, 26));

        var cut = RenderDashboard();

        Assert.Equal("Kommende saker med møtedato", cut.FindByTestId("upcoming-cases").QuerySelector("h2")!.TextContent);
        var note = cut.FindByTestId("meeting-date-note").TextContent;
        Assert.Contains("registrert på saken", note);
        Assert.Contains("ikke at et møte er bekreftet", note);
        Assert.DoesNotContain("Neste styremøte", cut.Markup);
        Assert.DoesNotContain("Styremøte", cut.FindByTestId("upcoming-cases").QuerySelector("ul")!.TextContent);
    }

    [Fact]
    public async Task Upcoming_LinksToTheBoardCaseList()
    {
        await RegisterCaseAsync("Årsmøte", new DateOnly(2026, 11, 26));

        var cut = RenderDashboard();

        Assert.Equal("/styresaker", cut.FindByTestId("upcoming-cases").QuerySelector("a.btn")!.GetAttribute("href"));
    }

    // Structure and accessibility

    [Fact]
    public async Task Structure_HasOneH1_AndEverySectionIsLabelledByItsHeading()
    {
        await Service.RegisterAsync("Storgata 1A");

        var cut = RenderDashboard();

        Assert.Single(cut.FindAll("h1"));
        foreach (var section in cut.FindAll("section[aria-labelledby]"))
        {
            var heading = cut.Find($"#{section.GetAttribute("aria-labelledby")}");
            Assert.Equal("H2", heading.TagName);
        }

        Assert.Equal(["Må følges opp", "Kommende saker med møtedato", "Elbillading"], cut.FindAll("section h2").Select(h => h.TextContent).ToList());
    }

    [Fact]
    public async Task Structure_ImportantInformationIsNeverInsideAClosedPanel()
    {
        await RegisterTaskAsync("Forfalt", Today.AddDays(-1));

        var cut = RenderDashboard();

        Assert.Empty(cut.FindAll("details details, [data-testid=follow-up] details, [data-testid=status-summary] details, [data-testid=upcoming-cases] details"));
        Assert.Single(cut.FindAll("details"));
    }

    // Errors and data

    [Theory]
    [InlineData("housing")]
    [InlineData("boardcase")]
    [InlineData("task")]
    public void Render_AnyDataSourceUnavailable_ShowsErrorWithoutNumbersAndRetryShowsTheOverview(string failing)
    {
        SetFailing(failing, true);
        var cut = Render<Dashboard>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente oversikten", cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll(".stat-card"));
        Assert.False(cut.HasTestId("getting-started"));

        SetFailing(failing, false);
        cut.Find("[role=alert] button").Click();

        cut.WaitForAssertion(() => Assert.True(cut.HasTestId("getting-started")));
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
