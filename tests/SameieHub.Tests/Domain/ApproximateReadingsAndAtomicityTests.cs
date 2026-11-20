using SameieHub.Domain;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Domain;

/// <summary>
/// M4E-B: readings found within the tolerance are marked approximate, and the service stores facility+meter,
/// charger+meter and reading sets through repository calls that are all-or-nothing.
/// </summary>
public class ApproximateReadingsAndAtomicityTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Tolerance = TimeSpan.FromHours(24);

    private int _nextId = 1;

    private MeterReading Reading(DateTime readAt, decimal kwh)
    {
        var reading = new MeterReading(1, new MeterReadingDetails { ReadAt = readAt, Kwh = kwh }, Now);
        reading.AssignId(_nextId++);

        return reading;
    }

    // Approximate marking

    [Fact]
    public void ConsumptionBetween_ReadingsExactlyAtTheBoundaries_IsNotApproximate()
    {
        var consumption = MeterHistory.ConsumptionBetween([Reading(Start, 100m), Reading(End, 200m)], Start, End, Tolerance);

        Assert.Equal(MeterConsumptionState.Documented, consumption.State);
        Assert.False(consumption.IsApproximate);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(0, -3)]
    [InlineData(-23, 23)]
    public void ConsumptionBetween_ReadingsNotExactlyAtTheBoundaries_IsApproximate(int startOffsetHours, int endOffsetHours)
    {
        var readings = new[] { Reading(Start.AddHours(startOffsetHours), 100m), Reading(End.AddHours(endOffsetHours), 200m) };

        var consumption = MeterHistory.ConsumptionBetween(readings, Start, End, Tolerance);

        Assert.Equal(MeterConsumptionState.Documented, consumption.State);
        Assert.True(consumption.IsApproximate);
        Assert.Equal(100m, consumption.Kwh);
    }

    [Fact]
    public void ConsumptionBetween_NotDocumented_IsNotMarkedApproximate()
    {
        var consumption = MeterHistory.ConsumptionBetween([Reading(Start, 100m)], Start, End, Tolerance);

        Assert.Equal(MeterConsumptionState.MissingEnd, consumption.State);
        Assert.False(consumption.IsApproximate);
    }

    [Fact]
    public void Reconcile_AllMetersExactlyAtTheBoundaries_HasNoApproximateReadings()
    {
        var result = Reconcile(subMeterOffsetHours: 0);

        Assert.True(result.IsComplete);
        Assert.False(result.HasApproximateReadings);
    }

    [Fact]
    public void Reconcile_OneSubMeterReadAtAnotherTime_MarksTheReconciliationAndThatMeterApproximateButItStaysComplete()
    {
        var result = Reconcile(subMeterOffsetHours: 5);

        Assert.True(result.IsComplete);
        Assert.True(result.HasApproximateReadings);
        Assert.Equal(200m, result.Difference);
        Assert.True(result.SubMeters.Single().Consumption.IsApproximate);
        Assert.False(result.Main!.Consumption.IsApproximate);
    }

    [Fact]
    public void Reconcile_MainMeterNotExactlyAtThePeriodBoundary_IsApproximate()
    {
        var meters = new List<Meter> { MeterWithId(Meter.CreateMain(1, "Hovedmåler", Now), 1) };
        var readings = new List<MeterReading>
        {
            Reading(Start.AddHours(2), 5000m, meterId: 1),
            Reading(End, 5100m, meterId: 1)
        };

        var result = ChargingReconciler.Reconcile(meters, [], readings, Start, End, Tolerance, expectedSubMeters: 0);

        Assert.True(result.Main!.Consumption.IsApproximate);
        Assert.True(result.HasApproximateReadings);
    }

    [Fact]
    public void Reconcile_IncompleteReconciliation_StillShowsWhichDocumentedMetersAreApproximateButNoDifference()
    {
        var meters = new List<Meter>
        {
            MeterWithId(Meter.CreateMain(1, "Hovedmåler", Now), 1),
            MeterWithId(Meter.CreateSub(1, 1, "Undermåler", 1, Now), 2)
        };
        var charger = new EvCharger(1, 1, new EvChargerDetails(), Now);
        charger.AssignId(1);
        var readings = new List<MeterReading>
        {
            Reading(Start, 5000m, meterId: 1),
            Reading(End, 5100m, meterId: 1),
            Reading(Start.AddHours(3), 10m, meterId: 2)
        };

        var result = ChargingReconciler.Reconcile(meters, [charger], readings, Start, End, Tolerance, expectedSubMeters: 1);

        Assert.False(result.IsComplete);
        Assert.Null(result.Difference);
        Assert.False(result.HasApproximateReadings);
    }

    private ChargingReconciliation Reconcile(int subMeterOffsetHours)
    {
        var meters = new List<Meter>
        {
            MeterWithId(Meter.CreateMain(1, "Hovedmåler", Now), 1),
            MeterWithId(Meter.CreateSub(1, 1, "Undermåler", 1, Now), 2)
        };
        var charger = new EvCharger(1, 1, new EvChargerDetails(), Now);
        charger.AssignId(1);
        var readings = new List<MeterReading>
        {
            Reading(Start, 5000m, meterId: 1),
            Reading(End, 5300m, meterId: 1),
            Reading(Start.AddHours(subMeterOffsetHours), 100m, meterId: 2),
            Reading(End.AddHours(subMeterOffsetHours), 200m, meterId: 2)
        };

        return ChargingReconciler.Reconcile(meters, [charger], readings, Start, End, Tolerance, expectedSubMeters: 1);
    }

    private MeterReading Reading(DateTime readAt, decimal kwh, int meterId)
    {
        var reading = new MeterReading(meterId, new MeterReadingDetails { ReadAt = readAt, Kwh = kwh }, Now);
        reading.AssignId(_nextId++);

        return reading;
    }

    private static Meter MeterWithId(Meter meter, int id)
    {
        meter.AssignId(id);

        return meter;
    }

    // Service behavior when the repository fails (the repository itself is all-or-nothing)

    private static (ChargingService Service, FakeEvChargerRepository Chargers, FakeMeterRepository Meters, FakeMeterReadingRepository Readings, HousingUnitService Units)
        CreateService()
    {
        var housingUnits = new FakeHousingUnitRepository();
        var meters = new FakeMeterRepository();
        var chargers = new FakeEvChargerRepository(meters);
        var readings = new FakeMeterReadingRepository();
        var service = new ChargingService(new FakeChargingFacilityRepository(meters), chargers, meters, readings, housingUnits, new ManualTimeProvider(Now));

        return (service, chargers, meters, readings, new HousingUnitService(housingUnits));
    }

    [Fact]
    public async Task RegisterCharger_WhenStoringFails_LeavesNeitherChargerNorMeter()
    {
        var (service, chargers, meters, _, units) = CreateService();
        await service.SetUpFacilityAsync("Ladeanlegg");
        var unit = await units.RegisterAsync("Fiktivdalen 1A");
        chargers.FailOnAdd = true;

        await Assert.ThrowsAsync<IOException>(() => service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1));

        Assert.Empty(await service.GetChargersAsync());
        Assert.Single(await meters.GetAllAsync());
    }

    [Fact]
    public async Task RegisterReadingSet_WhenStoringFails_StoresNoReadingAndTheErrorIsNotHidden()
    {
        var (service, _, _, readings, units) = CreateService();
        await service.SetUpFacilityAsync("Ladeanlegg");
        var unit = await units.RegisterAsync("Fiktivdalen 1A");
        var charger = await service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1);
        var sub = (await service.GetSubMeterAsync(charger.Id))!;
        var main = (await service.GetMainMeterAsync())!;
        readings.FailOnAddRange = true;
        var details = new MeterReadingSetDetails
        {
            ReadAt = Now.AddDays(-1),
            MainMeterKwh = 5000m,
            SubMeterKwh = new Dictionary<int, decimal?> { [sub.Id] = 100m }
        };

        await Assert.ThrowsAsync<IOException>(() => service.RegisterReadingSetAsync(details));

        Assert.Empty(await service.GetReadingsAsync(main.Id));
        Assert.Empty(await service.GetReadingsAsync(sub.Id));
    }
}
