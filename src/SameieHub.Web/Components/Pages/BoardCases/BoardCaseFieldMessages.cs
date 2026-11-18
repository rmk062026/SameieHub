using SameieHub.Domain;
using SameieHub.Web.Components.Shared;

namespace SameieHub.Web.Components.Pages.BoardCases;

/// <summary>
/// Norwegian messages for the rules enforced by <see cref="BoardCase"/> and <see cref="BoardCaseService"/>.
/// The keys are the property names the domain reports in <see cref="ArgumentException.ParamName"/>.
/// </summary>
public static class BoardCaseFieldMessages
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Title"] = $"Tittelen må fylles ut og kan ha maks {BoardCase.MaxTitleLength} tegn.",
        ["Description"] = $"Beskrivelsen kan ha maks {BoardCase.MaxDescriptionLength} tegn.",
        ["Priority"] = "Velg en gyldig prioritet.",
        ["MeetingDate"] = $"Møtedatoen må være mellom {NorwegianDateFormat.Date(BoardCase.MinMeetingDate)} og {NorwegianDateFormat.Date(BoardCase.MaxMeetingDate)}.",
        ["HousingUnitId"] = "Boenheten finnes ikke eller er inaktiv. Velg en aktiv boenhet, eller la saken gjelde hele sameiet.",
        ["Notes"] = $"Merknader kan ha maks {BoardCase.MaxNotesLength} tegn."
    };

    public static bool TryGet(string? fieldName, out string message)
    {
        message = string.Empty;

        return fieldName != null && Messages.TryGetValue(fieldName, out message!);
    }
}
