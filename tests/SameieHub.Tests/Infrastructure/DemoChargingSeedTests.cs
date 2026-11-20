using SameieHub.Domain;
using SameieHub.Infrastructure.Demo;

namespace SameieHub.Tests.Infrastructure;

public class DemoChargingSeedTests : IAsyncLifetime
{
    private static readonly string[] UnitNumbers = ["10A", "10B", "12A", "12B", "14A", "14B", "16A", "16B"];

    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private DemoDatabaseSeeder NewSeeder(IMeterReadingRepository? readings = null)
    {
        return new DemoDatabaseSeeder(
            _database.Repository,
            _database.BoardCaseRepository,
            _database.TaskRepository,
            _database.FacilityRepository,
            _database.ChargerRepository,
            _database.MeterRepository,
            readings ?? _database.ReadingRepository,
            _database.Clock);
    }

    private async Task SeedAsync()
    {
        await NewSeeder().SeedAsync();
    }

    private async Task<Dictionary<string, EvCharger>> ChargersByUnitNumberAsync()
    {
        var units = await _database.Service.GetAllAsync();
        var chargers = await _database.Charging.GetChargersAsync();
        var result = new Dictionary<string, EvCharger>();
        foreach (var unit in units)
        {
            var number = unit.Address.Replace(DemoDatabaseSeeder.AddressPrefix, string.Empty);
            result[number] = chargers.Single(charger => charger.HousingUnitId == unit.Id);
        }

        return result;
    }

    private async Task<List<ReconciliationPeriod>> PeriodsAsync()
    {
        var main = (await _database.Charging.GetMainMeterAsync())!;

        return ChargingReconciler.MainMeterPeriods(await _database.Charging.GetReadingsAsync(main.Id));
    }

    private async Task<ChargingReconciliation> ReconcilePeriodAsync(int index)
    {
        var period = (await PeriodsAsync())[index];

        return await _database.Charging.ReconcileAsync(period.From, period.To);
    }

    // Structure

    [Fact]
    public async Task Seed_CreatesOneFacilityOneMainMeterEightChargersAndEightSubMeters()
    {
        await SeedAsync();

        Assert.NotNull(await _database.Charging.GetFacilityAsync());
        var meters = await _database.Charging.GetMetersAsync();
        Assert.Single(meters, meter => meter.IsMain);
        Assert.Equal(8, meters.Count(meter => !meter.IsMain));
        Assert.Equal(8, (await _database.Charging.GetChargersAsync()).Count);
    }

    [Fact]
    public async Task Seed_PutsFourSubMetersInEachCabinetWithTheDemoDistribution()
    {
        await SeedAsync();

        var chargers = await ChargersByUnitNumberAsync();
        var cabinetOne = new[] { "10A", "10B", "12A", "12B" };
        foreach (var number in UnitNumbers)
        {
            var subMeter = (await _database.Charging.GetSubMeterAsync(chargers[number].Id))!;
            Assert.Equal(cabinetOne.Contains(number) ? 1 : 2, subMeter.CabinetNumber);
        }

        var meters = await _database.Charging.GetMetersAsync();
        Assert.Equal(4, meters.Count(meter => meter.CabinetNumber == 1));
        Assert.Equal(4, meters.Count(meter => meter.CabinetNumber == 2));
    }

    [Fact]
    public async Task Seed_ConnectsEachHousingUnitToItsOwnChargerAndSubMeter()
    {
        await SeedAsync();

        var units = await _database.Service.GetAllAsync();
        var chargers = await _database.Charging.GetChargersAsync();
        var meters = await _database.Charging.GetMetersAsync();

        Assert.Equal(8, chargers.Select(charger => charger.HousingUnitId).Distinct().Count());
        foreach (var unit in units)
        {
            var charger = chargers.Single(existing => existing.HousingUnitId == unit.Id);
            Assert.Single(meters, meter => meter.EvChargerId == charger.Id);
            Assert.Contains(unit.UnitNumber!, charger.ChargerNumber);
        }
    }

