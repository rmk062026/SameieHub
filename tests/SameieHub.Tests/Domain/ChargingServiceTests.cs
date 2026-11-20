using SameieHub.Domain;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Domain;

public class ChargingServiceTests
{
    // 2026-11-18 11:30 in Norway.
    private static readonly DateTime Now = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

    private readonly FakeHousingUnitRepository _housingUnits = new();
    private readonly FakeMeterReadingRepository _readings = new();
    private readonly ManualTimeProvider _time = new(Now);
    private readonly HousingUnitService _unitService;
    private readonly ChargingService _service;

    public ChargingServiceTests()
    {
        _unitService = new HousingUnitService(_housingUnits);
        _service = new ChargingService(
            new FakeChargingFacilityRepository(),
            new FakeEvChargerRepository(),
            new FakeMeterRepository(),
            _readings,
            _housingUnits,
            _time);
    }

    private async Task<HousingUnit> NewUnitAsync(string address = "Fiktivdalen 10A")
    {
        return await _unitService.RegisterAsync(address);
    }

    private async Task<(EvCharger Charger, Meter Meter)> NewChargerAsync(string address = "Fiktivdalen 10A", EvChargerDetails? details = null, int cabinet = 1)
    {
        var unit = await NewUnitAsync(address);
        var charger = await _service.RegisterChargerAsync(unit.Id, details ?? new EvChargerDetails { ChargerNumber = "L1" }, cabinet);
        var meter = (await _service.GetSubMeterAsync(charger.Id))!;

        return (charger, meter);
    }

    private static MeterReadingDetails Reading(decimal kwh, int daysAgo, int hour = 8, string? notes = null)
    {
        return new MeterReadingDetails { ReadAt = Now.Date.AddDays(-daysAgo).AddHours(hour), Kwh = kwh, Notes = notes };
    }

    // Facility, hierarchy

    [Fact]
    public async Task SetUpFacility_CreatesTheFacilityAndOneMainMeter()
    {
        var facility = await _service.SetUpFacilityAsync("  Felles ladeanlegg ");

        Assert.Equal("Felles ladeanlegg", facility.Name);
        Assert.True(facility.Id > 0);
        Assert.Equal(facility.Id, (await _service.GetFacilityAsync())!.Id);
        var main = await _service.GetMainMeterAsync();
        Assert.Equal(MeterKind.Main, main!.Kind);
        Assert.Equal(facility.Id, main.ChargingFacilityId);
        Assert.Equal(ChargingService.MainMeterName, main.Name);
        Assert.Single(await _service.GetMetersAsync());
    }

    [Fact]
    public async Task SetUpFacility_Twice_IsRejectedAndThereIsStillOneMainMeter()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");

        await Assert.ThrowsAsync<ChargingFacilityAlreadyExistsException>(() => _service.SetUpFacilityAsync("Annet"));

