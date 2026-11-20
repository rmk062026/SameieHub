using System.Globalization;
using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.Charging;

/// <summary>Reads what the user typed in the charging forms: meter values and Norwegian local times.</summary>
public static class ChargingInput
{
    public const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm";

    /// <summary>
    /// Reads a meter value. Empty text means «not read» and gives null with no error. The text 0 is a real value.
    /// Both comma and point are accepted as decimal separator.
    /// </summary>
    /// <returns>null when the text is fine (also when it is empty), otherwise a Norwegian error message.</returns>
    public static string? TryParseKwh(string? text, out decimal? kwh)
    {
        kwh = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim().Replace(" ", string.Empty).Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return "Skriv målerstanden som et tall, for eksempel 1234,5.";
        }

        try
        {
            MeterReading.CheckKwh(parsed);
        }
        catch (ArgumentException)
        {
            return parsed < 0
                ? "Målerstanden kan ikke være negativ."
                : $"Målerstanden kan ha høyst {MeterReading.KwhDecimals} desimaler og være høyst {ChargingText.Number(MeterReading.MaxKwh)} kWh.";
        }

        kwh = parsed;

        return null;
    }

    /// <summary>The value for a datetime-local input, from a UTC time shown as Norwegian time.</summary>
    public static string ToInputValue(DateTime utc)
    {
        return NorwegianTime.FromUtc(utc).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts the text of a datetime-local input (Norwegian time) to UTC. A time that does not exist or that
    /// happens twice at the daylight saving changes is explained, and for the repeated hour the user chooses.
    /// </summary>
    /// <returns>null when the time is fine, otherwise a Norwegian error message.</returns>
    public static string? TryConvertToUtc(string? text, bool? ambiguousIsDaylightTime, out DateTime utc)
    {
        utc = default;

        if (string.IsNullOrWhiteSpace(text)
            || !DateTime.TryParseExact(text, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return "Oppgi dato og klokkeslett for avlesningen.";
        }

        if (NorwegianTime.TryConvertToUtc(local, ambiguousIsDaylightTime, out utc, out var problem))
        {
            return null;
        }

        return problem == LocalTimeProblem.DoesNotExist
            ? "Dette klokkeslettet fantes ikke: klokken ble stilt frem ved overgangen til sommertid. Velg et annet klokkeslett."
            : "Dette klokkeslettet forekommer to ganger ved overgangen til vintertid. Velg om avlesningen var før (sommertid) eller etter (vintertid) klokken ble stilt tilbake.";
    }

    /// <summary>True when the text is a time that happens twice, so the form must ask which one is meant.</summary>
    public static bool IsAmbiguous(string? text)
    {
        return DateTime.TryParseExact(text, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            && NorwegianTime.IsAmbiguous(local);
    }
}
