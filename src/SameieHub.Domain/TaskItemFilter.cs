namespace SameieHub.Domain;

public record TaskItemFilterCriteria
{
    /// <summary>Matched, ignoring case, against title, description and assigned person, and exactly against the task number.</summary>
    public string? SearchText { get; init; }

    public TaskItemStatus? Status { get; init; }

    /// <summary>Hides completed tasks. Ignored when <see cref="Status"/> is set.</summary>
    public bool OpenOnly { get; init; }

    public TaskItemCategory? Category { get; init; }

    public TaskItemPriority? Priority { get; init; }

    public int? HousingUnitId { get; init; }

    public int? BoardCaseId { get; init; }

    /// <summary>Only tasks that are overdue on this date. The caller passes today's Norwegian date.</summary>
    public DateOnly? OverdueOn { get; init; }
}

public static class TaskItemFilter
{
    public static List<TaskItem> Apply(IEnumerable<TaskItem> tasks, TaskItemFilterCriteria criteria)
    {
        var searchText = criteria.SearchText?.Trim();
        var result = new List<TaskItem>();

        foreach (var task in tasks)
        {
            if (MatchesFilters(task, criteria) && MatchesText(task, searchText))
            {
                result.Add(task);
            }
        }

        return result;
    }

    private static bool MatchesFilters(TaskItem task, TaskItemFilterCriteria criteria)
    {
        if (criteria.Status != null && task.Status != criteria.Status)
        {
            return false;
        }

        if (criteria.Status == null && criteria.OpenOnly && task.Status == TaskItemStatus.Completed)
        {
            return false;
        }

        if (criteria.Category != null && task.Category != criteria.Category)
        {
            return false;
        }

        if (criteria.Priority != null && task.Priority != criteria.Priority)
        {
            return false;
        }

        if (criteria.HousingUnitId != null && task.HousingUnitId != criteria.HousingUnitId)
        {
            return false;
        }

        if (criteria.BoardCaseId != null && task.BoardCaseId != criteria.BoardCaseId)
        {
            return false;
        }

        if (criteria.OverdueOn != null && !task.IsOverdue(criteria.OverdueOn.Value))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesText(TaskItem task, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var isTaskNumber = int.TryParse(text.TrimStart('#'), out var taskNumber) && taskNumber == task.Id;

        return isTaskNumber
            || Contains(task.Title, text)
            || Contains(task.Description, text)
            || Contains(task.AssignedTo, text);
    }

    private static bool Contains(string? value, string text)
    {
        return value != null && value.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }
}
