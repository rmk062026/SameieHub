using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.Charging;

/// <summary>Norwegian explanations of the rules for meter readings that the domain reports as exceptions.</summary>
public static class ChargingErrorText
{
    public const string ReadAtMessage =
        "Tidspunktet kan ikke være i fremtiden, ikke før år 2000, og måleren kan ikke ha en annen avlesning på nøyaktig samme tidspunkt.";

    public static string Sequence(MeterReadingSequenceException exception)
    {
        var time = ChargingText.Time(exception.NeighbourReadAt);
        var value = ChargingText.Kwh(exception.NeighbourKwh);

        return exception.IsLowerThanEarlier
            ? $"Målerstanden er lavere enn avlesningen {time} ({value}). Målerstanden kan ikke gå ned."
            : $"Målerstanden er høyere enn den senere avlesningen {time} ({value}). Målerstanden kan ikke gå ned.";
    }

    public static string? ForReadingField(string? paramName)
    {
        return paramName switch
        {
            nameof(MeterReadingDetails.ReadAt) => ReadAtMessage,
            nameof(MeterReadingDetails.Kwh) => "Målerstanden må være et tall fra 0 med høyst tre desimaler.",
            nameof(MeterReadingDetails.Notes) => $"Merknaden kan ha maks {MeterReading.MaxNotesLength} tegn.",
            _ => null
        };
    }
}
