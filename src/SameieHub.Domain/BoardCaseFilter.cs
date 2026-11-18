namespace SameieHub.Domain;

public record BoardCaseFilterCriteria
{
    /// <summary>Matched, ignoring case, against title and description, and exactly against the case number.</summary>
    public string? SearchText { get; init; }

    public BoardCaseStatus? Status { get; init; }

    public BoardCasePriority? Priority { get; init; }

    /// <summary>Only cases linked to this housing unit.</summary>
    public int? HousingUnitId { get; init; }

    /// <summary>Only cases without a housing unit, that is, cases for the whole housing cooperative.</summary>
    public bool WholeCooperativeOnly { get; init; }
}

public static class BoardCaseFilter
{
    public static List<BoardCase> Apply(IEnumerable<BoardCase> cases, BoardCaseFilterCriteria criteria)
    {
        var searchText = criteria.SearchText?.Trim();
        var result = new List<BoardCase>();

        foreach (var boardCase in cases)
        {
            if (MatchesFilters(boardCase, criteria) && MatchesText(boardCase, searchText))
            {
                result.Add(boardCase);
            }
        }

        return result;
    }

    private static bool MatchesFilters(BoardCase boardCase, BoardCaseFilterCriteria criteria)
    {
        if (criteria.Status != null && boardCase.Status != criteria.Status)
        {
            return false;
        }

        if (criteria.Priority != null && boardCase.Priority != criteria.Priority)
        {
            return false;
        }

        if (criteria.WholeCooperativeOnly && boardCase.HousingUnitId != null)
        {
            return false;
        }

        if (criteria.HousingUnitId != null && boardCase.HousingUnitId != criteria.HousingUnitId)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesText(BoardCase boardCase, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var isCaseNumber = int.TryParse(text.TrimStart('#'), out var caseNumber) && caseNumber == boardCase.Id;

        return isCaseNumber
            || Contains(boardCase.Title, text)
            || Contains(boardCase.Description, text);
    }

    private static bool Contains(string? value, string text)
    {
        return value != null && value.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }
}
