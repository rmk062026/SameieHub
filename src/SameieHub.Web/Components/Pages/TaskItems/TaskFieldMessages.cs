using SameieHub.Domain;
using SameieHub.Web.Components.Shared;

namespace SameieHub.Web.Components.Pages.TaskItems;

/// <summary>
/// Norwegian messages for the rules enforced by <see cref="TaskItem"/> and <see cref="TaskItemService"/>.
/// The keys are the property names the domain reports in <see cref="ArgumentException.ParamName"/>.
/// </summary>
public static class TaskFieldMessages
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Title"] = $"Tittelen må fylles ut og kan ha maks {TaskItem.MaxTitleLength} tegn.",
        ["Description"] = $"Beskrivelsen kan ha maks {TaskItem.MaxDescriptionLength} tegn.",
        ["Category"] = "Velg en gyldig kategori.",
        ["Priority"] = "Velg en gyldig prioritet.",
        ["DueDate"] = $"Fristen må være mellom {NorwegianDateFormat.Date(TaskItem.MinDueDate)} og {NorwegianDateFormat.Date(TaskItem.MaxDueDate)}.",
        ["AssignedTo"] = $"Ansvarlig kan ha maks {TaskItem.MaxAssignedToLength} tegn.",
        ["HousingUnitId"] = "Boenheten finnes ikke eller er inaktiv. Velg en aktiv boenhet, eller la oppgaven stå uten boenhet.",
        ["BoardCaseId"] = "Styresaken finnes ikke. Velg en annen styresak, eller la oppgaven stå uten styresak.",
        ["Notes"] = $"Merknader kan ha maks {TaskItem.MaxNotesLength} tegn."
    };

    public static bool TryGet(string? fieldName, out string message)
    {
        message = string.Empty;

        return fieldName != null && Messages.TryGetValue(fieldName, out message!);
    }
}
