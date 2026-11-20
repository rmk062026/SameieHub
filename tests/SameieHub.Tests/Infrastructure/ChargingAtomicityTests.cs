using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// Rollback is tested with real database failures, not with validation errors. A trigger in the test database
/// makes SQLite itself abort one insert after other rows of the same operation were already inserted. If the
/// operation were not one transaction, those earlier rows would stay behind.
/// </summary>
public class ChargingAtomicityTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private async Task<string?> ScalarAsync(string sql) => await _database.QueryTextAsync(sql);

    private Task FailOnInsertAsync(string table, string condition)
    {
        return _database.ExecuteSqlAsync(
            $"CREATE TRIGGER simulated_failure BEFORE INSERT ON {table} WHEN {condition} "
            + "BEGIN SELECT RAISE(ABORT, 'Simulated database failure'); END;");
    }

    private Task RemoveFailureAsync() => _database.ExecuteSqlAsync("DROP TRIGGER simulated_failure;");

    private async Task AssertNoReadingsAsync()
    {
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
    }

    // Reading sets

    [Fact]
    public async Task ReadingSet_DatabaseFailsOnTheFifthReading_NoneOfTheNineReadingsStay()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        await FailOnInsertAsync("MeterReadings", "CAST(NEW.Kwh AS REAL) = 777");
        var subs = ChargingSetup.Same(100m);
        subs[3] = 777m;

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5000m, subs)));

        Assert.Contains("Simulated database failure", exception.InnerException!.Message);
        await AssertNoReadingsAsync();
    }

    [Fact]
    public async Task ReadingSet_DatabaseFailsOnTheLastReading_NoneStayEvenThoughEightWereInserted()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        await FailOnInsertAsync("MeterReadings", "CAST(NEW.Kwh AS REAL) = 777");
        var subs = ChargingSetup.Same(100m);
        subs[7] = 777m;

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5000m, subs)));

        await AssertNoReadingsAsync();
    }

    [Fact]
    public async Task ReadingSet_AfterAFailedAttempt_TheSameSetCanBeRegisteredCleanlyAndEarlierSetsAreUntouched()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(60, 5000m, ChargingSetup.Same(100m)));
        await FailOnInsertAsync("MeterReadings", "CAST(NEW.Kwh AS REAL) = 777");
        var subs = ChargingSetup.Same(197m);
        subs[5] = 777m;

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5800m, subs)));
        Assert.Equal("9", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));

        await RemoveFailureAsync();
        subs[5] = 197m;
        var result = await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5800m, subs));

        Assert.Equal("18", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
        Assert.Equal(24m, result.Reconciliation!.Difference);
    }

    [Fact]
    public async Task AddRange_OneReadingBreaksAForeignKey_NoneOfTheReadingsStay()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        var readings = new List<MeterReading>();
        foreach (var meter in setup.SubMeters.Take(4))
        {
            readings.Add(new MeterReading(meter.Id, new MeterReadingDetails { ReadAt = ChargingSetup.At(5), Kwh = 10m }, ChargingSetup.Now));
        }

        readings.Add(new MeterReading(999, new MeterReadingDetails { ReadAt = ChargingSetup.At(5), Kwh = 10m }, ChargingSetup.Now));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.ReadingRepository.AddRangeAsync(readings));

        Assert.Equal(787, Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode);
        await AssertNoReadingsAsync();
    }

    [Fact]
    public async Task AddRange_TheSameMeterAndTimeTwiceInOneBatch_IsRefusedAndNothingStays()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        var details = new MeterReadingDetails { ReadAt = ChargingSetup.At(5), Kwh = 10m };
        var readings = new List<MeterReading>
        {
            new(setup.MainMeter.Id, details, ChargingSetup.Now),
            new(setup.SubMeters[0].Id, details, ChargingSetup.Now),
            new(setup.SubMeters[0].Id, details with { Kwh = 11m }, ChargingSetup.Now)
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _database.ReadingRepository.AddRangeAsync(readings));

        Assert.Equal("ReadAt", exception.ParamName);
        await AssertNoReadingsAsync();
    }

    [Fact]
    public async Task AddRange_AlreadyStoredReadingsAreNotAffectedByAFailedBatch()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5000m, 100m));
        var batch = new List<MeterReading>
        {
            new(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = ChargingSetup.At(10), Kwh = 5100m }, ChargingSetup.Now),
            new(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = ChargingSetup.At(30), Kwh = 5200m }, ChargingSetup.Now)
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _database.ReadingRepository.AddRangeAsync(batch));

        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
    }

    // Charger and sub meter

    [Fact]
    public async Task RegisterCharger_DatabaseFailsWhileStoringTheMeter_NoChargerIsLeftWithoutAMeter()
    {
        await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await _database.Service.RegisterAsync("Fiktivdalen 1");
        await FailOnInsertAsync("Meters", "NEW.Kind = 'Sub'");

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1));

        Assert.Contains("Simulated database failure", exception.InnerException!.Message);
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM EvChargers"));
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
        Assert.Null(await _database.Charging.GetChargerForHousingUnitAsync(unit.Id));
    }

    [Fact]
    public async Task RegisterCharger_AfterAFailedAttempt_TheUnitCanStillGetItsCharger()
    {
        await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await _database.Service.RegisterAsync("Fiktivdalen 1");
        await FailOnInsertAsync("Meters", "NEW.Kind = 'Sub'");
        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1));

        await RemoveFailureAsync();
        var charger = await _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1);

        Assert.NotNull(await _database.Charging.GetSubMeterAsync(charger.Id));
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM EvChargers"));
        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
    }

    [Fact]
    public async Task EveryChargerHasExactlyOneSubMeter_AlsoAfterSeveralFailedAttempts()
    {
        await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        var units = new List<HousingUnit>();
        for (var number = 1; number <= 4; number++)
        {
            units.Add(await _database.Service.RegisterAsync($"Fiktivdalen {number}"));
        }

        await _database.Charging.RegisterChargerAsync(units[0].Id, new EvChargerDetails(), 1);
        await FailOnInsertAsync("Meters", "NEW.Kind = 'Sub'");
        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.RegisterChargerAsync(units[1].Id, new EvChargerDetails(), 1));
        await RemoveFailureAsync();
        await _database.Charging.RegisterChargerAsync(units[2].Id, new EvChargerDetails(), 2);
        await _database.Charging.RegisterChargerAsync(units[1].Id, new EvChargerDetails(), 1);

        Assert.Equal("3", await ScalarAsync("SELECT COUNT(*) FROM EvChargers"));
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM EvChargers c WHERE (SELECT COUNT(*) FROM Meters m WHERE m.EvChargerId = c.Id) <> 1"));
    }

    // Facility and main meter

    [Fact]
    public async Task SetUpFacility_DatabaseFailsWhileStoringTheMainMeter_NoFacilityStaysAndTheNextAttemptGetsId1()
    {
        await FailOnInsertAsync("Meters", "NEW.Kind = 'Main'");

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.SetUpFacilityAsync("Ladeanlegg"));

        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM ChargingFacilities"));
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
        Assert.Null(await _database.Charging.GetFacilityAsync());

        // The rolled back attempt also rolled back the id counter, so the one allowed facility can still be created.
        await RemoveFailureAsync();
        var facility = await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        Assert.Equal(1, facility.Id);
        Assert.NotNull(await _database.Charging.GetMainMeterAsync());
    }

    // The transaction really is a database transaction

    [Fact]
    public async Task Rollback_IsDoneByTheDatabase_ReadingsInsertedBeforeTheFailureAreVisibleToNoOne()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 2);
        await FailOnInsertAsync("MeterReadings", "CAST(NEW.Kwh AS REAL) = 777");
        var subs = new decimal?[] { 100m, 777m };

        await Assert.ThrowsAsync<DbUpdateException>(() => _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5000m, subs)));
        await _database.RestartAsync();

        // The main meter reading and the first sub meter reading were inserted before the failure. After a restart
        // (a new connection) they are gone, which only happens if the insert was rolled back.
        await AssertNoReadingsAsync();
        foreach (var meter in await _database.Charging.GetMetersAsync())
        {
            Assert.Empty(await _database.Charging.GetReadingsAsync(meter.Id));
        }
    }
}
