using SameieHub.Domain;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Domain;

/// <summary>
/// The physical set-up: one main meter and eight sub meters in two fuse cabinets (four in each), all read
/// by hand and registered together.
/// </summary>
public class ChargingReadingSetTests
{
    // 2026-11-18 11:30 in Norway.
    private static readonly DateTime Now = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

    private readonly FakeHousingUnitRepository _housingUnits = new();
    private readonly ManualTimeProvider _time = new(Now);
    private readonly HousingUnitService _unitService;
    private readonly ChargingService _service;
    private readonly List<EvCharger> _chargers = [];
    private readonly List<Meter> _subMeters = [];
    private Meter _main = null!;

    public ChargingReadingSetTests()
    {
        _unitService = new HousingUnitService(_housingUnits);
        _service = new ChargingService(
            new FakeChargingFacilityRepository(),
            new FakeEvChargerRepository(),
            new FakeMeterRepository(),
            new FakeMeterReadingRepository(),
            _housingUnits,
            _time);
    }

    /// <summary>Sets up the facility with a main meter and eight chargers, four in each cabinet.</summary>
    private async Task SetUpAsync()
    {
        await _service.SetUpFacilityAsync("Felles ladeanlegg");
        _main = (await _service.GetMainMeterAsync())!;

        for (var number = 1; number <= 8; number++)
        {
            var unit = await _unitService.RegisterAsync($"Fiktivdalen {number}");
            var charger = await _service.RegisterChargerAsync(unit.Id, new EvChargerDetails { ChargerNumber = $"L{number}" }, number <= 4 ? 1 : 2);
            _chargers.Add(charger);
            _subMeters.Add((await _service.GetSubMeterAsync(charger.Id))!);
        }
    }

    private static DateTime At(int daysAgo, int hour = 8) => Now.Date.AddDays(-daysAgo).AddHours(hour);

    private MeterReadingSetDetails Set(int daysAgo, decimal? main, decimal?[] subs, string? notes = null)
    {
        var values = new Dictionary<int, decimal?>();
        for (var index = 0; index < subs.Length; index++)
        {
            values[_subMeters[index].Id] = subs[index];
        }

        return new MeterReadingSetDetails { ReadAt = At(daysAgo), MainMeterKwh = main, SubMeterKwh = values, Notes = notes };
    }

    private static decimal?[] AllSubs(decimal value) => Enumerable.Repeat((decimal?)value, 8).ToArray();

    private static decimal?[] SubsPlus(decimal start, decimal each) => Enumerable.Repeat((decimal?)(start + each), 8).ToArray();

    // Cabinets

