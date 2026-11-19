using SameieHub.Domain;
using SameieHub.Web.Components.Shared;

namespace SameieHub.Web.Components.Pages.TaskItems;

public static class TaskText
{
    public static readonly TaskItemStatus[] AllStatuses = Enum.GetValues<TaskItemStatus>();

    public static readonly TaskItemCategory[] AllCategories = Enum.GetValues<TaskItemCategory>();

    public static readonly TaskItemPriority[] AllPriorities = Enum.GetValues<TaskItemPriority>();

    public static string StatusText(TaskItemStatus status)
    {
        return status switch
        {
            TaskItemStatus.NotStarted => "Ikke startet",
            TaskItemStatus.InProgress => "Pågår",
            _ => "Fullført"
        };
    }

    public static string CategoryText(TaskItemCategory category)
    {
        return category == TaskItemCategory.Maintenance ? "Vedlikehold" : "Generell";
    }

    public static string PriorityText(TaskItemPriority priority)
    {
        return priority switch
        {
            TaskItemPriority.Low => "Lav",
            TaskItemPriority.Normal => "Normal",
            _ => "Høy"
        };
    }

    public static string SortOrderText(TaskItemSortOrder order)
    {
        return order switch
        {
            TaskItemSortOrder.Newest => "Nyeste først",
            TaskItemSortOrder.Oldest => "Eldste først",
            TaskItemSortOrder.Priority => "Prioritet",
            TaskItemSortOrder.Title => "Tittel",
            _ => "Frist (nærmeste først)"
        };
    }

    public static string StatusCssClass(TaskItemStatus status)
    {
        return status switch
        {
            TaskItemStatus.NotStarted => "status-new",
            TaskItemStatus.InProgress => "status-in-progress",
            _ => "status-decided"
        };
    }

    public static string PriorityCssClass(TaskItemPriority priority)
    {
        return priority switch
        {
            TaskItemPriority.Low => "priority-low",
            TaskItemPriority.Normal => "priority-normal",
            _ => "priority-high"
        };
    }
}
