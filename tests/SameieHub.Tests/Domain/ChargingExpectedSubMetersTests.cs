using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

/// <summary>
/// The facility has eight sub meters, one for each of eight chargers. A reconciliation is never complete just
/// because all the sub meters that happen to be configured have readings.
/// </summary>
public class ChargingExpectedSubMetersTests
{
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tolerance = TimeSpan.FromHours(24);

    private readonly List<Meter> _meters = [];
    private readonly List<EvCharger> _chargers = [];
    private readonly List<MeterReading> _readings = [];
    private int _nextReadingId = 1;

    private Meter AddMain()
    {
        var meter = Meter.CreateMain(1, "Hovedmåler", Created);
        meter.AssignId(_meters.Count + 1);
        _meters.Add(meter);

        return meter;
    }

    private Meter AddSub(int unitId, EvCharger? sharedCharger = null)
    {
        var charger = sharedCharger;
        if (charger == null)
        {
            charger = new EvCharger(1, unitId, new EvChargerDetails { ChargerNumber = $"L{unitId}" }, Created);
            charger.AssignId(_chargers.Count + 1);
            _chargers.Add(charger);
        }

        var meter = Meter.CreateSub(1, charger.Id, $"Undermåler {unitId}", unitId <= 4 ? 1 : 2, Created);
        meter.AssignId(_meters.Count + 1);
        _meters.Add(meter);

        return meter;
    }

    private void Read(Meter meter, decimal atStart, decimal atEnd)
    {
        foreach (var (time, kwh) in new[] { (Start, atStart), (End, atEnd) })
        {
            var reading = new MeterReading(meter.Id, new MeterReadingDetails { ReadAt = time, Kwh = kwh }, Created);
            reading.AssignId(_nextReadingId++);
            _readings.Add(reading);
        }
    }

    /// <summary>A facility with a documented main meter and the given number of documented sub meters.</summary>
    private ChargingReconciliation ReconcileFacilityWith(int subMeterCount)
    {
        Read(AddMain(), 5000m, 5800m);
        for (var unit = 1; unit <= subMeterCount; unit++)
        {
            Read(AddSub(unit), 100m, 197m);
        }

        return ChargingReconciler.Reconcile(_meters, _chargers, _readings, Start, End, Tolerance);
    }

    [Fact]
    public void Reconcile_EightDocumentedSubMeters_IsCompleteAndNotApproximate()
    {
        var result = ReconcileFacilityWith(8);

        Assert.True(result.IsComplete);
        Assert.True(result.HasAllExpectedSubMeters);
        Assert.False(result.HasApproximateReadings);
        Assert.Equal(8, result.ExpectedSubMeterCount);
        Assert.Equal(8, result.ConfiguredSubMeterCount);
        Assert.Equal(24m, result.Difference);
        Assert.Equal(3m, result.DifferencePercent);
        Assert.Empty(result.MissingMeters);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(6)]
    [InlineData(1)]
    [InlineData(0)]
    public void Reconcile_FewerThanEightSubMeters_IsNeverCompleteEvenIfEveryConfiguredOneHasReadings(int configured)
    {
        var result = ReconcileFacilityWith(configured);

        Assert.False(result.IsComplete);
        Assert.False(result.HasAllExpectedSubMeters);
        Assert.Equal(configured, result.ConfiguredSubMeterCount);
        Assert.Equal(8 - configured, result.MissingSubMeterCount);
        Assert.Null(result.Difference);
        Assert.Null(result.DifferencePercent);
        Assert.Empty(result.MissingMeters);
    }

    [Fact]
    public void Reconcile_SevenSubMeters_StillShowsTheConsumptionOfTheSevenButNoDifference()
    {
        var result = ReconcileFacilityWith(7);

        Assert.Equal(679m, result.SubMeterKwh);
        Assert.Equal(800m, result.MainKwh);
        Assert.Null(result.Difference);
    }

    [Fact]
    public void Reconcile_NoMainMeter_IsIncompleteEvenWithEightDocumentedSubMeters()
    {
        for (var unit = 1; unit <= 8; unit++)
        {
            Read(AddSub(unit), 100m, 197m);
        }

        var result = ChargingReconciler.Reconcile(_meters, _chargers, _readings, Start, End, Tolerance);

        Assert.False(result.IsComplete);
        Assert.Null(result.Main);
        Assert.Null(result.Difference);
        Assert.Equal(8, result.ConfiguredSubMeterCount);
    }

    [Fact]
    public void Reconcile_MainMeterWithoutReadings_IsIncompleteAndListedAsMissing()
    {
        AddMain();
        for (var unit = 1; unit <= 8; unit++)
        {
            Read(AddSub(unit), 100m, 197m);
        }

        var result = ChargingReconciler.Reconcile(_meters, _chargers, _readings, Start, End, Tolerance);

        Assert.False(result.IsComplete);
        Assert.True(result.Main!.Meter.IsMain);
        Assert.Equal(MeterConsumptionState.NoReadings, Assert.Single(result.MissingMeters).Consumption.State);
    }

