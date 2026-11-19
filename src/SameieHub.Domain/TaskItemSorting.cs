namespace SameieHub.Domain;

public enum TaskItemSortOrder
{
    DueDate,
    Newest,
    Oldest,
    Priority,
    Title
}

public static class TaskItemSorting
{
    private static readonly StringComparer TitleComparer =
        StringComparer.Create(new System.Globalization.CultureInfo("nb-NO"), ignoreCase: true);

    /// <summary>
    /// Returns a new sorted list. Ties are broken so the order is always the same.
    /// By due date, open tasks come first with the nearest due date first (so overdue tasks are at the top),
    /// then open tasks without a due date, and completed tasks last.
    /// </summary>
    public static List<TaskItem> Sort(IEnumerable<TaskItem> tasks, TaskItemSortOrder order)
    {
        var sorted = order switch
        {
            TaskItemSortOrder.Newest => tasks.OrderByDescending(task => task.CreatedAt).ThenByDescending(task => task.Id),
            TaskItemSortOrder.Oldest => tasks.OrderBy(task => task.CreatedAt).ThenBy(task => task.Id),
            TaskItemSortOrder.Priority => tasks
                .OrderByDescending(task => task.Priority)
                .ThenBy(task => task.DueDate == null)
                .ThenBy(task => task.DueDate)
                .ThenBy(task => task.Id),
            TaskItemSortOrder.Title => tasks.OrderBy(task => task.Title, TitleComparer).ThenBy(task => task.Id),
            _ => tasks
                .OrderBy(task => task.Status == TaskItemStatus.Completed)
                .ThenBy(task => task.DueDate == null)
                .ThenBy(task => task.DueDate)
                .ThenByDescending(task => task.CreatedAt)
                .ThenBy(task => task.Id)
        };

        return sorted.ToList();
    }
}
