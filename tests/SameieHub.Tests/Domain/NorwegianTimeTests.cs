using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class NorwegianTimeTests
{
    [Fact]
    public void FromUtc_Winter_IsOneHourAhead()
    {
        var result = NorwegianTime.FromUtc(new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 11, 18, 11, 30, 0), result);
    }

    [Fact]
    public void FromUtc_Summer_IsTwoHoursAhead()
    {
        var result = NorwegianTime.FromUtc(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 7, 1, 12, 0, 0), result);
    }

    [Theory]
    [InlineData(2026, 11, 18, 22, 59, 18)]
    [InlineData(2026, 11, 18, 23, 0, 19)]
    [InlineData(2026, 7, 1, 21, 59, 1)]
    [InlineData(2026, 7, 1, 22, 0, 2)]
    public void DateOf_ChangesAtNorwegianMidnight(int year, int month, int day, int hour, int minute, int expectedDay)
    {
        var utcNow = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

        var date = NorwegianTime.DateOf(utcNow);

        Assert.Equal(expectedDay, date.Day);
    }

    // Local time to UTC, including the daylight saving changes (2026: 29 March and 25 October)

    [Fact]
    public void TryConvertToUtc_Winter_SubtractsOneHour()
    {
        var ok = NorwegianTime.TryConvertToUtc(new DateTime(2026, 11, 18, 11, 30, 0), null, out var utc, out var problem);

        Assert.True(ok);
        Assert.Equal(LocalTimeProblem.None, problem);
        Assert.Equal(new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void TryConvertToUtc_Summer_SubtractsTwoHours()
    {
        var ok = NorwegianTime.TryConvertToUtc(new DateTime(2026, 7, 1, 12, 0, 0), null, out var utc, out _);

        Assert.True(ok);
        Assert.Equal(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void TryConvertToUtc_ATimeSkippedInSpring_DoesNotExist()
    {
        var ok = NorwegianTime.TryConvertToUtc(new DateTime(2026, 3, 29, 2, 30, 0), null, out _, out var problem);

        Assert.False(ok);
        Assert.Equal(LocalTimeProblem.DoesNotExist, problem);
    }

    [Theory]
    [InlineData(1, 59, 0)]
    [InlineData(3, 0, 1)]
    public void TryConvertToUtc_TheTimesAroundTheSpringChange_AreValidAndNotMixedUp(int hour, int minute, int expectedUtcHour)
    {
        var ok = NorwegianTime.TryConvertToUtc(new DateTime(2026, 3, 29, hour, minute, 0), null, out var utc, out _);

        Assert.True(ok);
        Assert.Equal(expectedUtcHour, utc.Hour);
    }

    [Fact]
    public void TryConvertToUtc_ATimeRepeatedInAutumn_IsAmbiguousUntilTheUserChooses()
    {
        var local = new DateTime(2026, 10, 25, 2, 30, 0);

        var ok = NorwegianTime.TryConvertToUtc(local, null, out _, out var problem);

        Assert.False(ok);
        Assert.Equal(LocalTimeProblem.Ambiguous, problem);
        Assert.True(NorwegianTime.IsAmbiguous(local));
    }

    [Fact]
    public void TryConvertToUtc_AmbiguousTime_FirstIsSummerTimeAndSecondIsWinterTime()
    {
        var local = new DateTime(2026, 10, 25, 2, 30, 0);

        NorwegianTime.TryConvertToUtc(local, ambiguousIsDaylightTime: true, out var summer, out _);
        NorwegianTime.TryConvertToUtc(local, ambiguousIsDaylightTime: false, out var winter, out _);

        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), summer);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc), winter);
        Assert.Equal(TimeSpan.FromHours(1), winter - summer);
    }

    [Theory]
    [InlineData(2026, 10, 25, 1, 59)]
    [InlineData(2026, 10, 25, 3, 0)]
    [InlineData(2026, 11, 18, 2, 30)]
    public void IsAmbiguous_OnlyTheRepeatedHour_IsAmbiguous(int year, int month, int day, int hour, int minute)
    {
        Assert.False(NorwegianTime.IsAmbiguous(new DateTime(year, month, day, hour, minute, 0)));
    }

    [Fact]
    public void TryConvertToUtc_AndFromUtc_AreEachOthersInverse()
    {
        var utc = new DateTime(2026, 6, 15, 8, 45, 0, DateTimeKind.Utc);

        NorwegianTime.TryConvertToUtc(NorwegianTime.FromUtc(utc), null, out var back, out _);

        Assert.Equal(utc, back);
    }

    [Theory]
    [InlineData(2027, 7, 1, 6, true)]
    [InlineData(2027, 1, 15, 6, false)]
    [InlineData(2027, 10, 31, 0, true)]
    [InlineData(2027, 10, 31, 1, false)]
    public void IsDaylightTime_UsesTheUtcInstant_AlsoInTheRepeatedHour(int year, int month, int day, int hour, bool expected)
    {
        var utc = new DateTime(year, month, day, hour, 30, 0, DateTimeKind.Utc);

        Assert.Equal(expected, NorwegianTime.IsDaylightTime(utc));
    }
}