    [Fact]
    public async Task Seed_GivesAllThreeChargerStatusesAndSomeHouseholdsWithoutAnElectricVehicle()
    {
        await SeedAsync();

        var chargers = await ChargersByUnitNumberAsync();
        Assert.Equal(4, chargers.Values.Count(charger => charger.Status == ChargerStatus.InUse));
        Assert.Equal(3, chargers.Values.Count(charger => charger.Status == ChargerStatus.NotInUse));
        Assert.Single(chargers.Values, charger => charger.Status == ChargerStatus.TemporarilyDisabled);
        Assert.Equal(ChargerStatus.TemporarilyDisabled, chargers["14A"].Status);
        Assert.True(chargers.Values.Count(charger => charger.HasElectricVehicle == false) >= 2);
    }

    [Fact]
    public async Task Seed_ChargerDetailsAreFictional()
    {
        await SeedAsync();

        foreach (var charger in await _database.Charging.GetChargersAsync())
        {
            Assert.StartsWith("Fiktiv", charger.Manufacturer);
            Assert.DoesNotContain("@", charger.Notes ?? string.Empty);
        }
    }

    [Fact]
    public async Task Seed_HousingUnitNotesDoNotContradictTheChargerData()
    {
        await SeedAsync();

        foreach (var unit in await _database.Service.GetAllAsync())
        {
            Assert.Contains("Carport", unit.Notes);
            Assert.DoesNotContain("Elbillader:", unit.Notes);
            Assert.DoesNotContain("midlertidig deaktivert", unit.Notes);
        }
    }

    // Readings

    [Fact]
    public async Task Seed_CreatesFortyFourReadingsAndEveryMeterStartsWithAStartValue()
    {
        await SeedAsync();

        var readings = await _database.Charging.GetAllReadingsAsync();
        Assert.Equal(44, readings.Count);
        foreach (var meter in await _database.Charging.GetMetersAsync())
        {
            var meterReadings = await _database.Charging.GetReadingsAsync(meter.Id);
            Assert.NotEmpty(meterReadings);
            var consumption = MeterHistory.TotalDocumented(meterReadings);
            Assert.NotNull(consumption);
        }
    }

    [Fact]
    public async Task Seed_ReadingsAreAccumulatedAndFollowTheChronologyRules()
    {
        await SeedAsync();

        var now = _database.Clock.GetUtcNow().UtcDateTime;
        foreach (var meter in await _database.Charging.GetMetersAsync())
        {
            var readings = await _database.Charging.GetReadingsAsync(meter.Id);
            for (var index = 0; index < readings.Count; index++)
            {
                Assert.True(readings[index].ReadAt <= now);
                Assert.True(readings[index].RegisteredAt <= now);
                if (index > 0)
                {
                    Assert.True(readings[index].ReadAt > readings[index - 1].ReadAt);
                    Assert.True(readings[index].Kwh >= readings[index - 1].Kwh, "Accumulated values never fall.");
                }
            }
        }
    }

    [Fact]
    public async Task Seed_SpreadsTheReadingsOverTheLastMonthsInFiveRounds()
    {
        await SeedAsync();

        var main = (await _database.Charging.GetMainMeterAsync())!;
        var times = (await _database.Charging.GetReadingsAsync(main.Id)).Select(reading => reading.ReadAt).ToList();
        Assert.Equal(5, times.Count);
        var now = _database.Clock.GetUtcNow().UtcDateTime;
        Assert.InRange((now - times[0]).TotalDays, 90, 150);
        Assert.InRange((now - times[^1]).TotalDays, 1, 10);
    }

    [Fact]
    public async Task Seed_HasAtLeastThreeCompleteRoundsAndOneIncompleteRound()
    {
        await SeedAsync();

        var byTime = (await _database.Charging.GetAllReadingsAsync()).GroupBy(reading => reading.ReadAt).ToList();
        Assert.True(byTime.Count(round => round.Count() == 9) >= 3);
        Assert.Contains(byTime, round => round.Count() < 9);
    }

