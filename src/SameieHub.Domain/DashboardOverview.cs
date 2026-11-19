namespace SameieHub.Domain;

public enum UpcomingDateKind
{
    TaskDeadline,
    BoardMeeting
}

/// <summary>A future date to show on the dashboard: a task deadline or a board case meeting date.</summary>
public record UpcomingDate(DateOnly Date, UpcomingDateKind Kind, int Id, string Title);

/// <summary>
/// The numbers and lists shown on the dashboard, calculated from the existing data and today's Norwegian date.
/// Nothing here is stored.
/// </summary>
public record DashboardOverview
{
    public const int MaxFollowUpTasks = 8;
    public const int MaxBoardCases = 5;
    public const int MaxUpcomingDates = 8;

    /// <summary>Open tasks due within this many days after today are shown as follow-up tasks.</summary>
    public const int FollowUpDays = 7;

    /// <summary>Upcoming dates are shown this many days ahead.</summary>
    public const int UpcomingDays = 60;

    public int ActiveHousingUnits { get; init; }

    /// <summary>Board cases that are not closed.</summary>
    public int OpenBoardCases { get; init; }

    /// <summary>Tasks that are not completed.</summary>
    public int OpenTasks { get; init; }

    public int OverdueTasks { get; init; }

    public IReadOnlyList<TaskItem> FollowUpTasks { get; init; } = [];

    public IReadOnlyList<BoardCase> CurrentBoardCases { get; init; } = [];

    public IReadOnlyList<UpcomingDate> UpcomingDates { get; init; } = [];

    public static DashboardOverview Create(
        IEnumerable<HousingUnit> housingUnits,
        IEnumerable<BoardCase> boardCases,
        IEnumerable<TaskItem> tasks,
        DateOnly today)
    {
        var allCases = boardCases.ToList();
        var allTasks = tasks.ToList();

        var openCases = allCases.Where(boardCase => boardCase.Status != BoardCaseStatus.Closed).ToList();
        var openTasks = allTasks.Where(task => task.Status != TaskItemStatus.Completed).ToList();

        return new DashboardOverview
        {
            ActiveHousingUnits = housingUnits.Count(unit => unit.IsActive),
            OpenBoardCases = openCases.Count,
            OpenTasks = openTasks.Count,
            OverdueTasks = openTasks.Count(task => task.IsOverdue(today)),
            FollowUpTasks = FindFollowUpTasks(openTasks, today),
            CurrentBoardCases = FindCurrentBoardCases(openCases),
            UpcomingDates = FindUpcomingDates(openCases, openTasks, today)
        };
    }

    private static List<TaskItem> FindFollowUpTasks(List<TaskItem> openTasks, DateOnly today)
    {
        var lastDay = today.AddDays(FollowUpDays);
        var result = new List<TaskItem>();

        foreach (var task in openTasks)
        {
            if (task.DueDate != null && task.DueDate <= lastDay)
            {
                result.Add(task);
            }
        }

        return result
            .OrderBy(task => task.DueDate)
            .ThenByDescending(task => task.Priority)
            .ThenBy(task => task.Id)
            .Take(MaxFollowUpTasks)
            .ToList();
    }

    private static List<BoardCase> FindCurrentBoardCases(List<BoardCase> openCases)
    {
        return openCases
            .OrderByDescending(boardCase => boardCase.UpdatedAt)
            .ThenByDescending(boardCase => boardCase.Id)
            .Take(MaxBoardCases)
            .ToList();
    }

    /// <summary>
    /// Task deadlines inside the follow-up window are left out, because the follow-up list already shows them.
    /// </summary>
    private static List<UpcomingDate> FindUpcomingDates(List<BoardCase> openCases, List<TaskItem> openTasks, DateOnly today)
    {
        var firstTaskDay = today.AddDays(FollowUpDays + 1);
        var lastDay = today.AddDays(UpcomingDays);
        var result = new List<UpcomingDate>();

        foreach (var boardCase in openCases)
        {
            if (boardCase.MeetingDate is { } meetingDate && meetingDate >= today && meetingDate <= lastDay)
            {
                result.Add(new UpcomingDate(meetingDate, UpcomingDateKind.BoardMeeting, boardCase.Id, boardCase.Title));
            }
        }

        foreach (var task in openTasks)
        {
            if (task.DueDate is { } dueDate && dueDate >= firstTaskDay && dueDate <= lastDay)
            {
                result.Add(new UpcomingDate(dueDate, UpcomingDateKind.TaskDeadline, task.Id, task.Title));
            }
        }

        return result
            .OrderBy(date => date.Date)
            .ThenBy(date => date.Kind == UpcomingDateKind.TaskDeadline)
            .ThenBy(date => date.Id)
            .Take(MaxUpcomingDates)
            .ToList();
    }
}