        Assert.Single(await _service.GetMetersAsync());
    }

    [Fact]
    public async Task SetUpFacility_EmptyName_IsRejected()
    {
        Assert.Equal("Name", (await Assert.ThrowsAsync<ArgumentException>(() => _service.SetUpFacilityAsync(" "))).ParamName);
        Assert.Null(await _service.GetFacilityAsync());
    }

    [Fact]
    public async Task RegisterCharger_WithoutAFacility_IsRejected()
    {
        var unit = await NewUnitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1));
    }

    [Fact]
    public async Task RegisterCharger_CreatesTheChargerAndItsSubMeterForTheRightHousingUnit()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var unit = await NewUnitAsync("Fiktivdalen 12A");

        var charger = await _service.RegisterChargerAsync(unit.Id, new EvChargerDetails { ChargerNumber = "L3", Model = "EL-7" }, 2);

        Assert.Equal(unit.Id, charger.HousingUnitId);
        Assert.Equal("EL-7", charger.Model);
        var subMeter = await _service.GetSubMeterAsync(charger.Id);
        Assert.Equal(MeterKind.Sub, subMeter!.Kind);
        Assert.Equal(charger.Id, subMeter.EvChargerId);
        Assert.Contains("Fiktivdalen 12A", subMeter.Name);
        Assert.Equal(charger.Id, (await _service.GetChargerForHousingUnitAsync(unit.Id))!.Id);
    }

    [Fact]
    public async Task RegisterCharger_EightUnits_GivesOneMainMeterAndEightSubMeters()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        for (var number = 1; number <= 8; number++)
        {
            var unit = await NewUnitAsync($"Fiktivdalen {number}A");
            await _service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1);
        }

        var meters = await _service.GetMetersAsync();
        var chargers = await _service.GetChargersAsync();

        Assert.Equal(9, meters.Count);
        Assert.Single(meters, meter => meter.IsMain);
        Assert.Equal(8, meters.Count(meter => !meter.IsMain));
        Assert.Equal(8, chargers.Count);
        Assert.Equal(chargers.Select(charger => charger.Id), meters.Where(meter => !meter.IsMain).Select(meter => meter.EvChargerId!.Value));
    }

    [Fact]
    public async Task RegisterCharger_SecondChargerForTheSameUnit_IsRejectedAndNoExtraMeterIsCreated()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var unit = await NewUnitAsync();
        await _service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1);

        await Assert.ThrowsAsync<EvChargerAlreadyExistsException>(() => _service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1));

        Assert.Single(await _service.GetChargersAsync());
        Assert.Equal(2, (await _service.GetMetersAsync()).Count);
    }

    [Fact]
    public async Task RegisterCharger_UnknownHousingUnit_IsRejected()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterChargerAsync(999, new EvChargerDetails(), 1));

        Assert.Equal("HousingUnitId", exception.ParamName);
        Assert.Empty(await _service.GetChargersAsync());
    }

    [Fact]
    public async Task RegisterCharger_InvalidValue_StoresNothing()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var unit = await NewUnitAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterChargerAsync(unit.Id, new EvChargerDetails { Model = new string('A', 101) }, 1));

        Assert.Empty(await _service.GetChargersAsync());
        Assert.Single(await _service.GetMetersAsync());
    }

    [Fact]
    public async Task RegisterCharger_InactiveHousingUnit_IsAllowedBecauseTheChargerIsAPhysicalThing()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var unit = await NewUnitAsync();
        await _unitService.DeactivateAsync(unit.Id);

        var charger = await _service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1);

        Assert.Equal(unit.Id, charger.HousingUnitId);
    }

    // Status and details

    [Fact]
    public async Task ChangeChargerStatus_AllThreeStatusesCanBeSet()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (charger, _) = await NewChargerAsync();

        foreach (var status in new[] { ChargerStatus.NotInUse, ChargerStatus.TemporarilyDisabled, ChargerStatus.InUse })
        {
            await _service.ChangeChargerStatusAsync(charger.Id, status);

            Assert.Equal(status, (await _service.GetChargerAsync(charger.Id))!.Status);
        }
    }

    [Fact]
    public async Task ChangeChargerStatus_DoesNotChangeTheHousingUnit()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (charger, _) = await NewChargerAsync();

        await _service.ChangeChargerStatusAsync(charger.Id, ChargerStatus.TemporarilyDisabled);

        Assert.True((await _unitService.GetByIdAsync(charger.HousingUnitId))!.IsActive);
    }

    [Fact]
    public async Task ChangeChargerStatus_UnknownCharger_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeChargerStatusAsync(999, ChargerStatus.NotInUse));
    }

    [Fact]
    public async Task UpdateChargerDetails_ChangesInformationAndKeepsTheHousingUnit()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (charger, _) = await NewChargerAsync();
        _time.Advance(TimeSpan.FromHours(2));

        await _service.UpdateChargerDetailsAsync(charger.Id, new EvChargerDetails
        {
            ChargerNumber = "L9",
            Manufacturer = "Fiktiv Lade",
            Model = "Pro 11",
            Status = ChargerStatus.NotInUse,
            HasElectricVehicle = false
        });

        var stored = (await _service.GetChargerAsync(charger.Id))!;
        Assert.Equal("L9", stored.ChargerNumber);
        Assert.Equal(ChargerStatus.NotInUse, stored.Status);
        Assert.False(stored.HasElectricVehicle);
        Assert.Equal(charger.HousingUnitId, stored.HousingUnitId);
        Assert.Equal(Now.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateChargerDetails_InvalidValue_KeepsTheStoredCharger()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (charger, _) = await NewChargerAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateChargerDetailsAsync(charger.Id, new EvChargerDetails { ChargerNumber = "Ny", Notes = new string('A', 2001) }));

        Assert.Equal("L1", (await _service.GetChargerAsync(charger.Id))!.ChargerNumber);
    }

    // Registering readings

    [Fact]
    public async Task RegisterReading_FirstReadingIsTheStartValueAndSetsTheRegistrationTime()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();

        var reading = await _service.RegisterReadingAsync(meter.Id, Reading(1240.5m, daysAgo: 30, notes: "  Startverdi "));

        Assert.Equal(meter.Id, reading.MeterId);
        Assert.Equal(1240.5m, reading.Kwh);
        Assert.Equal(Now.Date.AddDays(-30).AddHours(8), reading.ReadAt);
        Assert.Equal(Now, reading.RegisteredAt);
        Assert.Equal("Startverdi", reading.Notes);
        Assert.Empty(MeterHistory.BuildIntervals(await _service.GetReadingsAsync(meter.Id)));
    }

    [Fact]
    public async Task RegisterReading_LaterReadings_GiveConsumptionFromTheDifference()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();

        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        await _service.RegisterReadingAsync(meter.Id, Reading(1182.4m, 30));
        await _service.RegisterReadingAsync(meter.Id, Reading(1388.15m, 0, hour: 7));

        var intervals = MeterHistory.BuildIntervals(await _service.GetReadingsAsync(meter.Id));
        Assert.Equal([182.4m, 205.75m], intervals.Select(interval => interval.Kwh));
    }

    [Fact]
    public async Task RegisterReading_TheMainMeterAndTheSubMetersHaveSeparateHistories()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, sub) = await NewChargerAsync();
        var main = (await _service.GetMainMeterAsync())!;

        await _service.RegisterReadingAsync(main.Id, Reading(90000m, 30));
        await _service.RegisterReadingAsync(sub.Id, Reading(100m, 30));
        await _service.RegisterReadingAsync(main.Id, Reading(90500m, 0));

        Assert.Equal(2, (await _service.GetReadingsAsync(main.Id)).Count);
        Assert.Single(await _service.GetReadingsAsync(sub.Id));
    }

    [Fact]
    public async Task RegisterReading_SameKwhAsBefore_IsAllowedAndMeansZeroConsumption()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(85m, 60));

        await _service.RegisterReadingAsync(meter.Id, Reading(85m, 0));

        Assert.Equal(0m, MeterHistory.TotalDocumented(await _service.GetReadingsAsync(meter.Id)));
    }

    [Fact]
    public async Task RegisterReading_LowerThanThePreviousReading_IsRejectedAndNothingIsStored()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 30));

        var exception = await Assert.ThrowsAsync<MeterReadingSequenceException>(() => _service.RegisterReadingAsync(meter.Id, Reading(999.999m, 0)));

        Assert.True(exception.IsLowerThanEarlier);
        Assert.Equal(1000m, exception.NeighbourKwh);
        Assert.Single(await _service.GetReadingsAsync(meter.Id));
    }

    [Fact]
    public async Task RegisterReading_BackdatedBetweenTwoReadings_MustFitBetweenThem()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        await _service.RegisterReadingAsync(meter.Id, Reading(1200m, 0));

        await _service.RegisterReadingAsync(meter.Id, Reading(1100m, 30));
        await Assert.ThrowsAsync<MeterReadingSequenceException>(() => _service.RegisterReadingAsync(meter.Id, Reading(1300m, 45)));
        await Assert.ThrowsAsync<MeterReadingSequenceException>(() => _service.RegisterReadingAsync(meter.Id, Reading(900m, 45)));

        Assert.Equal([1000m, 1100m, 1200m], (await _service.GetReadingsAsync(meter.Id)).Select(reading => reading.Kwh));
    }

    [Fact]
    public async Task RegisterReading_ReadingsAreReturnedInReadTimeOrderNotRegistrationOrder()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1200m, 0));
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        await _service.RegisterReadingAsync(meter.Id, Reading(1100m, 30));

        Assert.Equal([1000m, 1100m, 1200m], (await _service.GetReadingsAsync(meter.Id)).Select(reading => reading.Kwh));
    }

    [Fact]
    public async Task RegisterReading_SameTimeAsAnotherReading_IsRejectedAsDuplicate()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 30));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingAsync(meter.Id, Reading(1000m, 30)));

        Assert.Equal("ReadAt", exception.ParamName);
        Assert.Single(await _service.GetReadingsAsync(meter.Id));
    }

    [Fact]
    public async Task RegisterReading_SameDayButAnotherTime_IsAllowed()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 30, hour: 8));

        await _service.RegisterReadingAsync(meter.Id, Reading(1001m, 30, hour: 20));

        Assert.Equal(2, (await _service.GetReadingsAsync(meter.Id)).Count);
    }

    [Fact]
    public async Task RegisterReading_SameTimeOnAnotherMeter_IsNotADuplicate()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, sub) = await NewChargerAsync();
        var main = (await _service.GetMainMeterAsync())!;
        await _service.RegisterReadingAsync(main.Id, Reading(5000m, 30));

        await _service.RegisterReadingAsync(sub.Id, Reading(100m, 30));

        Assert.Single(await _service.GetReadingsAsync(sub.Id));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public async Task RegisterReading_NegativeKwh_IsRejected(double kwh)
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingAsync(meter.Id, Reading((decimal)kwh, 1)));

        Assert.Equal("Kwh", exception.ParamName);
    }

    [Fact]
    public async Task RegisterReading_InTheFuture_IsRejected()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegisterReadingAsync(meter.Id, new MeterReadingDetails { ReadAt = Now.AddMinutes(1), Kwh = 10m }));

        Assert.Equal("ReadAt", exception.ParamName);
        Assert.Empty(await _service.GetReadingsAsync(meter.Id));
    }

    [Fact]
    public async Task RegisterReading_RightNow_IsAllowed()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();

        await _service.RegisterReadingAsync(meter.Id, new MeterReadingDetails { ReadAt = Now, Kwh = 10m });

        Assert.Single(await _service.GetReadingsAsync(meter.Id));
    }

    [Fact]
    public async Task RegisterReading_UnknownMeter_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RegisterReadingAsync(999, Reading(1m, 1)));
    }

    [Fact]
    public async Task RegisterReading_ForAChargerThatIsNotInUse_IsAllowedBecauseTheHistoryContinues()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (charger, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(100m, 60));
        await _service.ChangeChargerStatusAsync(charger.Id, ChargerStatus.TemporarilyDisabled);

        await _service.RegisterReadingAsync(meter.Id, Reading(100m, 0));

        Assert.Equal(2, (await _service.GetReadingsAsync(meter.Id)).Count);
    }

    // Corrections and deletion

    [Fact]
    public async Task CorrectReading_ChangesTheValueAndTheConsumptionIsCalculatedAgain()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        var wrong = await _service.RegisterReadingAsync(meter.Id, Reading(1118m, 30));
        await _service.RegisterReadingAsync(meter.Id, Reading(1300m, 0));
        Assert.Equal([118m, 182m], MeterHistory.BuildIntervals(await _service.GetReadingsAsync(meter.Id)).Select(interval => interval.Kwh));
        _time.Advance(TimeSpan.FromDays(1));

        await _service.CorrectReadingAsync(wrong.Id, Reading(1181m, 30, notes: "Rettet fra 1118"));

        var readings = await _service.GetReadingsAsync(meter.Id);
        Assert.Equal([181m, 119m], MeterHistory.BuildIntervals(readings).Select(interval => interval.Kwh));
        var corrected = readings[1];
        Assert.Equal("Rettet fra 1118", corrected.Notes);
        Assert.Equal(Now, corrected.RegisteredAt);
        Assert.Equal(Now.AddDays(1), corrected.UpdatedAt);
    }

    [Fact]
    public async Task CorrectReading_CanMoveTheReadTimeAsLongAsTheChronologyHolds()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        var reading = await _service.RegisterReadingAsync(meter.Id, Reading(1100m, 30));
        await _service.RegisterReadingAsync(meter.Id, Reading(1200m, 0));

        await _service.CorrectReadingAsync(reading.Id, Reading(1100m, 40));

        Assert.Equal(Now.Date.AddDays(-40).AddHours(8), (await _service.GetReadingsAsync(meter.Id))[1].ReadAt);
        await Assert.ThrowsAsync<MeterReadingSequenceException>(() => _service.CorrectReadingAsync(reading.Id, Reading(1100m, 70)));
    }

    [Fact]
    public async Task CorrectReading_ToAValueThatBreaksTheChronology_IsRejectedAndTheStoredReadingIsUnchanged()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        var middle = await _service.RegisterReadingAsync(meter.Id, Reading(1100m, 30));
        await _service.RegisterReadingAsync(meter.Id, Reading(1200m, 0));

        await Assert.ThrowsAsync<MeterReadingSequenceException>(() => _service.CorrectReadingAsync(middle.Id, Reading(1250m, 30)));
        await Assert.ThrowsAsync<MeterReadingSequenceException>(() => _service.CorrectReadingAsync(middle.Id, Reading(900m, 30)));

        Assert.Equal(1100m, (await _service.GetReadingsAsync(meter.Id))[1].Kwh);
    }

    [Fact]
    public async Task CorrectReading_KeepingItsOwnTimeIsNotADuplicate()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        var reading = await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 30));

        await _service.CorrectReadingAsync(reading.Id, Reading(1005m, 30));

        Assert.Equal(1005m, Assert.Single(await _service.GetReadingsAsync(meter.Id)).Kwh);
    }

    [Fact]
    public async Task CorrectReading_ToTheTimeOfAnotherReading_IsRejectedAsDuplicate()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        var second = await _service.RegisterReadingAsync(meter.Id, Reading(1100m, 30));

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CorrectReadingAsync(second.Id, Reading(1100m, 60)));
    }

    [Fact]
    public async Task CorrectReading_InvalidValue_ChangesNothing()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        var reading = await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 30));

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CorrectReadingAsync(reading.Id, Reading(-5m, 30)));

        Assert.Equal(1000m, (await _service.GetReadingsAsync(meter.Id)).Single().Kwh);
    }

    [Fact]
    public async Task CorrectReading_UnknownReading_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CorrectReadingAsync(999, Reading(1m, 1)));
    }

    [Fact]
    public async Task DeleteReading_RemovesItAndTheOthersStillFollowTheChronology()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (_, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(1000m, 60));
        var wrong = await _service.RegisterReadingAsync(meter.Id, Reading(1100m, 30));
        await _service.RegisterReadingAsync(meter.Id, Reading(1200m, 0));

        await _service.DeleteReadingAsync(wrong.Id);

        var readings = await _service.GetReadingsAsync(meter.Id);
        Assert.Equal([1000m, 1200m], readings.Select(reading => reading.Kwh));
        Assert.Equal(200m, MeterHistory.TotalDocumented(readings));
    }

    [Fact]
    public async Task DeleteReading_UnknownReading_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteReadingAsync(999));
    }

    // History after deactivation

    [Fact]
    public async Task DisabledCharger_KeepsAllItsReadingsAndHistoricalConsumption()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (charger, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(900m, 120));
        await _service.RegisterReadingAsync(meter.Id, Reading(1040m, 90));
        await _service.RegisterReadingAsync(meter.Id, Reading(1205.5m, 60));

        await _service.ChangeChargerStatusAsync(charger.Id, ChargerStatus.TemporarilyDisabled);

        var readings = await _service.GetReadingsAsync(meter.Id);
        Assert.Equal(3, readings.Count);
        Assert.Equal(305.5m, MeterHistory.TotalDocumented(readings));
        Assert.Equal(charger.Id, (await _service.GetSubMeterAsync(charger.Id))!.EvChargerId);
    }

    [Fact]
    public async Task InactiveHousingUnit_DoesNotAffectTheChargerOrItsReadings()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var (charger, meter) = await NewChargerAsync();
        await _service.RegisterReadingAsync(meter.Id, Reading(900m, 30));

        await _unitService.DeactivateAsync(charger.HousingUnitId);

        Assert.Equal(ChargerStatus.InUse, (await _service.GetChargerAsync(charger.Id))!.Status);
        Assert.Single(await _service.GetReadingsAsync(meter.Id));
    }

    // Reconciliation through the service

    [Fact]
    public async Task Reconcile_UsesTheMetersAndReadingsOfTheFacility()
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var main = (await _service.GetMainMeterAsync())!;
        var (_, sub1) = await NewChargerAsync("Fiktivdalen 10A");
        var (_, sub2) = await NewChargerAsync("Fiktivdalen 10B");
        var from = Now.Date.AddDays(-30).AddHours(8);
        var to = Now.Date.AddHours(8);
        await _service.RegisterReadingAsync(main.Id, Reading(5000m, 30));
        await _service.RegisterReadingAsync(main.Id, Reading(5300m, 0));
        await _service.RegisterReadingAsync(sub1.Id, Reading(100m, 30));
        await _service.RegisterReadingAsync(sub1.Id, Reading(220m, 0));
        await _service.RegisterReadingAsync(sub2.Id, Reading(50m, 30, hour: 10));
        await _service.RegisterReadingAsync(sub2.Id, Reading(170m, 0, hour: 6));

        var result = await _service.ReconcileAsync(from, to);

        Assert.True(result.IsComplete);
        Assert.Equal(300m, result.MainKwh);
        Assert.Equal(240m, result.SubMeterKwh);
        Assert.Equal(60m, result.Difference);
        Assert.Equal(20m, result.DifferencePercent);
        Assert.Equal(MeterHistory.DefaultTolerance, result.Tolerance);
    }

    [Fact]
    public async Task Reconcile_WithoutAFacility_GivesAnIncompleteResult()
    {
        var result = await _service.ReconcileAsync(Now.AddDays(-30), Now);

        Assert.False(result.IsComplete);
        Assert.Null(result.Difference);
    }
}
