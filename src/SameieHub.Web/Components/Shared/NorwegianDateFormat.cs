using System.Globalization;
using SameieHub.Domain;

namespace SameieHub.Web.Components.Shared;

/// <summary>
/// Shows dates the Norwegian way. Times are stored as UTC and shown in Norwegian time.
/// </summary>
public static class NorwegianDateFormat
{
    public static string DateTime(DateTime utcTime)
    {
        return NorwegianTime.FromUtc(utcTime).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    public static string Date(DateOnly date)
    {
        return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
