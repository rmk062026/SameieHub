namespace SameieHub.Domain;

public static class HousingUnitFilter
{
    /// <summary>
    /// Filters on status and on search text. The text is matched, ignoring case, against address,
    /// unit number, section number and both owner names.
    /// </summary>
    public static List<HousingUnit> Apply(IEnumerable<HousingUnit> units, string? searchText, HousingUnitStatusFilter status)
    {
        var text = searchText?.Trim();
        var result = new List<HousingUnit>();

        foreach (var unit in units)
        {
            if (MatchesStatus(unit, status) && MatchesText(unit, text))
            {
                result.Add(unit);
            }
        }

        return result;
    }

    private static bool MatchesStatus(HousingUnit unit, HousingUnitStatusFilter status)
    {
        return status switch
        {
            HousingUnitStatusFilter.Active => unit.IsActive,
            HousingUnitStatusFilter.Inactive => !unit.IsActive,
            _ => true
        };
    }

    private static bool MatchesText(HousingUnit unit, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        return Contains(unit.Address, text)
            || Contains(unit.UnitNumber, text)
            || Contains(unit.SectionNumber?.ToString(), text)
            || Contains(unit.OwnerName, text)
            || Contains(unit.SecondOwnerName, text);
    }

    private static bool Contains(string? value, string text)
    {
        return value != null && value.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }
}
