namespace SameieHub.Domain;

/// <summary>
/// Norwegian time and dates. Times are stored as UTC, but "today" and the dates users see follow Norwegian time.
/// </summary>
public static class NorwegianTime
{
    private static readonly TimeZoneInfo TimeZone = FindTimeZone();

    public static DateTime FromUtc(DateTime utcTime)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(utcTime, TimeZone);
    }

    public static DateOnly DateOf(DateTimeOffset utcNow)
    {
        return DateOnly.FromDateTime(FromUtc(utcNow.UtcDateTime));
    }

    private static TimeZoneInfo FindTimeZone()
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
