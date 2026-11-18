using System.Globalization;

namespace SameieHub.Web.Components.Shared;

/// <summary>
/// Shows dates the Norwegian way. Times are stored as UTC and shown in Norwegian time.
/// </summary>
public static class NorwegianDateFormat
{
    private static readonly TimeZoneInfo NorwegianTimeZone = FindNorwegianTimeZone();

    public static string DateTime(DateTime utcTime)
    {
        var norwegianTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, NorwegianTimeZone);

        return norwegianTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    public static string Date(DateOnly date)
    {
        return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo FindNorwegianTimeZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Oslo", out var ianaZone))
        {
            return ianaZone;
        }

        if (TimeZoneInfo.TryFindSystemTimeZoneById("W. Europe Standard Time", out var windowsZone))
        {
            return windowsZone;
        }

        return TimeZoneInfo.Local;
    }
}
