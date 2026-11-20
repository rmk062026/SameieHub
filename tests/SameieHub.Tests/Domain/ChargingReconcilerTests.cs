using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class ChargingReconcilerTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tolerance = TimeSpan.FromHours(24);

    private readonly List<Meter> _meters = [];
    private readonly List<EvCharger> _chargers = [];
    private readonly List<MeterReading> _readings = [];
    private int _nextReadingId = 1;

    public ChargingReconcilerTests()
    {
        AddMeter(Meter.CreateMain(1, "Hovedmåler", Created));
    }

    private Meter Main => _meters.Single(meter => meter.IsMain);

    private void AddMeter(Meter meter)
    {
        meter.AssignId(_meters.Count + 1);
        _meters.Add(meter);
    }

    private Meter AddSubMeter(int unitId, ChargerStatus status = ChargerStatus.InUse)
    {
        var charger = new EvCharger(1, unitId, new EvChargerDetails { Status = status, ChargerNumber = $"L{unitId}" }, Created);
        charger.AssignId(_chargers.Count + 1);
        _chargers.Add(charger);

        var meter = Meter.CreateSub(1, charger.Id, $"Undermåler L{unitId}", unitId <= 4 ? 1 : 2, Created);
        AddMeter(meter);

        return meter;
    }

    private void Read(Meter meter, DateTime readAt, decimal kwh)
    {
        var reading = new MeterReading(meter.Id, new MeterReadingDetails { ReadAt = readAt, Kwh = kwh }, Created);
        reading.AssignId(_nextReadingId++);
        _readings.Add(reading);
    }

    private void ReadBoth(Meter meter, decimal atStart, decimal atEnd)
    {
        Read(meter, Start, atStart);
        Read(meter, End, atEnd);
    }

    private ChargingReconciliation Reconcile(DateTime? from = null, DateTime? to = null, TimeSpan? tolerance = null)
    {
        return ChargingReconciler.Reconcile(_meters, _chargers, _readings, from ?? Start, to ?? End, tolerance ?? Tolerance);
    }

    private void FillEightSubMeters(decimal kwhEach, decimal startValue = 100m)
    {
        for (var unit = 1; unit <= 8; unit++)
        {
            var meter = AddSubMeter(unit);
            ReadBoth(meter, startValue, startValue + kwhEach);
        }
    }

    // Hierarchy

    [Fact]
    public void Reconcile_ListsTheMainMeterAndTheEightSubMetersWithTheirChargers()
    {
        ReadBoth(Main, 5000m, 5800m);
        FillEightSubMeters(97m);

        var result = Reconcile();

        Assert.Equal(Main.Id, result.Main!.Meter.Id);
        Assert.Null(result.Main.Charger);
        Assert.Equal(8, result.SubMeters.Count);
        Assert.All(result.SubMeters, line => Assert.False(line.Meter.IsMain));
        Assert.All(result.SubMeters, line => Assert.Equal(line.Meter.EvChargerId, line.Charger!.Id));
        Assert.Equal(Enumerable.Range(1, 8), result.SubMeters.Select(line => line.Charger!.HousingUnitId));
    }

    // Formula

    [Fact]
    public void Reconcile_AllMetersDocumented_DifferenceIsMainMinusTheSumOfTheSubMeters()
    {
        ReadBoth(Main, 5000m, 5800m);
        FillEightSubMeters(97m);

        var result = Reconcile();

        Assert.True(result.IsComplete);
        Assert.Equal(800m, result.MainKwh);
        Assert.Equal(776m, result.SubMeterKwh);
        Assert.Equal(24m, result.Difference);
        Assert.Equal(3m, result.DifferencePercent);
        Assert.Empty(result.MissingMeters);
    }

    [Fact]
    public void Reconcile_SumsConsumptionNotAccumulatedMeterValues()
    {
        ReadBoth(Main, 90000m, 90400m);
        for (var unit = 1; unit <= 8; unit++)
        {
            ReadBoth(AddSubMeter(unit), 5000m * unit, 5000m * unit + 50m);
        }

        var result = Reconcile();

        Assert.Equal(400m, result.SubMeterKwh);
        Assert.Equal(0m, result.Difference);
        Assert.Equal(0m, result.DifferencePercent);
    }

    [Fact]
    public void Reconcile_SubMetersAddUpToMoreThanTheMainMeter_GivesANegativeDifference()
    {
        ReadBoth(Main, 5000m, 5700m);
        FillEightSubMeters(90m);

        var result = Reconcile();

        Assert.Equal(-20m, result.Difference);
        Assert.Equal(-20m / 700m * 100m, result.DifferencePercent);
    }

    [Fact]
    public void Reconcile_MainMeterConsumptionIsZero_HasADifferenceButNoPercent()
    {
        ReadBoth(Main, 5000m, 5000m);
        for (var unit = 1; unit <= 8; unit++)
        {
            ReadBoth(AddSubMeter(unit), 100m, 100m);
        }

        var result = Reconcile();

        Assert.True(result.IsComplete);
        Assert.Equal(0m, result.MainKwh);
        Assert.Equal(0m, result.Difference);
        Assert.Null(result.DifferencePercent);
    }

    [Fact]
    public void Reconcile_ZeroMainConsumptionButSubMetersUsedPower_HasANegativeDifferenceAndNoPercent()
    {
        ReadBoth(Main, 5000m, 5000m);
        ReadBoth(AddSubMeter(1), 100m, 105m);

        var result = Reconcile();

        Assert.Equal(-5m, result.Difference);
        Assert.Null(result.DifferencePercent);
    }

    [Fact]
    public void Reconcile_KeepsDecimalPrecision()
    {
        ReadBoth(Main, 1000.125m, 1010.375m);
        ReadBoth(AddSubMeter(1), 10.001m, 20.002m);

        var result = Reconcile();

        Assert.Equal(10.250m, result.MainKwh);
        Assert.Equal(10.001m, result.SubMeterKwh);
        Assert.Equal(0.249m, result.Difference);
    }

    // Incomplete data

    [Fact]
    public void Reconcile_ASubMeterWithoutReadings_IsIncompleteHasNoDifferenceAndNamesTheMeter()
    {
        ReadBoth(Main, 5000m, 5800m);
        for (var unit = 1; unit <= 7; unit++)
        {
            ReadBoth(AddSubMeter(unit), 100m, 197m);
        }

        var withoutReadings = AddSubMeter(8);

        var result = Reconcile();

        Assert.False(result.IsComplete);
        Assert.Null(result.Difference);
        Assert.Null(result.DifferencePercent);
        Assert.Equal(679m, result.SubMeterKwh);
        var missing = Assert.Single(result.MissingMeters);
        Assert.Equal(withoutReadings.Id, missing.Meter.Id);
        Assert.Equal(MeterConsumptionState.NoReadings, missing.Consumption.State);
    }

    [Fact]
    public void Reconcile_ASubMeterIsNotAssumedToBeZeroEvenWhenItsChargerIsNotInUse()
    {
        ReadBoth(Main, 5000m, 5100m);
        ReadBoth(AddSubMeter(1), 100m, 200m);
        AddSubMeter(2, ChargerStatus.NotInUse);

        var result = Reconcile();

        Assert.False(result.IsComplete);
        Assert.Null(result.Difference);
        Assert.Single(result.MissingMeters);
    }

    [Fact]
    public void Reconcile_ASubMeterMissingOnlyTheEndReading_IsMarkedMissingEnd()
    {
        ReadBoth(Main, 5000m, 5100m);
        var meter = AddSubMeter(1);
        Read(meter, Start, 100m);

        var result = Reconcile();

        Assert.False(result.IsComplete);
        Assert.Equal(MeterConsumptionState.MissingEnd, Assert.Single(result.MissingMeters).Consumption.State);
    }

    [Fact]
    public void Reconcile_MainMeterWithoutReadingsNearTheEnds_IsIncompleteEvenIfAllSubMetersAreDocumented()
    {
        Read(Main, Start.AddDays(10), 5000m);
        FillEightSubMeters(97m);

        var result = Reconcile();

        Assert.False(result.IsComplete);
        Assert.Null(result.MainKwh);
        Assert.Null(result.Difference);
        Assert.Equal(Main.Id, Assert.Single(result.MissingMeters).Meter.Id);
        Assert.Equal(776m, result.SubMeterKwh);
    }

    [Fact]
    public void Reconcile_NoSubMetersAtAll_IsIncomplete()
    {
        ReadBoth(Main, 5000m, 5100m);

        var result = Reconcile();

        Assert.False(result.IsComplete);
        Assert.Null(result.Difference);
    }

    [Fact]
    public void Reconcile_NoMainMeter_IsIncompleteWithoutFailing()
    {
        var onlySubMeters = new List<Meter>(_meters.Where(meter => !meter.IsMain));
        var charger = new EvCharger(1, 1, new EvChargerDetails(), Created);
        charger.AssignId(1);
        var sub = Meter.CreateSub(1, 1, "Undermåler", 1, Created);
        sub.AssignId(2);
        onlySubMeters.Add(sub);

        var result = ChargingReconciler.Reconcile(onlySubMeters, [charger], [], Start, End, Tolerance);

        Assert.False(result.IsComplete);
        Assert.Null(result.Main);
        Assert.Null(result.Difference);
    }

    // Different reading times

    [Fact]
    public void Reconcile_MetersReadOnDifferentTimesWithinTheTolerance_AreComparable()
    {
        Read(Main, Start, 5000m);
        Read(Main, End, 5800m);
        for (var unit = 1; unit <= 8; unit++)
        {
            var meter = AddSubMeter(unit);
            Read(meter, Start.AddHours(unit - 4), 100m);
            Read(meter, End.AddHours(4 - unit), 197m);
        }

        var result = Reconcile();

        Assert.True(result.IsComplete);
        Assert.Equal(24m, result.Difference);
    }

    [Fact]
    public void Reconcile_ASubMeterReadMoreThanTheToleranceAway_IsNotComparable()
    {
        ReadBoth(Main, 5000m, 5800m);
        for (var unit = 1; unit <= 7; unit++)
        {
            ReadBoth(AddSubMeter(unit), 100m, 197m);
        }

        var late = AddSubMeter(8);
        Read(late, Start.AddDays(3), 100m);
        Read(late, End.AddDays(3), 197m);

        var result = Reconcile();

        Assert.False(result.IsComplete);
        Assert.Equal(MeterConsumptionState.MissingBoth, Assert.Single(result.MissingMeters).Consumption.State);
    }

    [Fact]
    public void Reconcile_ALargerToleranceMakesTheSameDataComparable()
    {
        ReadBoth(Main, 5000m, 5800m);
        var late = AddSubMeter(1);
        Read(late, Start.AddDays(3), 100m);
        Read(late, End.AddDays(3), 197m);

        Assert.False(Reconcile().IsComplete);
        Assert.True(Reconcile(tolerance: TimeSpan.FromDays(4)).IsComplete);
    }

    [Fact]
    public void Reconcile_TheLinesShowWhichReadingsWereUsed()
    {
        ReadBoth(Main, 5000m, 5800m);
        var meter = AddSubMeter(1);
        Read(meter, Start.AddHours(3), 100m);
        Read(meter, End.AddHours(-2), 197m);

        var line = Reconcile().SubMeters.Single();

        Assert.Equal(Start.AddHours(3), line.Consumption.StartReading!.ReadAt);
        Assert.Equal(End.AddHours(-2), line.Consumption.EndReading!.ReadAt);
    }

    // Deactivated chargers

    [Fact]
    public void Reconcile_ADisabledChargerWithHistoricalReadingsInThePeriodCountsItsDocumentedConsumption()
    {
        ReadBoth(Main, 5000m, 5300m);
        ReadBoth(AddSubMeter(1, ChargerStatus.TemporarilyDisabled), 100m, 160m);
        ReadBoth(AddSubMeter(2), 100m, 200m);

        var result = Reconcile();

        Assert.True(result.IsComplete);
        Assert.Equal(160m, result.SubMeterKwh);
        Assert.Equal(140m, result.Difference);
    }

    [Fact]
    public void Reconcile_ADisabledChargerWithNoReadingsInThePeriodMakesItIncomplete_EvenIfItHasOlderReadings()
    {
        ReadBoth(Main, 5000m, 5300m);
        var disabled = AddSubMeter(1, ChargerStatus.TemporarilyDisabled);
        Read(disabled, Start.AddDays(-90), 100m);
        Read(disabled, Start.AddDays(-60), 160m);
        ReadBoth(AddSubMeter(2), 100m, 200m);

        var result = Reconcile();

        Assert.False(result.IsComplete);
        Assert.Equal(disabled.Id, Assert.Single(result.MissingMeters).Meter.Id);
    }

    // Periods

    [Fact]
    public void MainMeterPeriods_AreTheIntervalsBetweenConsecutiveMainReadings()
    {
        Read(Main, Start, 5000m);
        Read(Main, End, 5800m);
        Read(Main, End.AddDays(30), 6500m);

        var periods = ChargingReconciler.MainMeterPeriods(_readings);

        Assert.Equal(2, periods.Count);
        Assert.Equal(new ReconciliationPeriod(Start, End), periods[0]);
        Assert.Equal(new ReconciliationPeriod(End, End.AddDays(30)), periods[1]);
    }

    [Fact]
    public void MainMeterPeriods_FewerThanTwoReadings_GiveNoPeriods()
    {
        Assert.Empty(ChargingReconciler.MainMeterPeriods([]));
        Read(Main, Start, 5000m);
        Assert.Empty(ChargingReconciler.MainMeterPeriods(_readings));
    }

    [Fact]
    public void MainMeterPeriods_AreSortedEvenWhenRegisteredInAnotherOrder()
    {
        Read(Main, End, 5800m);
        Read(Main, Start, 5000m);

        Assert.Equal(new ReconciliationPeriod(Start, End), Assert.Single(ChargingReconciler.MainMeterPeriods(_readings)));
    }

    [Fact]
    public void Reconcile_ResultCarriesThePeriodAndTolerance()
    {
        var result = Reconcile();

        Assert.Equal(Start, result.From);
        Assert.Equal(End, result.To);
        Assert.Equal(Tolerance, result.Tolerance);
    }
}