    [Fact]
    public async Task Seed_ARoundWithMissingReadingsIsCompletedLater()
    {
        await SeedAsync();

        var secondRound = (await _database.Charging.GetAllReadingsAsync())
            .GroupBy(reading => reading.ReadAt)
            .OrderBy(round => round.Key)
            .ElementAt(1)
            .ToList();

        var latest = secondRound.Max(reading => reading.RegisteredAt);
        var earliest = secondRound.Min(reading => reading.RegisteredAt);
        Assert.True(latest > earliest, "One reading of the round was registered later than the others.");
        Assert.Equal(1, secondRound.Count(reading => reading.RegisteredAt == latest));
    }

    // Reconciliations

    [Fact]
    public async Task Seed_HasFourPeriodsBetweenTheMainMeterReadings()
    {
        await SeedAsync();

        Assert.Equal(4, (await PeriodsAsync()).Count);
    }

    [Fact]
    public async Task Seed_TheDocumentedPeriodHas1250MainAnd1220SubMeterKwhWithADifferenceOf30Kwh()
    {
        await SeedAsync();

        var reconciliation = await ReconcilePeriodAsync(2);

        Assert.True(reconciliation.IsComplete);
        Assert.Equal(1250m, reconciliation.MainKwh);
        Assert.Equal(1220m, reconciliation.SubMeterKwh);
        Assert.Equal(30m, reconciliation.Difference);
        Assert.Equal(2.4m, decimal.Round(reconciliation.DifferencePercent!.Value, 1));
        Assert.False(reconciliation.HasApproximateReadings);
    }

