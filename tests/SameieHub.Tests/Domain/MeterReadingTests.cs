using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class MeterReadingTests
{
    private static readonly DateTime ReadAt = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Registered = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    private static MeterReading NewReading(decimal kwh = 1234.567m, string? notes = null, DateTime? readAt = null)
    {
        return new MeterReading(3, new MeterReadingDetails { ReadAt = readAt ?? ReadAt, Kwh = kwh, Notes = notes }, Registered);
    }

    [Fact]
    public void Create_ValidReading_KeepsReadTimeAndRegistrationTimeApart()
    {
        var reading = NewReading(notes: "  Avlest av styret ");

        Assert.Equal(3, reading.MeterId);
        Assert.Equal(ReadAt, reading.ReadAt);
        Assert.Equal(Registered, reading.RegisteredAt);
        Assert.Equal(Registered, reading.UpdatedAt);
        Assert.Equal(1234.567m, reading.Kwh);
        Assert.Equal("Avlest av styret", reading.Notes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.001)]
    [InlineData(1500.25)]
    [InlineData(9999999.999)]
    public void Create_ValidKwh_IsAccepted(double value)
    {
        Assert.Equal((decimal)value, NewReading((decimal)value).Kwh);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(-5)]
    [InlineData(10000000)]
    public void Create_KwhOutOfRange_IsRejectedWithTheKwhName(double value)
    {
        Assert.Equal("Kwh", Assert.Throws<ArgumentException>(() => NewReading((decimal)value)).ParamName);
    }

    [Fact]
    public void Create_MoreThanThreeDecimals_IsRejectedNotRounded()
    {
        Assert.Equal("Kwh", Assert.Throws<ArgumentException>(() => NewReading(10.1234m)).ParamName);
    }

    [Fact]
    public void Create_ThreeDecimalsKeepAllPrecision()
    {
        Assert.Equal(0.125m, NewReading(0.125m).Kwh);
        Assert.Equal(1234567.891m, NewReading(1234567.891m).Kwh);
    }

    [Fact]
    public void Create_NonUtcReadTime_IsRejected()
    {
        var local = DateTime.SpecifyKind(ReadAt, DateTimeKind.Local);

        Assert.Equal("ReadAt", Assert.Throws<ArgumentException>(() => NewReading(readAt: local)).ParamName);
    }

    [Fact]
    public void Create_ReadTimeBefore2000_IsRejected()
    {
        var old = new DateTime(1999, 12, 31, 23, 0, 0, DateTimeKind.Utc);

        Assert.Equal("ReadAt", Assert.Throws<ArgumentException>(() => NewReading(readAt: old)).ParamName);
    }

    [Fact]
    public void Create_NotesTooLong_AreRejected()
    {
        Assert.Equal("Notes", Assert.Throws<ArgumentException>(() => NewReading(notes: new string('A', 501))).ParamName);
    }

    [Fact]
    public void Create_NonUtcRegistrationTimeOrInvalidMeter_AreRejected()
    {
        var details = new MeterReadingDetails { ReadAt = ReadAt, Kwh = 1 };

        Assert.Throws<ArgumentException>(() => new MeterReading(3, details, DateTime.SpecifyKind(Registered, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeterReading(0, details, Registered));
    }

    [Fact]
    public void AssignId_OnlyOnce()
    {
        var reading = NewReading();

        reading.AssignId(8);

        Assert.Equal(8, reading.Id);
        Assert.Throws<InvalidOperationException>(() => reading.AssignId(9));
    }

    // Corrections

    [Fact]
    public void Correct_ChangesValuesAndUpdatedAtButNotRegisteredAt()
    {
        var reading = NewReading(100m);
        var now = Registered.AddDays(2);

        reading.Correct(new MeterReadingDetails { ReadAt = ReadAt.AddHours(1), Kwh = 110.5m, Notes = "Rettet" }, now);

        Assert.Equal(110.5m, reading.Kwh);
        Assert.Equal(ReadAt.AddHours(1), reading.ReadAt);
        Assert.Equal("Rettet", reading.Notes);
        Assert.Equal(now, reading.UpdatedAt);
        Assert.Equal(Registered, reading.RegisteredAt);
    }

    [Fact]
    public void Correct_NothingChanged_KeepsUpdatedAt()
    {
        var reading = NewReading(100m);

        reading.Correct(reading.ToDetails(), Registered.AddDays(2));

        Assert.Equal(Registered, reading.UpdatedAt);
    }

    [Fact]
    public void Correct_InvalidValue_ChangesNothing()
    {
        var reading = NewReading(100m, "Opprinnelig");

        Assert.Throws<ArgumentException>(() =>
            reading.Correct(new MeterReadingDetails { ReadAt = ReadAt.AddDays(1), Kwh = -1m, Notes = "Ny" }, Registered.AddDays(2)));

        Assert.Equal(100m, reading.Kwh);
        Assert.Equal(ReadAt, reading.ReadAt);
        Assert.Equal("Opprinnelig", reading.Notes);
        Assert.Equal(Registered, reading.UpdatedAt);
    }
}