    [Fact]
    public async Task SubMeters_SitInCabinet1Or2_FourInEach()
    {
        await SetUpAsync();

        Assert.Equal(4, _subMeters.Count(meter => meter.CabinetNumber == 1));
        Assert.Equal(4, _subMeters.Count(meter => meter.CabinetNumber == 2));
        Assert.Null(_main.CabinetNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task RegisterCharger_CabinetOtherThan1Or2_IsRejected(int cabinet)
    {
        await _service.SetUpFacilityAsync("Ladeanlegg");
        var unit = await _unitService.RegisterAsync("Fiktivdalen 1");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterChargerAsync(unit.Id, new EvChargerDetails(), cabinet));

        Assert.Equal("CabinetNumber", exception.ParamName);
        Assert.Empty(await _service.GetChargersAsync());
        Assert.Single(await _service.GetMetersAsync());
    }

    [Fact]
    public async Task ChangeCabinetNumber_CorrectsTheSubMeter()
    {
        await SetUpAsync();

        await _service.ChangeCabinetNumberAsync(_chargers[0].Id, 2);

        Assert.Equal(2, (await _service.GetSubMeterAsync(_chargers[0].Id))!.CabinetNumber);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ChangeCabinetNumberAsync(_chargers[0].Id, 3));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeCabinetNumberAsync(999, 1));
    }

    [Fact]
    public void MainMeter_CannotGetACabinetNumber()
    {
        var main = Meter.CreateMain(1, "Hovedmåler", Now);

        Assert.Throws<InvalidOperationException>(() => main.ChangeCabinetNumber(1));
    }

    // The first registration is a start value

    [Fact]
    public async Task RegisterReadingSet_FirstSet_IsStartValuesForAllNineMetersWithoutConsumption()
    {
        await SetUpAsync();

        var result = await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));

        Assert.Equal(At(60), result.ReadAt);
        Assert.Equal(9, result.Lines.Count);
        Assert.True(result.Lines[0].Meter.IsMain);
        Assert.All(result.Lines, line => Assert.True(line.IsStartValue));
        Assert.All(result.Lines, line => Assert.Null(line.ConsumptionKwh));
        Assert.Empty(result.MetersNotRead);
        Assert.Null(result.Reconciliation);
    }

    [Fact]
    public async Task RegisterReadingSet_StoresOneReadingPerMeterAllWithTheSameTimeAndTheRegistrationTime()
    {
        await SetUpAsync();

        await _service.RegisterReadingSetAsync(Set(30, 5000m, AllSubs(100m), notes: "Avlest i skap 1 og 2"));

        var mainReading = Assert.Single(await _service.GetReadingsAsync(_main.Id));
        Assert.Equal(5000m, mainReading.Kwh);
        Assert.Equal(At(30), mainReading.ReadAt);
        Assert.Equal(Now, mainReading.RegisteredAt);
        Assert.Equal("Avlest i skap 1 og 2", mainReading.Notes);
        foreach (var meter in _subMeters)
        {
            var reading = Assert.Single(await _service.GetReadingsAsync(meter.Id));
            Assert.Equal(At(30), reading.ReadAt);
            Assert.Equal(100m, reading.Kwh);
        }
    }

    // Later sets: consumption since the previous reading, difference in kWh and percent

    [Fact]
    public async Task RegisterReadingSet_SecondSet_GivesConsumptionPerMeterAndTheDifference()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));

        var result = await _service.RegisterReadingSetAsync(Set(30, 5800m, SubsPlus(100m, 97m)));

        Assert.All(result.Lines, line => Assert.False(line.IsStartValue));
        Assert.Equal(800m, result.Lines[0].ConsumptionKwh);
        Assert.All(result.Lines.Skip(1), line => Assert.Equal(97m, line.ConsumptionKwh));
        var reconciliation = result.Reconciliation!;
        Assert.True(reconciliation.IsComplete);
        Assert.Equal(800m, reconciliation.MainKwh);
        Assert.Equal(776m, reconciliation.SubMeterKwh);
        Assert.Equal(24m, reconciliation.Difference);
        Assert.Equal(3m, reconciliation.DifferencePercent);
        Assert.Equal(At(60), reconciliation.From);
        Assert.Equal(At(30), reconciliation.To);
    }

    [Fact]
    public async Task RegisterReadingSet_ConsumptionIsSinceThePreviousReadingNotSinceTheStart()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(90, 5000m, AllSubs(100m)));
        await _service.RegisterReadingSetAsync(Set(60, 5800m, SubsPlus(100m, 97m)));

        var third = await _service.RegisterReadingSetAsync(Set(30, 6500m, SubsPlus(197m, 85m)));

        Assert.Equal(700m, third.Lines[0].ConsumptionKwh);
        Assert.All(third.Lines.Skip(1), line => Assert.Equal(85m, line.ConsumptionKwh));
        Assert.Equal(At(60), third.Lines[0].PreviousReading!.ReadAt);
    }

    [Fact]
    public async Task RegisterReadingSet_AccumulatedMeterValuesAreNeverAddedUpAsConsumption()
    {
        await SetUpAsync();
        var start = new decimal?[] { 1000m, 2000m, 3000m, 4000m, 5000m, 6000m, 7000m, 8000m };
        var end = new decimal?[] { 1010m, 2010m, 3010m, 4010m, 5010m, 6010m, 7010m, 8010m };
        await _service.RegisterReadingSetAsync(Set(60, 90000m, start));

        var result = await _service.RegisterReadingSetAsync(Set(30, 90100m, end));

        Assert.Equal(80m, result.Reconciliation!.SubMeterKwh);
        Assert.Equal(20m, result.Reconciliation.Difference);
    }

    [Fact]
    public async Task RegisterReadingSet_SubMetersAddUpToMoreThanTheMainMeter_GivesNegativeDifferenceAndPercent()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));

        var result = await _service.RegisterReadingSetAsync(Set(30, 5700m, SubsPlus(100m, 90m)));

        Assert.Equal(-20m, result.Reconciliation!.Difference);
        Assert.True(result.Reconciliation.DifferencePercent < 0);
    }

    [Fact]
    public async Task RegisterReadingSet_NoConsumptionAnywhere_IsZeroNotMissingAndHasNoPercent()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));

        var result = await _service.RegisterReadingSetAsync(Set(30, 5000m, AllSubs(100m)));

        Assert.True(result.Reconciliation!.IsComplete);
        Assert.Equal(0m, result.Reconciliation.Difference);
        Assert.Null(result.Reconciliation.DifferencePercent);
        Assert.All(result.Lines, line => Assert.Equal(0m, line.ConsumptionKwh));
    }

    // Partly filled sets: missing is not zero

    [Fact]
    public async Task RegisterReadingSet_ASubMeterLeftBlank_IsNotReadAndNotStoredAsZero()
    {
        await SetUpAsync();
        var subs = AllSubs(100m);
        subs[3] = null;

        var result = await _service.RegisterReadingSetAsync(Set(30, 5000m, subs));

        Assert.Equal(_subMeters[3].Id, Assert.Single(result.MetersNotRead).Id);
        var line = result.Lines.Single(line => line.Meter.Id == _subMeters[3].Id);
        Assert.False(line.WasRead);
        Assert.Null(line.Reading);
        Assert.Empty(await _service.GetReadingsAsync(_subMeters[3].Id));
        Assert.Equal(8, result.Lines.Count(line => line.WasRead));
    }

    [Fact]
    public async Task RegisterReadingSet_ASubMeterAbsentFromTheDictionary_IsAlsoNotRead()
    {
        await SetUpAsync();
        var details = new MeterReadingSetDetails
        {
            ReadAt = At(30),
            MainMeterKwh = 5000m,
            SubMeterKwh = new Dictionary<int, decimal?> { [_subMeters[0].Id] = 100m }
        };

        var result = await _service.RegisterReadingSetAsync(details);

        Assert.Equal(7, result.MetersNotRead.Count);
        Assert.Equal(2, result.Lines.Count(line => line.WasRead));
    }

    [Fact]
    public async Task RegisterReadingSet_ExplicitZeroIsARealReading()
    {
        await SetUpAsync();
        var subs = AllSubs(100m);
        subs[0] = 0m;

        var result = await _service.RegisterReadingSetAsync(Set(30, 5000m, subs));

        Assert.Equal(0m, Assert.Single(await _service.GetReadingsAsync(_subMeters[0].Id)).Kwh);
        Assert.Empty(result.MetersNotRead);
    }

    [Fact]
    public async Task RegisterReadingSet_APartlyFilledLaterSet_GivesAnIncompleteReconciliationWithoutADifference()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));
        var subs = SubsPlus(100m, 97m);
        subs[5] = null;

        var result = await _service.RegisterReadingSetAsync(Set(30, 5800m, subs));

        var reconciliation = result.Reconciliation!;
        Assert.False(reconciliation.IsComplete);
        Assert.Null(reconciliation.Difference);
        Assert.Null(reconciliation.DifferencePercent);
        Assert.Equal(679m, reconciliation.SubMeterKwh);
        var missing = Assert.Single(reconciliation.MissingMeters);
        Assert.Equal(_subMeters[5].Id, missing.Meter.Id);
        Assert.Equal(MeterConsumptionState.MissingEnd, missing.Consumption.State);
    }

    [Fact]
    public async Task RegisterReadingSet_MainMeterLeftBlank_HasNoReconciliationButTheSubMetersAreStored()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));

        var result = await _service.RegisterReadingSetAsync(Set(30, null, SubsPlus(100m, 97m)));

        Assert.Null(result.Reconciliation);
        Assert.Equal(_main.Id, Assert.Single(result.MetersNotRead).Id);
        Assert.Single(await _service.GetReadingsAsync(_main.Id));
        Assert.Equal(2, (await _service.GetReadingsAsync(_subMeters[0].Id)).Count);
    }

    [Fact]
    public async Task RegisterReadingSet_NoValueAtAll_IsRejected()
    {
        await SetUpAsync();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(Set(30, null, new decimal?[8])));

        Assert.Equal("SubMeterKwh", exception.ParamName);
    }

    [Fact]
    public async Task RegisterReadingSet_ASetCanBeCompletedLaterWithTheSameTime()
    {
        await SetUpAsync();
        var subs = AllSubs(100m);
        subs[2] = null;
        subs[6] = null;
        await _service.RegisterReadingSetAsync(Set(30, 5000m, subs));

        var completion = new MeterReadingSetDetails
        {
            ReadAt = At(30),
            SubMeterKwh = new Dictionary<int, decimal?> { [_subMeters[2].Id] = 101m, [_subMeters[6].Id] = 102m }
        };
        var result = await _service.RegisterReadingSetAsync(completion);

        Assert.Equal(2, result.Lines.Count(line => line.WasRead));
        Assert.Equal(101m, Assert.Single(await _service.GetReadingsAsync(_subMeters[2].Id)).Kwh);
        Assert.Single(await _service.GetReadingsAsync(_main.Id));
    }

    // Atomic: all or nothing

    [Fact]
    public async Task RegisterReadingSet_OneInvalidValue_StoresNothingForAnyMeter()
    {
        await SetUpAsync();
        var subs = AllSubs(100m);
        subs[4] = -1m;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(Set(30, 5000m, subs)));

        Assert.Equal("SubMeterKwh", exception.ParamName);
        Assert.Empty(await _service.GetReadingsAsync(_main.Id));
        foreach (var meter in _subMeters)
        {
            Assert.Empty(await _service.GetReadingsAsync(meter.Id));
        }
    }

    [Fact]
    public async Task RegisterReadingSet_InvalidMainValue_StoresNothingAndNamesTheMainMeter()
    {
        await SetUpAsync();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(Set(30, 10.1234m, AllSubs(100m))));

        Assert.Equal("MainMeterKwh", exception.ParamName);
        Assert.Empty(await _service.GetReadingsAsync(_subMeters[0].Id));
    }

    [Fact]
    public async Task RegisterReadingSet_OneValueLowerThanThePreviousReading_StoresNothing()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));
        var subs = SubsPlus(100m, 97m);
        subs[1] = 99m;

        var exception = await Assert.ThrowsAsync<MeterReadingSequenceException>(() => _service.RegisterReadingSetAsync(Set(30, 5800m, subs)));

        Assert.True(exception.IsLowerThanEarlier);
        Assert.Single(await _service.GetReadingsAsync(_main.Id));
        Assert.Single(await _service.GetReadingsAsync(_subMeters[0].Id));
    }

    [Fact]
    public async Task RegisterReadingSet_ASubMeterIdThatIsNotASubMeter_IsRejected()
    {
        await SetUpAsync();
        var details = new MeterReadingSetDetails
        {
            ReadAt = At(30),
            MainMeterKwh = 5000m,
            SubMeterKwh = new Dictionary<int, decimal?> { [_main.Id] = 1m }
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(details));

        Assert.Equal("SubMeterKwh", exception.ParamName);
        Assert.Empty(await _service.GetReadingsAsync(_main.Id));
    }

    [Fact]
    public async Task RegisterReadingSet_UnknownSubMeterId_IsRejected()
    {
        await SetUpAsync();
        var details = new MeterReadingSetDetails
        {
            ReadAt = At(30),
            MainMeterKwh = 5000m,
            SubMeterKwh = new Dictionary<int, decimal?> { [999] = 1m }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(details));
    }

    // Coinciding times

    [Fact]
    public async Task RegisterReadingSet_ATimeThatAlreadyHasAReadingForAMeterInTheSet_IsRejectedAndNothingIsStored()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(30, 5000m, AllSubs(100m)));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(Set(30, 5000m, AllSubs(100m))));

        Assert.Equal("ReadAt", exception.ParamName);
        Assert.Single(await _service.GetReadingsAsync(_main.Id));
    }

    [Fact]
    public async Task RegisterReadingSet_TheSameDayButAnotherTime_IsAnotherSet()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(30, 5000m, AllSubs(100m)));
        var evening = new MeterReadingSetDetails { ReadAt = At(30, hour: 20), MainMeterKwh = 5010m, SubMeterKwh = Set(30, null, SubsPlus(100m, 1m)).SubMeterKwh };

        await _service.RegisterReadingSetAsync(evening);

        Assert.Equal(2, (await _service.GetReadingsAsync(_main.Id)).Count);
    }

    [Fact]
    public async Task RegisterReadingSet_InTheFuture_IsRejected()
    {
        await SetUpAsync();
        var details = Set(30, 5000m, AllSubs(100m)) with { ReadAt = Now.AddHours(1) };

        Assert.Equal("ReadAt", (await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(details))).ParamName);
    }

    [Fact]
    public async Task RegisterReadingSet_NonUtcTime_IsRejected()
    {
        await SetUpAsync();
        var details = Set(30, 5000m, AllSubs(100m)) with { ReadAt = DateTime.SpecifyKind(At(30), DateTimeKind.Local) };

        Assert.Equal("ReadAt", (await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterReadingSetAsync(details))).ParamName);
    }

    [Fact]
    public async Task RegisterReadingSet_WithoutAFacility_IsRejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RegisterReadingSetAsync(
            new MeterReadingSetDetails { ReadAt = At(1), MainMeterKwh = 1m }));
    }

    // Different reading times between the meters

    [Fact]
    public async Task Reconcile_MetersReadHoursApartAreComparableWithinTheTolerance()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));
        await _service.RegisterReadingAsync(_main.Id, new MeterReadingDetails { ReadAt = At(30), Kwh = 5800m });
        for (var index = 0; index < 8; index++)
        {
            await _service.RegisterReadingAsync(_subMeters[index].Id, new MeterReadingDetails { ReadAt = At(30, hour: 8 + index), Kwh = 197m });
        }

        var result = await _service.ReconcileLatestPeriodAsync();

        Assert.True(result!.IsComplete);
        Assert.Equal(24m, result.Difference);
    }

    [Fact]
    public async Task Reconcile_SubMetersReadDaysAfterTheMainMeter_AreNotComparable()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));
        await _service.RegisterReadingAsync(_main.Id, new MeterReadingDetails { ReadAt = At(30), Kwh = 5800m });
        foreach (var meter in _subMeters)
        {
            await _service.RegisterReadingAsync(meter.Id, new MeterReadingDetails { ReadAt = At(27), Kwh = 197m });
        }

        var result = await _service.ReconcileLatestPeriodAsync();

        Assert.False(result!.IsComplete);
        Assert.Null(result.Difference);
        Assert.Equal(8, result.MissingMeters.Count);
    }

    // Latest period

    [Fact]
    public async Task ReconcileLatestPeriod_FewerThanTwoMainReadings_IsNull()
    {
        await SetUpAsync();
        Assert.Null(await _service.ReconcileLatestPeriodAsync());

        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));

        Assert.Null(await _service.ReconcileLatestPeriodAsync());
    }

    [Fact]
    public async Task ReconcileLatestPeriod_UsesTheLastTwoMainReadings()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(90, 5000m, AllSubs(100m)));
        await _service.RegisterReadingSetAsync(Set(60, 5800m, SubsPlus(100m, 97m)));
        await _service.RegisterReadingSetAsync(Set(30, 6500m, SubsPlus(197m, 85m)));

        var result = await _service.ReconcileLatestPeriodAsync();

        Assert.Equal(At(60), result!.From);
        Assert.Equal(At(30), result.To);
        Assert.Equal(700m, result.MainKwh);
        Assert.Equal(680m, result.SubMeterKwh);
        Assert.Equal(20m, result.Difference);
    }

    // Corrections

    [Fact]
    public async Task CorrectReading_OfASubMeterInASet_ChangesTheDifferenceOfThePeriod()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));
        var subs = SubsPlus(100m, 97m);
        subs[2] = 179m;
        await _service.RegisterReadingSetAsync(Set(30, 5800m, subs));
        var wrong = (await _service.GetReadingsAsync(_subMeters[2].Id))[1];
        var before = await _service.ReconcileLatestPeriodAsync();
        Assert.Equal(24m + 18m, before!.Difference);

        await _service.CorrectReadingAsync(wrong.Id, new MeterReadingDetails { ReadAt = wrong.ReadAt, Kwh = 197m, Notes = "Rettet: feil avlest" });

        var after = await _service.ReconcileLatestPeriodAsync();
        Assert.Equal(24m, after!.Difference);
        Assert.Equal(3m, after.DifferencePercent);
    }

    [Fact]
    public async Task CorrectReading_AMainMeterReadingThatBreaksTheChronology_IsRejected()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));
        await _service.RegisterReadingSetAsync(Set(30, 5800m, SubsPlus(100m, 97m)));
        var latest = (await _service.GetReadingsAsync(_main.Id))[1];

        await Assert.ThrowsAsync<MeterReadingSequenceException>(() =>
            _service.CorrectReadingAsync(latest.Id, new MeterReadingDetails { ReadAt = latest.ReadAt, Kwh = 4999m }));
    }

    // Charger status never blocks reading or registering a sub meter

    [Theory]
    [InlineData(ChargerStatus.NotInUse)]
    [InlineData(ChargerStatus.TemporarilyDisabled)]
    public async Task RegisterReadingSet_ChargersThatAreNotInUse_StillGetTheirSubMeterRead(ChargerStatus status)
    {
        await SetUpAsync();
        await _service.ChangeChargerStatusAsync(_chargers[3].Id, status);
        await _service.RegisterReadingSetAsync(Set(60, 5000m, AllSubs(100m)));

        var result = await _service.RegisterReadingSetAsync(Set(30, 5800m, SubsPlus(100m, 97m)));

        Assert.True(result.Reconciliation!.IsComplete);
        Assert.Equal(2, (await _service.GetReadingsAsync(_subMeters[3].Id)).Count);
        Assert.Equal(97m, result.Lines.Single(line => line.Meter.Id == _subMeters[3].Id).ConsumptionKwh);
    }

    [Fact]
    public async Task DisabledCharger_KeepsItsHistoryAndANotReadSubMeterIsStillMissingNotZero()
    {
        await SetUpAsync();
        await _service.RegisterReadingSetAsync(Set(90, 5000m, AllSubs(100m)));
        await _service.ChangeChargerStatusAsync(_chargers[0].Id, ChargerStatus.TemporarilyDisabled);
        var subs = SubsPlus(100m, 97m);
        subs[0] = null;

        var result = await _service.RegisterReadingSetAsync(Set(60, 5800m, subs));

        Assert.Single(await _service.GetReadingsAsync(_subMeters[0].Id));
        Assert.False(result.Reconciliation!.IsComplete);
        Assert.Null(result.Reconciliation.Difference);
        Assert.Equal(_subMeters[0].Id, Assert.Single(result.Reconciliation.MissingMeters).Meter.Id);
    }
}
