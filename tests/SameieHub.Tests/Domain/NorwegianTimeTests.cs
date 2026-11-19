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
}
