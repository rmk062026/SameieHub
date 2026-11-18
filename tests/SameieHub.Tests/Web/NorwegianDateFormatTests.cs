using SameieHub.Web.Components.Shared;

namespace SameieHub.Tests.Web;

public class NorwegianDateFormatTests
{
    [Fact]
    public void DateTime_WinterTime_IsOneHourAheadOfUtc()
    {
        var utc = new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

        Assert.Equal("18.11.2026 11:30", NorwegianDateFormat.DateTime(utc));
    }

    [Fact]
    public void DateTime_SummerTime_IsTwoHoursAheadOfUtc()
    {
        var utc = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal("01.07.2026 12:00", NorwegianDateFormat.DateTime(utc));
    }

    [Fact]
    public void DateTime_LateEveningUtc_MovesToTheNextNorwegianDay()
    {
        var utc = new DateTime(2026, 12, 31, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal("01.01.2027 00:30", NorwegianDateFormat.DateTime(utc));
    }

    [Fact]
    public void Date_ShowsDayMonthYear()
    {
        Assert.Equal("03.12.2026", NorwegianDateFormat.Date(new DateOnly(2026, 12, 3)));
    }
}