    [Fact]
    public void Reconcile_TwoSubMetersForTheSameCharger_CountAsOneSoTheFacilityIsIncomplete()
    {
        Read(AddMain(), 5000m, 5800m);
        for (var unit = 1; unit <= 7; unit++)
        {
            Read(AddSub(unit), 100m, 197m);
        }

        Read(AddSub(7, sharedCharger: _chargers[6]), 100m, 197m);

        var result = ChargingReconciler.Reconcile(_meters, _chargers, _readings, Start, End, Tolerance);

        Assert.Equal(8, result.SubMeters.Count);
        Assert.Equal(7, result.ConfiguredSubMeterCount);
        Assert.False(result.HasAllExpectedSubMeters);
        Assert.False(result.IsComplete);
        Assert.Null(result.Difference);
    }

    [Fact]
    public void Reconcile_ASubMeterWhoseChargerIsUnknown_DoesNotCountAsAConfiguredSubMeter()
    {
        Read(AddMain(), 5000m, 5800m);
        for (var unit = 1; unit <= 7; unit++)
        {
            Read(AddSub(unit), 100m, 197m);
        }

        var orphan = Meter.CreateSub(1, 99, "Undermåler uten lader", 2, Created);
        orphan.AssignId(_meters.Count + 1);
        _meters.Add(orphan);
        Read(orphan, 100m, 197m);

        var result = ChargingReconciler.Reconcile(_meters, _chargers, _readings, Start, End, Tolerance);

        Assert.Equal(7, result.ConfiguredSubMeterCount);
        Assert.False(result.IsComplete);
    }

    [Fact]
    public void Reconcile_MoreThanEightSubMeters_IsAConfigurationErrorAndNotComplete()
    {
        var result = ReconcileFacilityWith(9);

        Assert.False(result.IsComplete);
        Assert.False(result.HasAllExpectedSubMeters);
        Assert.Equal(9, result.ConfiguredSubMeterCount);
        Assert.Equal(0, result.MissingSubMeterCount);
    }

    [Fact]
    public void Reconcile_EightSubMetersWhereOneHasNoReading_IsIncompleteBecauseAReadingIsMissing()
    {
        Read(AddMain(), 5000m, 5800m);
        for (var unit = 1; unit <= 7; unit++)
        {
            Read(AddSub(unit), 100m, 197m);
        }

        AddSub(8);

        var result = ChargingReconciler.Reconcile(_meters, _chargers, _readings, Start, End, Tolerance);

        Assert.True(result.HasAllExpectedSubMeters);
        Assert.False(result.IsComplete);
        Assert.Single(result.MissingMeters);
    }

    [Fact]
    public void Reconcile_ExpectedCountCanBeGivenExplicitlyForOtherFacilities()
    {
        Read(AddMain(), 5000m, 5300m);
        Read(AddSub(1), 100m, 200m);

        var result = ChargingReconciler.Reconcile(_meters, _chargers, _readings, Start, End, Tolerance, expectedSubMeters: 1);

        Assert.True(result.IsComplete);
        Assert.Equal(200m, result.Difference);
    }

    [Fact]
    public async Task ReconcileAsync_UsesTheFacilityRuleOfEightSubMeters()
    {
        var housingUnits = new Fakes.FakeHousingUnitRepository();
        var meters = new Fakes.FakeMeterRepository();
        var readings = new Fakes.FakeMeterReadingRepository();
        var clock = new Fakes.ManualTimeProvider(new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc));
        var service = new ChargingService(new Fakes.FakeChargingFacilityRepository(meters), new Fakes.FakeEvChargerRepository(meters), meters, readings, housingUnits, clock);
        var units = new HousingUnitService(housingUnits);
        await service.SetUpFacilityAsync("Ladeanlegg");
        var main = (await service.GetMainMeterAsync())!;
        var from = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        await service.RegisterReadingAsync(main.Id, new MeterReadingDetails { ReadAt = from, Kwh = 5000m });
        await service.RegisterReadingAsync(main.Id, new MeterReadingDetails { ReadAt = to, Kwh = 5800m });
        for (var number = 1; number <= 7; number++)
        {
            var unit = await units.RegisterAsync($"Fiktivdalen {number}");
            var charger = await service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1);
            var sub = (await service.GetSubMeterAsync(charger.Id))!;
            await service.RegisterReadingAsync(sub.Id, new MeterReadingDetails { ReadAt = from, Kwh = 100m });
            await service.RegisterReadingAsync(sub.Id, new MeterReadingDetails { ReadAt = to, Kwh = 197m });
        }

        var result = await service.ReconcileAsync(from, to);

        Assert.False(result.IsComplete);
        Assert.Equal(1, result.MissingSubMeterCount);
    }
}
