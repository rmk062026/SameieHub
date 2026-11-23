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

    // Structure

    // Readings

    // Reconciliations

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
