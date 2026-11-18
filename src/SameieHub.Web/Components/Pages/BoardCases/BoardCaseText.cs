using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.BoardCases;

public static class BoardCaseText
{
    public static readonly BoardCaseStatus[] AllStatuses = Enum.GetValues<BoardCaseStatus>();

    public static readonly BoardCasePriority[] AllPriorities = Enum.GetValues<BoardCasePriority>();

    public static string StatusText(BoardCaseStatus status)
    {
        return status switch
        {
            BoardCaseStatus.New => "Ny",
            BoardCaseStatus.InProgress => "Under behandling",
            BoardCaseStatus.Decided => "Vedtatt",
            _ => "Avsluttet"
        };
    }

    public static string PriorityText(BoardCasePriority priority)
    {
        return priority switch
        {
            BoardCasePriority.Low => "Lav",
            BoardCasePriority.Normal => "Normal",
            _ => "Høy"
        };
    }

    public static string SortOrderText(BoardCaseSortOrder order)
    {
        return order switch
        {
            BoardCaseSortOrder.Oldest => "Eldste først",
            BoardCaseSortOrder.RecentlyUpdated => "Sist oppdatert",
            BoardCaseSortOrder.MeetingDate => "Møtedato",
            BoardCaseSortOrder.Priority => "Prioritet",
            BoardCaseSortOrder.Title => "Tittel",
            _ => "Nyeste først"
        };
    }

    public static string StatusCssClass(BoardCaseStatus status)
    {
        return status switch
        {
            BoardCaseStatus.New => "status-new",
            BoardCaseStatus.InProgress => "status-in-progress",
            BoardCaseStatus.Decided => "status-decided",
            _ => "status-closed"
        };
    }

    public static string PriorityCssClass(BoardCasePriority priority)
    {
        return priority switch
        {
            BoardCasePriority.Low => "priority-low",
            BoardCasePriority.Normal => "priority-normal",
            _ => "priority-high"
        };
    }
}
