using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.Charging;

/// <summary>
/// Norwegian messages for the rules enforced by <see cref="EvCharger"/> and <see cref="ChargingService"/>.
/// The keys are the property names the domain reports in <see cref="ArgumentException.ParamName"/>.
/// </summary>
public static class ChargerFieldMessages
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["HousingUnitId"] = "Boenheten finnes ikke. Velg en boenhet fra listen.",
        ["CabinetNumber"] = "Sikringsskapet må være 1 eller 2.",
        ["ChargerNumber"] = $"Ladernummer kan ha maks {EvCharger.MaxChargerNumberLength} tegn.",
        ["Manufacturer"] = $"Produsent kan ha maks {EvCharger.MaxManufacturerLength} tegn.",
        ["Model"] = $"Modell kan ha maks {EvCharger.MaxModelLength} tegn.",
        ["Status"] = "Velg en gyldig status.",
        ["Notes"] = $"Merknader kan ha maks {EvCharger.MaxNotesLength} tegn."
    };

    public static bool TryGet(string? fieldName, out string message)
    {
        message = string.Empty;

        return fieldName != null && Messages.TryGetValue(fieldName, out message!);
    }
}
