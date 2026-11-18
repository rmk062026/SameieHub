namespace SameieHub.Domain;

public enum BoardCaseSortOrder
{
    Newest,
    Oldest,
    RecentlyUpdated,
    MeetingDate,
    Priority,
    Title
}

public static class BoardCaseSorting
{
    private static readonly StringComparer TitleComparer =
        StringComparer.Create(new System.Globalization.CultureInfo("nb-NO"), ignoreCase: true);

    /// <summary>
    /// Returns a new sorted list. Ties are broken so the order is always the same.
    /// </summary>
    public static List<BoardCase> Sort(IEnumerable<BoardCase> cases, BoardCaseSortOrder order)
    {
        var sorted = order switch
        {
            BoardCaseSortOrder.Oldest => cases.OrderBy(boardCase => boardCase.CreatedAt).ThenBy(boardCase => boardCase.Id),
            BoardCaseSortOrder.RecentlyUpdated => cases.OrderByDescending(boardCase => boardCase.UpdatedAt).ThenByDescending(boardCase => boardCase.Id),
            BoardCaseSortOrder.MeetingDate => cases
                .OrderBy(boardCase => boardCase.MeetingDate == null)
                .ThenBy(boardCase => boardCase.MeetingDate)
                .ThenByDescending(boardCase => boardCase.Id),
            BoardCaseSortOrder.Priority => cases.OrderByDescending(boardCase => boardCase.Priority).ThenByDescending(boardCase => boardCase.CreatedAt),
            BoardCaseSortOrder.Title => cases.OrderBy(boardCase => boardCase.Title, TitleComparer).ThenBy(boardCase => boardCase.Id),
            _ => cases.OrderByDescending(boardCase => boardCase.CreatedAt).ThenByDescending(boardCase => boardCase.Id)
        };

        return sorted.ToList();
    }
}
