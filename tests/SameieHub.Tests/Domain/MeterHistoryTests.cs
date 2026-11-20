using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class MeterHistoryTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tolerance = TimeSpan.FromHours(24);

    private int _nextId = 1;

    private MeterReading Reading(decimal kwh, int daysAfterStart, int hours = 0)
    {
        var reading = new MeterReading(
            1,
            new MeterReadingDetails { ReadAt = Start.AddDays(daysAfterStart).AddHours(hours), Kwh = kwh },
            Start.AddDays(100));
        reading.AssignId(_nextId++);

        return reading;
    }

    // Sorting and intervals

    [Fact]
    public void Sort_OrdersByReadTimeNotByRegistrationOrder()
    {
        var late = Reading(300, 60);
        var early = Reading(100, 0);
        var middle = Reading(200, 30);

        var sorted = MeterHistory.Sort([late, early, middle]);

        Assert.Equal([100m, 200m, 300m], sorted.Select(reading => reading.Kwh));
    }

    [Fact]
    public void Sort_UsesTheTimeOfDayToo()
    {
        var evening = Reading(150, 0, hours: 18);
        var morning = Reading(100, 0, hours: 6);

        Assert.Equal([100m, 150m], MeterHistory.Sort([evening, morning]).Select(reading => reading.Kwh));
    }

    [Fact]
    public void Sort_ReturnsANewListAndKeepsTheInput()
    {
        var readings = new List<MeterReading> { Reading(200, 30), Reading(100, 0) };

        MeterHistory.Sort(readings);

        Assert.Equal([200m, 100m], readings.Select(reading => reading.Kwh));
    }

    [Fact]
    public void BuildIntervals_NoReadingsOrOnlyTheStartValue_GivesNoConsumption()
    {
        Assert.Empty(MeterHistory.BuildIntervals([]));
        Assert.Empty(MeterHistory.BuildIntervals([Reading(1234.5m, 0)]));
    }

    [Fact]
    public void BuildIntervals_FirstReadingIsAStartValueAndConsumptionIsTheDifference()
    {
        var intervals = MeterHistory.BuildIntervals([Reading(1000m, 0), Reading(1182.4m, 30), Reading(1388.15m, 61)]);

        Assert.Equal(2, intervals.Count);
        Assert.Equal(182.4m, intervals[0].Kwh);
        Assert.Equal(205.75m, intervals[1].Kwh);
        Assert.Equal(TimeSpan.FromDays(30), intervals[0].Duration);
        Assert.Equal(TimeSpan.FromDays(31), intervals[1].Duration);
    }

    [Fact]
    public void BuildIntervals_UnsortedInput_IsSortedFirst()
    {
        var intervals = MeterHistory.BuildIntervals([Reading(1182.4m, 30), Reading(1000m, 0)]);

        Assert.Equal(182.4m, Assert.Single(intervals).Kwh);
    }

    [Fact]
    public void BuildIntervals_EqualReadings_GiveZeroConsumptionNotNothing()
    {
        var intervals = MeterHistory.BuildIntervals([Reading(85m, 0), Reading(85m, 90)]);

        Assert.Equal(0m, Assert.Single(intervals).Kwh);
    }

    [Fact]
    public void BuildIntervals_KeepsThreeDecimalsExactly()
    {
        var intervals = MeterHistory.BuildIntervals([Reading(0.001m, 0), Reading(0.003m, 1)]);

        Assert.Equal(0.002m, Assert.Single(intervals).Kwh);
    }

    [Fact]
    public void TotalDocumented_IsLastMinusFirstAndEqualsTheSumOfTheIntervals()
    {
        var readings = new[] { Reading(1000m, 0), Reading(1182.4m, 30), Reading(1388.15m, 61), Reading(1500m, 90) };

        var total = MeterHistory.TotalDocumented(readings);

        Assert.Equal(500m, total);
        Assert.Equal(total, MeterHistory.BuildIntervals(readings).Sum(interval => interval.Kwh));
    }

    [Fact]
    public void TotalDocumented_FewerThanTwoReadings_IsNullNotZero()
    {
        Assert.Null(MeterHistory.TotalDocumented([]));
        Assert.Null(MeterHistory.TotalDocumented([Reading(100m, 0)]));
    }

    // Finding the reading for a time

    [Fact]
    public void FindNearest_ReturnsTheClosestReadingWithinTheTolerance()
    {
        var readings = new[] { Reading(100m, 0), Reading(200m, 30, hours: 3), Reading(300m, 60) };

        var found = MeterHistory.FindNearest(readings, Start.AddDays(30), Tolerance);

        Assert.Equal(200m, found!.Kwh);
    }

    [Fact]
    public void FindNearest_NothingWithinTheTolerance_ReturnsNull()
    {
        var readings = new[] { Reading(100m, 0), Reading(300m, 60) };

        Assert.Null(MeterHistory.FindNearest(readings, Start.AddDays(30), Tolerance));
    }

    [Fact]
    public void FindNearest_ToleranceLimitIsIncluded()
    {
        var readings = new[] { Reading(100m, 0) };

        Assert.NotNull(MeterHistory.FindNearest(readings, Start.AddHours(24), Tolerance));
        Assert.Null(MeterHistory.FindNearest(readings, Start.AddHours(24).AddMinutes(1), Tolerance));
    }

    [Fact]
    public void FindNearest_Tie_PicksTheEarlierReading()
    {
        var readings = new[] { Reading(100m, 0, hours: 6), Reading(200m, 0, hours: 18) };

        Assert.Equal(100m, MeterHistory.FindNearest(readings, Start.AddHours(12), Tolerance)!.Kwh);
    }

    // Consumption between two times

    [Fact]
    public void ConsumptionBetween_ReadingsAtBothEnds_IsDocumented()
    {
        var readings = new[] { Reading(1000m, 0), Reading(1200m, 30), Reading(1450m, 61) };

        var consumption = MeterHistory.ConsumptionBetween(readings, Start, Start.AddDays(30), Tolerance);

        Assert.Equal(MeterConsumptionState.Documented, consumption.State);
        Assert.Equal(200m, consumption.Kwh);
        Assert.Equal(1000m, consumption.StartReading!.Kwh);
        Assert.Equal(1200m, consumption.EndReading!.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_ReadingsReadOnSlightlyDifferentTimes_AreMatchedWithinTheTolerance()
    {
        var readings = new[] { Reading(1000m, 0, hours: 5), Reading(1200m, 30, hours: -7) };

        var consumption = MeterHistory.ConsumptionBetween(readings, Start.AddHours(8), Start.AddDays(30).AddHours(8), Tolerance);

        Assert.Equal(MeterConsumptionState.Documented, consumption.State);
        Assert.Equal(200m, consumption.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_NoReadingNearTheStart_IsNotDocumentedAndNothingIsEstimated()
    {
        var readings = new[] { Reading(1200m, 30), Reading(1450m, 61) };

        var consumption = MeterHistory.ConsumptionBetween(readings, Start, Start.AddDays(30), Tolerance);

        Assert.Equal(MeterConsumptionState.MissingStart, consumption.State);
        Assert.Null(consumption.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_NoReadingNearTheEnd_IsMissingEnd()
    {
        var readings = new[] { Reading(1000m, 0), Reading(1450m, 61) };

        var consumption = MeterHistory.ConsumptionBetween(readings, Start, Start.AddDays(30), Tolerance);

        Assert.Equal(MeterConsumptionState.MissingEnd, consumption.State);
        Assert.Null(consumption.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_NoReadingNearEitherEnd_IsMissingBoth()
    {
        var readings = new[] { Reading(1000m, 10), Reading(1450m, 20) };

        Assert.Equal(MeterConsumptionState.MissingBoth, MeterHistory.ConsumptionBetween(readings, Start, Start.AddDays(30), Tolerance).State);
    }

    [Fact]
    public void ConsumptionBetween_NoReadingsAtAll_IsNoReadings()
    {
        var consumption = MeterHistory.ConsumptionBetween([], Start, Start.AddDays(30), Tolerance);

        Assert.Equal(MeterConsumptionState.NoReadings, consumption.State);
        Assert.Null(consumption.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_ZeroConsumption_IsDocumentedAsZero()
    {
        var readings = new[] { Reading(85m, 0), Reading(85m, 30) };

        var consumption = MeterHistory.ConsumptionBetween(readings, Start, Start.AddDays(30), Tolerance);

        Assert.Equal(MeterConsumptionState.Documented, consumption.State);
        Assert.Equal(0m, consumption.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_ShortPeriodWhereTheSameReadingIsNearestToBothEnds_IsNotDocumented()
    {
        var readings = new[] { Reading(1000m, 0) };

        var consumption = MeterHistory.ConsumptionBetween(readings, Start.AddHours(-2), Start.AddHours(2), Tolerance);

        Assert.Equal(MeterConsumptionState.SameReading, consumption.State);
        Assert.Null(consumption.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_UsesOnlyTheReadingsNearTheEndsNotTheOnesBetween()
    {
        var readings = new[] { Reading(1000m, 0), Reading(1100m, 10), Reading(1200m, 20), Reading(1500m, 30) };

        Assert.Equal(500m, MeterHistory.ConsumptionBetween(readings, Start, Start.AddDays(30), Tolerance).Kwh);
    }

    // Fitting a reading into the chronology

    [Fact]
    public void EnsureFits_FirstReading_AlwaysFits()
    {
        MeterHistory.EnsureFits([], Start, 5000m);
    }

    [Fact]
    public void EnsureFits_AfterTheLastReadingWithAHigherOrEqualValue_Fits()
    {
        var existing = new[] { Reading(1000m, 0), Reading(1200m, 30) };

        MeterHistory.EnsureFits(existing, Start.AddDays(60), 1300m);
        MeterHistory.EnsureFits(existing, Start.AddDays(61), 1200m);
    }

    [Fact]
    public void EnsureFits_LowerThanTheEarlierReading_IsRejectedAndNamesTheEarlierReading()
    {
        var existing = new[] { Reading(1000m, 0), Reading(1200m, 30) };

        var exception = Assert.Throws<MeterReadingSequenceException>(() => MeterHistory.EnsureFits(existing, Start.AddDays(60), 1199.999m));

        Assert.True(exception.IsLowerThanEarlier);
        Assert.Equal(1200m, exception.NeighbourKwh);
        Assert.Equal(Start.AddDays(30), exception.NeighbourReadAt);
    }

    [Fact]
    public void EnsureFits_BetweenTwoReadings_MustBeBetweenTheirValues()
    {
        var existing = new[] { Reading(1000m, 0), Reading(1200m, 60) };

        MeterHistory.EnsureFits(existing, Start.AddDays(30), 1100m);
        Assert.True(Assert.Throws<MeterReadingSequenceException>(() => MeterHistory.EnsureFits(existing, Start.AddDays(30), 900m)).IsLowerThanEarlier);

        var tooHigh = Assert.Throws<MeterReadingSequenceException>(() => MeterHistory.EnsureFits(existing, Start.AddDays(30), 1300m));
        Assert.False(tooHigh.IsLowerThanEarlier);
        Assert.Equal(1200m, tooHigh.NeighbourKwh);
    }

    [Fact]
    public void EnsureFits_BeforeTheFirstReading_MustNotBeHigherThanIt()
    {
        var existing = new[] { Reading(1000m, 10) };

        MeterHistory.EnsureFits(existing, Start, 900m);
        Assert.False(Assert.Throws<MeterReadingSequenceException>(() => MeterHistory.EnsureFits(existing, Start, 1000.001m)).IsLowerThanEarlier);
    }

    [Fact]
    public void EnsureFits_SameTimeAsAnotherReading_IsRejectedAsADuplicate()
    {
        var existing = new[] { Reading(1000m, 0) };

        var exception = Assert.Throws<ArgumentException>(() => MeterHistory.EnsureFits(existing, Start, 1000m));

        Assert.Equal("ReadAt", exception.ParamName);
    }

    [Fact]
    public void EnsureFits_SameDayButAnotherTime_IsNotADuplicate()
    {
        var existing = new[] { Reading(1000m, 0, hours: 0) };

        MeterHistory.EnsureFits(existing, Start.AddHours(6), 1010m);
    }
}