    [Fact]
    public async Task Seed_TheLatestPeriodIsCompleteWithASmallPositiveDifference()
    {
        await SeedAsync();

        var reconciliation = await _database.Charging.ReconcileLatestPeriodAsync();

        Assert.NotNull(reconciliation);
        Assert.True(reconciliation.IsComplete);
        Assert.InRange(reconciliation.Difference!.Value, 1m, 50m);
        Assert.InRange(reconciliation.DifferencePercent!.Value, 0.1m, 5m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Seed_TheTwoOldestPeriodsAreIncompleteBecauseOneMeterWasNotRead(int periodIndex)
    {
        await SeedAsync();

        var reconciliation = await ReconcilePeriodAsync(periodIndex);

        Assert.False(reconciliation.IsComplete);
        Assert.Null(reconciliation.Difference);
        var missing = Assert.Single(reconciliation.MissingMeters);
        var chargers = await ChargersByUnitNumberAsync();
        Assert.Equal(chargers["14B"].Id, missing.Charger!.Id);
    }

    [Fact]
    public async Task Seed_TheMissingReadingThatWasCompletedLaterIsDocumentedInTheSecondRound()
    {
        await SeedAsync();

        var chargers = await ChargersByUnitNumberAsync();
        var meter = (await _database.Charging.GetSubMeterAsync(chargers["16A"].Id))!;

        var readings = await _database.Charging.GetReadingsAsync(meter.Id);

        Assert.Equal(5, readings.Count);
    }

    [Fact]
    public async Task Seed_SubMeterConsumptionAddsUpToTheDocumentedSum()
    {
        await SeedAsync();

        var reconciliation = await ReconcilePeriodAsync(2);
        var chargers = await ChargersByUnitNumberAsync();
        var expected = new Dictionary<string, decimal>
        {
            ["10A"] = 280, ["10B"] = 0, ["12A"] = 245, ["12B"] = 0, ["14A"] = 100, ["14B"] = 0, ["16A"] = 390, ["16B"] = 205
        };

        foreach (var (number, kwh) in expected)
        {
            var line = reconciliation.SubMeters.Single(existing => existing.Charger!.Id == chargers[number].Id);
            Assert.Equal(kwh, line.Consumption.Kwh);
        }
    }

    [Fact]
    public async Task Seed_TheDeactivatedChargerKeepsItsHistoricalConsumption()
    {
        await SeedAsync();

        var chargers = await ChargersByUnitNumberAsync();
        Assert.Equal(ChargerStatus.TemporarilyDisabled, chargers["14A"].Status);
        var meter = (await _database.Charging.GetSubMeterAsync(chargers["14A"].Id))!;

        var readings = await _database.Charging.GetReadingsAsync(meter.Id);

        Assert.Equal(5, readings.Count);
        Assert.Equal(400m, readings[^2].Kwh - readings[0].Kwh);
        Assert.Equal(readings[^2].Kwh, readings[^1].Kwh);
    }

    [Fact]
    public async Task Seed_AChargerWithoutAnElectricVehicleHasZeroDocumentedConsumptionInAPeriod()
    {
        await SeedAsync();

        var chargers = await ChargersByUnitNumberAsync();
        Assert.False(chargers["10B"].HasElectricVehicle);
        var reconciliation = await ReconcilePeriodAsync(2);

        var line = reconciliation.SubMeters.Single(existing => existing.Charger!.Id == chargers["10B"].Id);

        Assert.Equal(MeterConsumptionState.Documented, line.Consumption.State);
        Assert.Equal(0m, line.Consumption.Kwh);
    }

    [Fact]
    public async Task Seed_AMeterCanHaveARealStartValueOfZero()
    {
        await SeedAsync();

        var chargers = await ChargersByUnitNumberAsync();
        var meter = (await _database.Charging.GetSubMeterAsync(chargers["12B"].Id))!;

        var first = (await _database.Charging.GetReadingsAsync(meter.Id))[0];

        Assert.Equal(0m, first.Kwh);
    }

    [Fact]
    public async Task Seed_PeriodConsumptionIsCalculatedFromAccumulatedValuesNotStoredAsReadings()
    {
        await SeedAsync();

        var main = (await _database.Charging.GetMainMeterAsync())!;
        var readings = await _database.Charging.GetReadingsAsync(main.Id);

        Assert.All(readings, reading => Assert.True(reading.Kwh > 10000m));
        Assert.Equal(1250m, readings[3].Kwh - readings[2].Kwh);
    }

    // Seeding twice, restart and later use

    [Fact]
    public async Task Seed_Twice_DoesNotDuplicateAnyChargingData()
    {
        await SeedAsync();

        var result = await NewSeeder().SeedAsync();

        Assert.Equal(DemoSeedResult.AlreadySeeded, result);
        Assert.Equal(44, (await _database.Charging.GetAllReadingsAsync()).Count);
        Assert.Equal(8, (await _database.Charging.GetChargersAsync()).Count);
        Assert.Equal(9, (await _database.Charging.GetMetersAsync()).Count);
    }

    [Fact]
    public async Task Seed_DataSurvivesRestartAndIsStillReportedAsComplete()
    {
        await SeedAsync();

        await _database.RestartAsync();

        Assert.Equal(DemoDatabaseState.Complete, await NewSeeder().InspectAsync());
        Assert.Equal(44, (await _database.Charging.GetAllReadingsAsync()).Count);
    }

    [Fact]
    public async Task Seed_ANewReadingSetRegisteredLaterDoesNotMakeTheDemoIncomplete()
    {
        await SeedAsync();
        var meters = await _database.Charging.GetMetersAsync();
        var main = meters.Single(meter => meter.IsMain);
        var last = (await _database.Charging.GetReadingsAsync(main.Id))[^1];
        _database.Clock.Advance(TimeSpan.FromDays(1));
        await _database.Charging.RegisterReadingAsync(main.Id, new MeterReadingDetails { ReadAt = last.ReadAt.AddHours(2), Kwh = last.Kwh + 10 });

        Assert.Equal(DemoDatabaseState.Complete, await NewSeeder().InspectAsync());
        Assert.Equal(DemoSeedResult.AlreadySeeded, await NewSeeder().SeedAsync());
    }

    // States

    [Fact]
    public async Task Inspect_EmptyDatabase_IsEmpty()
    {
        Assert.Equal(DemoDatabaseState.Empty, await NewSeeder().InspectAsync());
    }

    [Fact]
    public async Task Inspect_OnlyAChargingFacility_IsOtherDataAndSeedChangesNothing()
    {
        await _database.Charging.SetUpFacilityAsync("Mitt anlegg");

        Assert.Equal(DemoDatabaseState.OtherData, await NewSeeder().InspectAsync());
        await Assert.ThrowsAsync<DemoDatabaseNotEmptyException>(SeedAsync);
        Assert.Empty(await _database.Service.GetAllAsync());
    }

    [Fact]
    public async Task Seed_OlderDemoWithoutChargingData_IsReportedAsOutdatedAndNothingIsChanged()
    {
        await SeedAsync();
        await RemoveAllChargingDataAsync();
        var unit = (await _database.Service.GetAllAsync()).First();
        await _database.Service.ChangeAddressAsync(unit.Id, "Fiktivdalen 10A");

        Assert.Equal(DemoDatabaseState.OutdatedWithoutCharging, await NewSeeder().InspectAsync());
        var exception = await Assert.ThrowsAsync<DemoDatabaseOutdatedException>(SeedAsync);

        Assert.Contains("reset", exception.Message);
        Assert.Empty(await _database.Charging.GetChargersAsync());
        Assert.Equal(8, (await _database.Service.GetAllAsync()).Count);
        Assert.Equal(20, (await _database.Tasks.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Seed_OlderDemoWithManualEdits_KeepsTheEditsAndIsStillOutdated()
    {
        await SeedAsync();
        await RemoveAllChargingDataAsync();
        var task = (await _database.Tasks.GetAllAsync()).First();
        await _database.Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        await Assert.ThrowsAsync<DemoDatabaseOutdatedException>(SeedAsync);

        Assert.Equal(TaskItemStatus.Completed, (await _database.Tasks.GetByIdAsync(task.Id))!.Status);
    }

    [Fact]
    public async Task Seed_OlderDemoMigratedFromTheSchemaBeforeCharging_IsOutdatedAndStartsNormally()
    {
        await _database.DisposeAsync();
        _database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await _database.MigrateToMigrationAsync("AddTaskItems");
        foreach (var number in UnitNumbers)
        {
            await _database.Service.RegisterAsync($"Fiktivdalen {number}");
        }

        for (var number = 1; number <= 11; number++)
        {
            await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = $"Sak {number}" });
        }

        await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Gjennomgang av ladeanleggets kapasitet" });
        for (var number = 1; number <= 19; number++)
        {
            await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = $"Oppgave {number}" });
        }

        await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Fornye merking av postkasser ved Fiktivdalen 16A" });

        await _database.RestartAsync();

        Assert.Equal(DemoDatabaseState.OutdatedWithoutCharging, await NewSeeder().InspectAsync());
        await Assert.ThrowsAsync<DemoDatabaseOutdatedException>(SeedAsync);
        Assert.Empty(await _database.Charging.GetChargersAsync());
        Assert.Equal(8, (await _database.Service.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Seed_DemoWithADeletedReading_IsReportedAsPartlyCreatedAndNothingIsChanged()
    {
        await SeedAsync();
        var reading = (await _database.Charging.GetAllReadingsAsync()).First();
        await _database.Charging.DeleteReadingAsync(reading.Id);

        Assert.Equal(DemoDatabaseState.PartlyCreated, await NewSeeder().InspectAsync());
        var exception = await Assert.ThrowsAsync<DemoDatabaseIncompleteException>(SeedAsync);

        Assert.Contains("reset", exception.Message);
        Assert.Equal(43, (await _database.Charging.GetAllReadingsAsync()).Count);
    }

    [Fact]
    public async Task Seed_DemoWithACorrectedStartValue_IsNotReportedAsComplete()
    {
        await SeedAsync();
        var main = (await _database.Charging.GetMainMeterAsync())!;
        var first = (await _database.Charging.GetReadingsAsync(main.Id))[0];
        await _database.Charging.CorrectReadingAsync(first.Id, new MeterReadingDetails { ReadAt = first.ReadAt, Kwh = first.Kwh - 1 });

        Assert.Equal(DemoDatabaseState.PartlyCreated, await NewSeeder().InspectAsync());
    }

    [Theory]
    [InlineData("UPDATE EvChargers SET ChargerNumber = 'X' || Id")]
    [InlineData("UPDATE Meters SET CabinetNumber = 1 WHERE CabinetNumber = 2")]
    [InlineData("UPDATE ChargingFacilities SET Name = 'Annet anlegg'")]
    public async Task Seed_RightNumberOfRowsButWrongContent_IsNotReportedAsComplete(string sql)
    {
        await SeedAsync();
        await _database.ExecuteSqlAsync(sql);

        Assert.Equal(DemoDatabaseState.PartlyCreated, await NewSeeder().InspectAsync());
        await Assert.ThrowsAsync<DemoDatabaseIncompleteException>(SeedAsync);
    }

    // Failure while the charging data is created

    [Fact]
    public async Task Seed_FailureWhileCreatingChargingData_IsReportedAsIncompleteWithResetAdvice()
    {
        var failing = new FailingReadingRepository(_database.ReadingRepository, failOnAddRange: 3);

        var exception = await Assert.ThrowsAsync<DemoDatabaseIncompleteException>(() => NewSeeder(failing).SeedAsync());

        Assert.Contains("reset", exception.Message);
        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal(DemoDatabaseState.PartlyCreated, await NewSeeder().InspectAsync());
        await Assert.ThrowsAsync<DemoDatabaseIncompleteException>(SeedAsync);
    }

    // Reset: the dedicated demo file is deleted, then the demo is created again

    [Fact]
    public async Task Reset_DeletingTheDatabaseFileAndMigratingAgain_GivesAFreshCompleteDemoWithoutAnyForeignKeyProblems()
    {
        await SeedAsync();
        var path = _database.DatabasePath;
        await _database.StopForResetAsync();

        File.Delete(path);
        await _database.RestartAsync();

        Assert.Equal(DemoDatabaseState.Empty, await NewSeeder().InspectAsync());
        await SeedAsync();
        Assert.Equal(DemoDatabaseState.Complete, await NewSeeder().InspectAsync());
        Assert.Equal(44, (await _database.Charging.GetAllReadingsAsync()).Count);
    }

    [Fact]
    public async Task Seed_OnlyUsesTheGivenDatabase_NotTheOrdinaryDatabaseLocation()
    {
        var ordinary = SameieHub.Infrastructure.Persistence.DatabaseLocation.GetDefaultPath();
        var existedBefore = File.Exists(ordinary);
        var sizeBefore = existedBefore ? new FileInfo(ordinary).Length : 0;

        await SeedAsync();

        Assert.Equal(existedBefore, File.Exists(ordinary));
        if (existedBefore)
        {
            Assert.Equal(sizeBefore, new FileInfo(ordinary).Length);
        }

        Assert.NotEqual(Path.GetFullPath(ordinary), Path.GetFullPath(_database.DatabasePath));
    }

    private async Task RemoveAllChargingDataAsync()
    {
        await _database.ExecuteSqlAsync("DELETE FROM MeterReadings");
        await _database.ExecuteSqlAsync("DELETE FROM Meters");
        await _database.ExecuteSqlAsync("DELETE FROM EvChargers");
        await _database.ExecuteSqlAsync("DELETE FROM ChargingFacilities");
    }

    private sealed class FailingReadingRepository : IMeterReadingRepository
    {
        private readonly IMeterReadingRepository _inner;
        private readonly int _failOnAddRange;
        private int _addRanges;

        public FailingReadingRepository(IMeterReadingRepository inner, int failOnAddRange)
        {
            _inner = inner;
            _failOnAddRange = failOnAddRange;
        }

        public Task AddAsync(MeterReading reading) => _inner.AddAsync(reading);

        public Task AddRangeAsync(IReadOnlyList<MeterReading> readings)
        {
            _addRanges++;
            if (_addRanges == _failOnAddRange)
            {
                throw new IOException("The database is unavailable.");
            }

            return _inner.AddRangeAsync(readings);
        }

        public Task<MeterReading?> GetByIdAsync(int id) => _inner.GetByIdAsync(id);

        public Task<IReadOnlyList<MeterReading>> GetByMeterIdAsync(int meterId) => _inner.GetByMeterIdAsync(meterId);

        public Task<IReadOnlyList<MeterReading>> GetAllAsync() => _inner.GetAllAsync();

        public Task UpdateAsync(MeterReading reading) => _inner.UpdateAsync(reading);

        public Task<bool> DeleteAsync(int id) => _inner.DeleteAsync(id);
    }
}
