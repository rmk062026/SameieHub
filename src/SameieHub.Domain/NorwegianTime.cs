namespace SameieHub.Domain;

/// <summary>Why a Norwegian local time could not be converted to UTC.</summary>
public enum LocalTimeProblem
{
    None,

    /// <summary>The time does not exist: the clock skipped it when daylight saving time started.</summary>
    DoesNotExist,

    /// <summary>The time happened twice: the clock repeated it when daylight saving time ended. The user must choose.</summary>
    Ambiguous
}

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

    /// <summary>True when the UTC time falls in Norwegian summer time (daylight saving time).</summary>
    public static bool IsDaylightTime(DateTime utcTime)
    {
        // A UTC time is converted unambiguously, unlike the repeated local hour in autumn.
        return TimeZone.IsDaylightSavingTime(DateTime.SpecifyKind(utcTime, DateTimeKind.Utc));
    }

    public static DateOnly DateOf(DateTimeOffset utcNow)
    {
        return DateOnly.FromDateTime(FromUtc(utcNow.UtcDateTime));
    }

    /// <summary>
    /// Converts a time the user typed (Norwegian local time) to UTC. Two times around the daylight saving changes
    /// need care: a time that does not exist (the hour skipped in spring) is refused, and a time that happened twice
    /// (the hour repeated in autumn) is refused unless <paramref name="ambiguousIsDaylightTime"/> says which one is meant:
    /// true for the first (summer time), false for the second (winter time).
    /// </summary>
    public static bool TryConvertToUtc(DateTime localTime, bool? ambiguousIsDaylightTime, out DateTime utcTime, out LocalTimeProblem problem)
    {
        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        utcTime = default;

        if (TimeZone.IsInvalidTime(unspecified))
        {
            problem = LocalTimeProblem.DoesNotExist;

            return false;
        }

        TimeSpan offset;
        if (TimeZone.IsAmbiguousTime(unspecified))
        {
            if (ambiguousIsDaylightTime == null)
            {
                problem = LocalTimeProblem.Ambiguous;

                return false;
            }

            var offsets = TimeZone.GetAmbiguousTimeOffsets(unspecified);
            offset = ambiguousIsDaylightTime.Value ? offsets.Max() : offsets.Min();
        }
        else
        {
            offset = TimeZone.GetUtcOffset(unspecified);
        }

        utcTime = DateTime.SpecifyKind(unspecified - offset, DateTimeKind.Utc);
        problem = LocalTimeProblem.None;

        return true;
    }

    /// <summary>True when the local time happens twice (the autumn change), so the user must say which one is meant.</summary>
    public static bool IsAmbiguous(DateTime localTime)
    {
        return TimeZone.IsAmbiguousTime(DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified));
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
