using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class DashboardOverviewTests
{
    private static readonly DateTime Start = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 18);

    private int _nextUnitId = 1;
    private int _nextCaseId = 1;
    private int _nextTaskId = 1;

    private HousingUnit NewUnit(bool active = true)
    {
        var unit = new HousingUnit(new HousingUnitDetails { Address = $"Storgata {_nextUnitId}" });
        unit.AssignId(_nextUnitId++);
        if (!active)
        {
            unit.Deactivate();
        }

        return unit;
    }

    private BoardCase NewCase(
        string title = "Sak",
        BoardCaseStatus status = BoardCaseStatus.New,
        int hoursAfterStart = 0,
        DateOnly? meetingDate = null)
    {
        var boardCase = new BoardCase(new BoardCaseDetails { Title = title, MeetingDate = meetingDate }, Start.AddHours(hoursAfterStart));
        boardCase.AssignId(_nextCaseId++);
        if (status == BoardCaseStatus.Decided)
        {
            boardCase.RegisterDecision("Fiktivt vedtak.", null, Start.AddHours(hoursAfterStart));
        }

        boardCase.ChangeStatus(status, Start.AddHours(hoursAfterStart));

        return boardCase;
    }

    private TaskItem NewTask(
        string title = "Oppgave",
        DateOnly? dueDate = null,
        TaskItemStatus status = TaskItemStatus.NotStarted,
        TaskItemPriority priority = TaskItemPriority.Normal,
        int hoursAfterStart = 0)
    {
        var task = new TaskItem(new TaskItemDetails { Title = title, DueDate = dueDate, Priority = priority }, Start.AddHours(hoursAfterStart));
        task.AssignId(_nextTaskId++);
        task.ChangeStatus(status, Start.AddHours(hoursAfterStart));

        return task;
    }

    private static DashboardOverview Create(
        IEnumerable<HousingUnit>? units = null,
        IEnumerable<BoardCase>? cases = null,
        IEnumerable<TaskItem>? tasks = null,
        DateOnly? today = null)
    {
        return DashboardOverview.Create(units ?? [], cases ?? [], tasks ?? [], today ?? Today);
    }

    private static List<string> Titles(IEnumerable<TaskItem> tasks) => tasks.Select(task => task.Title).ToList();

    // Counts

    [Fact]
    public void Create_NoData_GivesZeroCountsAndEmptyLists()
    {
        var overview = Create();

        Assert.Equal(0, overview.ActiveHousingUnits);
        Assert.Equal(0, overview.OpenBoardCases);
        Assert.Equal(0, overview.OpenTasks);
        Assert.Equal(0, overview.OverdueTasks);
        Assert.Empty(overview.FollowUpTasks);
        Assert.Empty(overview.CurrentBoardCases);
        Assert.Empty(overview.UpcomingDates);
    }

    [Fact]
    public void Create_CountsOnlyActiveHousingUnits()
    {
        var overview = Create(units: [NewUnit(), NewUnit(), NewUnit(active: false)]);

        Assert.Equal(2, overview.ActiveHousingUnits);
    }

    [Fact]
    public void Create_CountsAllBoardCaseStatusesExceptClosedAsOpen()
    {
        var cases = new[]
        {
            NewCase(status: BoardCaseStatus.New),
            NewCase(status: BoardCaseStatus.InProgress),
            NewCase(status: BoardCaseStatus.Decided),
            NewCase(status: BoardCaseStatus.Closed)
        };

        Assert.Equal(3, Create(cases: cases).OpenBoardCases);
    }

    [Fact]
    public void Create_CountsOnlyTasksThatAreNotCompletedAsOpen()
    {
        var tasks = new[]
        {
            NewTask(status: TaskItemStatus.NotStarted),
            NewTask(status: TaskItemStatus.InProgress),
            NewTask(status: TaskItemStatus.Completed)
        };

        Assert.Equal(2, Create(tasks: tasks).OpenTasks);
    }

    [Fact]
    public void Create_OverdueIsDueBeforeTodayAndNotCompleted()
    {
        var tasks = new[]
        {
            NewTask("Forfalt", Today.AddDays(-1)),
            NewTask("I dag", Today),
            NewTask("Fremtid", Today.AddDays(1)),
            NewTask("Uten frist"),
            NewTask("Fullført gammel", Today.AddDays(-30), TaskItemStatus.Completed)
        };

        var overview = Create(tasks: tasks);

        Assert.Equal(1, overview.OverdueTasks);
        Assert.Equal(4, overview.OpenTasks);
    }

    [Fact]
    public void Create_ChangingToday_ChangesWhichTasksAreOverdue()
    {
        var tasks = new[] { NewTask("Frist 18.", new DateOnly(2026, 11, 18)) };

        Assert.Equal(0, Create(tasks: tasks, today: new DateOnly(2026, 11, 18)).OverdueTasks);
        Assert.Equal(1, Create(tasks: tasks, today: new DateOnly(2026, 11, 19)).OverdueTasks);
    }

    // Follow-up tasks

    [Fact]
    public void FollowUp_OrdersOverdueThenDueTodayThenNearDeadlines()
    {
        var tasks = new[]
        {
            NewTask("Om tre dager", Today.AddDays(3)),
            NewTask("I dag", Today),
            NewTask("Forfalt nylig", Today.AddDays(-1)),
            NewTask("Forfalt lenge", Today.AddDays(-20)),
            NewTask("I morgen", Today.AddDays(1))
        };

        var overview = Create(tasks: tasks);

        Assert.Equal(["Forfalt lenge", "Forfalt nylig", "I dag", "I morgen", "Om tre dager"], Titles(overview.FollowUpTasks));
    }

    [Fact]
    public void FollowUp_ExcludesCompletedTasksTasksWithoutDueDateAndTasksBeyondTheWindow()
    {
        var tasks = new[]
        {
            NewTask("Fullført forfalt", Today.AddDays(-5), TaskItemStatus.Completed),
            NewTask("Uten frist"),
            NewTask("Langt frem", Today.AddDays(DashboardOverview.FollowUpDays + 1)),
            NewTask("Siste dag i vinduet", Today.AddDays(DashboardOverview.FollowUpDays))
        };

        var overview = Create(tasks: tasks);

        Assert.Equal(["Siste dag i vinduet"], Titles(overview.FollowUpTasks));
    }

    [Fact]
    public void FollowUp_SameDueDate_HigherPriorityFirstThenLowestId()
    {
        var tasks = new[]
        {
            NewTask("Normal først registrert", Today),
            NewTask("Høy", Today, priority: TaskItemPriority.High),
            NewTask("Normal sist registrert", Today)
        };

        var overview = Create(tasks: tasks);

        Assert.Equal(["Høy", "Normal først registrert", "Normal sist registrert"], Titles(overview.FollowUpTasks));
    }

    [Fact]
    public void FollowUp_IsLimited_AndOverdueTasksWinTheLimitedPlaces()
    {
        var tasks = Enumerable.Range(1, 10).Select(number => NewTask($"Fremtid {number}", Today.AddDays(1))).ToList();
        tasks.Add(NewTask("Forfalt", Today.AddDays(-1)));

        var overview = Create(tasks: tasks);

        Assert.Equal(DashboardOverview.MaxFollowUpTasks, overview.FollowUpTasks.Count);
        Assert.Equal("Forfalt", overview.FollowUpTasks[0].Title);
        Assert.Equal(11, overview.OpenTasks);
    }

    // Board cases

    [Fact]
    public void CurrentBoardCases_ExcludesClosedAndOrdersByLatestUpdate()
    {
        var cases = new[]
        {
            NewCase("Eldst oppdatert", hoursAfterStart: 0),
            NewCase("Avsluttet", BoardCaseStatus.Closed, hoursAfterStart: 5),
            NewCase("Nyest oppdatert", BoardCaseStatus.InProgress, hoursAfterStart: 3),
            NewCase("Vedtatt", BoardCaseStatus.Decided, hoursAfterStart: 2)
        };

        var overview = Create(cases: cases);

        Assert.Equal(["Nyest oppdatert", "Vedtatt", "Eldst oppdatert"], overview.CurrentBoardCases.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public void CurrentBoardCases_IsLimited()
    {
        var cases = Enumerable.Range(0, 9).Select(number => NewCase($"Sak {number}", hoursAfterStart: number)).ToList();

        var overview = Create(cases: cases);

        Assert.Equal(DashboardOverview.MaxBoardCases, overview.CurrentBoardCases.Count);
        Assert.Equal(9, overview.OpenBoardCases);
        Assert.Equal("Sak 8", overview.CurrentBoardCases[0].Title);
    }

    // Upcoming dates

    [Fact]
    public void UpcomingDates_ContainsMeetingDatesFromTodayOnAndSkipsPastMeetingsAndClosedCases()
    {
        var cases = new[]
        {
            NewCase("Møte i dag", meetingDate: Today),
            NewCase("Møte om en uke", meetingDate: Today.AddDays(7)),
            NewCase("Møte i går", meetingDate: Today.AddDays(-1)),
            NewCase("Avsluttet med møte", BoardCaseStatus.Closed, meetingDate: Today.AddDays(2)),
            NewCase("Uten møtedato")
        };

        var overview = Create(cases: cases);

        Assert.Equal(["Møte i dag", "Møte om en uke"], overview.UpcomingDates.Select(date => date.Title));
        Assert.All(overview.UpcomingDates, date => Assert.Equal(UpcomingDateKind.BoardMeeting, date.Kind));
    }

    [Fact]
    public void UpcomingDates_ContainsDeadlinesAfterTheFollowUpWindowWithoutRepeatingFollowUpTasks()
    {
        var tasks = new[]
        {
            NewTask("Nær frist", Today.AddDays(DashboardOverview.FollowUpDays)),
            NewTask("Fjern frist", Today.AddDays(DashboardOverview.FollowUpDays + 1)),
            NewTask("Forfalt", Today.AddDays(-1)),
            NewTask("Fullført fjern frist", Today.AddDays(20), TaskItemStatus.Completed)
        };

        var overview = Create(tasks: tasks);

        Assert.Equal(["Forfalt", "Nær frist"], Titles(overview.FollowUpTasks));
        var upcoming = Assert.Single(overview.UpcomingDates);
        Assert.Equal("Fjern frist", upcoming.Title);
        Assert.Equal(UpcomingDateKind.TaskDeadline, upcoming.Kind);
    }

    [Fact]
    public void UpcomingDates_StopsAfterTheUpcomingWindow()
    {
        var tasks = new[]
        {
            NewTask("Innenfor", Today.AddDays(DashboardOverview.UpcomingDays)),
            NewTask("Utenfor", Today.AddDays(DashboardOverview.UpcomingDays + 1))
        };
        var cases = new[] { NewCase("Møte utenfor", meetingDate: Today.AddDays(DashboardOverview.UpcomingDays + 1)) };

        var overview = Create(cases: cases, tasks: tasks);

        Assert.Equal(["Innenfor"], overview.UpcomingDates.Select(date => date.Title));
    }

    [Fact]
    public void UpcomingDates_AreSortedByDateAndMixTasksAndMeetings()
    {
        var tasks = new[] { NewTask("Frist dag 30", Today.AddDays(30)), NewTask("Frist dag 12", Today.AddDays(12)) };
        var cases = new[] { NewCase("Møte dag 20", meetingDate: Today.AddDays(20)) };

        var overview = Create(cases: cases, tasks: tasks);

        Assert.Equal(["Frist dag 12", "Møte dag 20", "Frist dag 30"], overview.UpcomingDates.Select(date => date.Title));
        Assert.Equal(
            [UpcomingDateKind.TaskDeadline, UpcomingDateKind.BoardMeeting, UpcomingDateKind.TaskDeadline],
            overview.UpcomingDates.Select(date => date.Kind));
    }

    [Fact]
    public void UpcomingDates_SameDay_MeetingBeforeDeadlineAndLowerIdFirst()
    {
        var day = Today.AddDays(15);
        var tasks = new[] { NewTask("Frist", day) };
        var cases = new[] { NewCase("Møte", meetingDate: day) };

        var overview = Create(cases: cases, tasks: tasks);

        Assert.Equal(["Møte", "Frist"], overview.UpcomingDates.Select(date => date.Title));
    }

    [Fact]
    public void UpcomingDates_AreLimited()
    {
        var tasks = Enumerable.Range(0, 12).Select(number => NewTask($"Frist {number}", Today.AddDays(10 + number))).ToList();

        var overview = Create(tasks: tasks);

        Assert.Equal(DashboardOverview.MaxUpcomingDates, overview.UpcomingDates.Count);
        Assert.Equal("Frist 0", overview.UpcomingDates[0].Title);
    }

    [Fact]
    public void UpcomingDates_CarryTheIdOfTheTaskOrCase()
    {
        var task = NewTask("Frist", Today.AddDays(20));
        var boardCase = NewCase("Møte", meetingDate: Today.AddDays(21));

        var overview = Create(cases: [boardCase], tasks: [task]);

        Assert.Equal([task.Id, boardCase.Id], overview.UpcomingDates.Select(date => date.Id));
    }

    [Fact]
    public void Create_DoesNotChangeTheInput()
    {
        var tasks = new List<TaskItem> { NewTask("B", Today.AddDays(2)), NewTask("A", Today.AddDays(-2)) };

        Create(tasks: tasks);

        Assert.Equal(["B", "A"], Titles(tasks));
    }
}
