using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class ChargingPersistenceTests : IAsyncLifetime
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

    private static DateTime At(int daysAgo, int hour = 8) => ChargingSetup.At(daysAgo, hour);

    // SQLite does not check foreign keys unless this is switched on for the connection.
    private Task RawAsync(string sql) => _database.ExecuteSqlAsync("PRAGMA foreign_keys = ON; " + sql);

    private async Task<string?> ScalarAsync(string sql) => await _database.QueryTextAsync(sql);

    // Facility, meters, chargers

    [Fact]
    public async Task Facility_IsStoredWithItsMainMeterAndFoundAfterRestart()
    {
        var facility = await _database.Charging.SetUpFacilityAsync("  Felles ladeanlegg ");

        await _database.RestartAsync();

        var found = (await _database.Charging.GetFacilityAsync())!;
        Assert.Equal(facility.Id, found.Id);
        Assert.Equal("Felles ladeanlegg", found.Name);
        Assert.Equal(DateTimeKind.Utc, found.CreatedAt.Kind);
        Assert.Equal(ChargingSetup.Now, found.CreatedAt);
        var main = (await _database.Charging.GetMainMeterAsync())!;
        Assert.True(main.IsMain);
        Assert.Equal(found.Id, main.ChargingFacilityId);
        Assert.Null(main.EvChargerId);
        Assert.Null(main.CabinetNumber);
    }

    [Fact]
    public async Task Facility_NoneSetUp_IsNull()
    {
        Assert.Null(await _database.Charging.GetFacilityAsync());
        Assert.Null(await _database.Charging.GetMainMeterAsync());
    }

    [Fact]
    public async Task Facility_OnlyOneCanExist_TheServiceRefusesAndSoDoesTheDatabase()
    {
        await _database.Charging.SetUpFacilityAsync("Ladeanlegg");

        await Assert.ThrowsAsync<ChargingFacilityAlreadyExistsException>(() => _database.Charging.SetUpFacilityAsync("Annet"));

        // The service is bypassed: the database itself refuses a second facility, and nothing from the attempt stays.
        var second = new ChargingFacility("Annet", null, ChargingSetup.Now);
        await Assert.ThrowsAsync<ChargingFacilityAlreadyExistsException>(
            () => _database.FacilityRepository.AddWithMainMeterAsync(second, id => Meter.CreateMain(id, "Hovedmåler", ChargingSetup.Now)));
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM ChargingFacilities"));
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
    }

    [Fact]
    public async Task Hierarchy_OneMainMeterAndEightSubMetersInTwoCabinets()
    {
        var setup = await ChargingSetup.CreateAsync(_database);

        await _database.RestartAsync();

        var meters = await _database.Charging.GetMetersAsync();
        Assert.Equal(9, meters.Count);
        Assert.Single(meters, meter => meter.IsMain);
        var subs = meters.Where(meter => !meter.IsMain).ToList();
        Assert.Equal(8, subs.Count);
        Assert.Equal(4, subs.Count(meter => meter.CabinetNumber == 1));
        Assert.Equal(4, subs.Count(meter => meter.CabinetNumber == 2));
        Assert.Equal(setup.Chargers.Select(charger => charger.Id), subs.Select(meter => meter.EvChargerId!.Value));
        Assert.Equal("Main", await ScalarAsync("SELECT Kind FROM Meters WHERE EvChargerId IS NULL"));
        Assert.Equal("8", await ScalarAsync("SELECT COUNT(*) FROM Meters WHERE Kind = 'Sub'"));
    }

    [Fact]
    public async Task Charger_IsStoredWithAllDetailsAndLinkedToTheRightHousingUnit()
    {
        await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await _database.Service.RegisterAsync("Fiktivdalen 12A");
        var registered = await _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails
        {
            ChargerNumber = "L3",
            Manufacturer = "Demo Charge",
            Model = "EL-7",
            Status = ChargerStatus.TemporarilyDisabled,
            HasElectricVehicle = false,
            Notes = "Fiktiv merknad"
        }, 2);

        await _database.RestartAsync();

        var found = (await _database.Charging.GetChargerForHousingUnitAsync(unit.Id))!;
        Assert.Equal(registered.Id, found.Id);
        Assert.Equal("L3", found.ChargerNumber);
        Assert.Equal("Demo Charge", found.Manufacturer);
        Assert.Equal(ChargerStatus.TemporarilyDisabled, found.Status);
        Assert.False(found.HasElectricVehicle);
        Assert.Equal("Fiktiv merknad", found.Notes);
        Assert.Equal(DateTimeKind.Utc, found.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, found.UpdatedAt.Kind);
        Assert.Equal("TemporarilyDisabled", await ScalarAsync("SELECT Status FROM EvChargers"));
        var sub = (await _database.Charging.GetSubMeterAsync(found.Id))!;
        Assert.Equal(2, sub.CabinetNumber);
        Assert.Contains("Fiktivdalen 12A", sub.Name);
    }

    [Fact]
    public async Task Charger_UnknownVehicleIsStoredAsNullNotAsNo()
    {
        await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await _database.Service.RegisterAsync("Fiktivdalen 1A");

        await _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails { HasElectricVehicle = null }, 1);

        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM EvChargers WHERE HasElectricVehicle IS NULL"));
    }

    [Fact]
    public async Task Charger_OnePerHousingUnit_TheServiceRefusesAndSoDoesTheDatabase()
    {
        await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await _database.Service.RegisterAsync("Fiktivdalen 1A");
        await _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1);

        await Assert.ThrowsAsync<EvChargerAlreadyExistsException>(() => _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails(), 1));

        // The service is bypassed: the unique index refuses it, and the rolled back attempt leaves no extra meter.
        var facility = (await _database.Charging.GetFacilityAsync())!;
        var duplicate = new EvCharger(facility.Id, unit.Id, new EvChargerDetails(), ChargingSetup.Now);
        var exception = await Assert.ThrowsAsync<EvChargerAlreadyExistsException>(
            () => _database.ChargerRepository.AddWithSubMeterAsync(duplicate, id => Meter.CreateSub(facility.Id, id, "Undermåler", 1, ChargingSetup.Now)));
        Assert.IsType<DbUpdateException>(exception.InnerException);
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM EvChargers"));
        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
    }

    [Fact]
    public async Task Charger_UnknownHousingUnit_IsRejectedByTheForeignKeyAndNoMeterStays()
    {
        var facility = await _database.Charging.SetUpFacilityAsync("Ladeanlegg");
        var charger = new EvCharger(facility.Id, 999, new EvChargerDetails(), ChargingSetup.Now);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => _database.ChargerRepository.AddWithSubMeterAsync(charger, id => Meter.CreateSub(facility.Id, id, "Undermåler", 1, ChargingSetup.Now)));

        Assert.Equal(787, Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode);
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM EvChargers"));
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
    }

    [Fact]
    public async Task Charger_StatusAndDetailsChangesAreStoredAndUpdatedAtMoves()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        _database.Clock.Advance(TimeSpan.FromHours(3));

        await _database.Charging.ChangeChargerStatusAsync(setup.Chargers[0].Id, ChargerStatus.NotInUse);
        await _database.Charging.ChangeCabinetNumberAsync(setup.Chargers[0].Id, 2);
        await _database.RestartAsync();

        var found = (await _database.Charging.GetChargerAsync(setup.Chargers[0].Id))!;
        Assert.Equal(ChargerStatus.NotInUse, found.Status);
        Assert.Equal(ChargingSetup.Now.AddHours(3), found.UpdatedAt);
        Assert.Equal(2, (await _database.Charging.GetSubMeterAsync(found.Id))!.CabinetNumber);
    }

    [Fact]
    public async Task ChargerStatus_IsIndependentOfTheHousingUnitBeingActive()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);

        await _database.Service.DeactivateAsync(setup.Units[0].Id);
        await _database.RestartAsync();

        Assert.Equal(ChargerStatus.InUse, (await _database.Charging.GetChargerAsync(setup.Chargers[0].Id))!.Status);
    }

    // What the database enforces by itself (raw SQL bypasses the service)

    [Fact]
    public async Task Database_RefusesASecondMainMeterAndASecondSubMeterForTheSameCharger()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);

        await Assert.ThrowsAsync<SqliteException>(() => RawAsync(
            $"INSERT INTO Meters (ChargingFacilityId, Kind, EvChargerId, CabinetNumber, Name, CreatedAt) VALUES ({setup.Facility.Id}, 'Main', NULL, NULL, 'Ekstra', '2026-11-18 10:30:00')"));
        await Assert.ThrowsAsync<SqliteException>(() => RawAsync(
            $"INSERT INTO Meters (ChargingFacilityId, Kind, EvChargerId, CabinetNumber, Name, CreatedAt) VALUES ({setup.Facility.Id}, 'Sub', {setup.Chargers[0].Id}, 1, 'Ekstra', '2026-11-18 10:30:00')"));

        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
    }

    [Theory]
    [InlineData("'Sub'", "{charger}", "0")]
    [InlineData("'Sub'", "{charger}", "3")]
    [InlineData("'Sub'", "{charger}", "NULL")]
    [InlineData("'Sub'", "NULL", "1")]
    [InlineData("'Main'", "NULL", "1")]
    [InlineData("'Other'", "NULL", "NULL")]
    public async Task Database_RefusesMetersThatBreakTheKindCabinetAndChargerRules(string kind, string charger, string cabinet)
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        var extraUnit = await _database.Service.RegisterAsync("Fiktivdalen 9");
        await RawAsync(
            $"INSERT INTO EvChargers (ChargingFacilityId, HousingUnitId, Status, CreatedAt, UpdatedAt) VALUES ({setup.Facility.Id}, {extraUnit.Id}, 'InUse', '2026-11-18 10:30:00', '2026-11-18 10:30:00')");
        var chargerId = await ScalarAsync($"SELECT Id FROM EvChargers WHERE HousingUnitId = {extraUnit.Id}");

        var sql = $"INSERT INTO Meters (ChargingFacilityId, Kind, EvChargerId, CabinetNumber, Name, CreatedAt) "
            + $"VALUES ({setup.Facility.Id}, {kind}, {charger.Replace("{charger}", chargerId!)}, {cabinet}, 'Test', '2026-11-18 10:30:00')";

        await Assert.ThrowsAsync<SqliteException>(() => RawAsync(sql));
    }

    [Fact]
    public async Task Database_RefusesANegativeKwh()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);

        await Assert.ThrowsAsync<SqliteException>(() => RawAsync(
            $"INSERT INTO MeterReadings (MeterId, ReadAt, Kwh, RegisteredAt, UpdatedAt) VALUES ({setup.MainMeter.Id}, '2026-11-01 08:00:00', '-1', '2026-11-18 10:30:00', '2026-11-18 10:30:00')"));
    }

    // Foreign keys and restricted deletion

    [Fact]
    public async Task Reading_ForAnUnknownMeter_IsRejectedByTheForeignKey()
    {
        await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        var reading = new MeterReading(999, new MeterReadingDetails { ReadAt = At(5), Kwh = 10m }, ChargingSetup.Now);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => _database.ReadingRepository.AddAsync(reading));

        Assert.Equal(787, Assert.IsType<SqliteException>(exception.InnerException).SqliteExtendedErrorCode);
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
    }

    [Fact]
    public async Task HousingUnit_WithACharger_CannotBeDeletedAndNothingIsRemoved()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);

        await Assert.ThrowsAsync<HousingUnitInUseException>(() => _database.Service.DeleteAsync(setup.Units[0].Id));

        Assert.NotNull(await _database.Service.GetByIdAsync(setup.Units[0].Id));
        Assert.NotNull(await _database.Charging.GetChargerForHousingUnitAsync(setup.Units[0].Id));
    }

    [Fact]
    public async Task Facility_Charger_AndMeterWithReadings_CannotBeDeletedByTheDatabase()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5000m, 100m));

        await Assert.ThrowsAsync<SqliteException>(() => RawAsync($"DELETE FROM Meters WHERE Id = {setup.SubMeters[0].Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => RawAsync($"DELETE FROM EvChargers WHERE Id = {setup.Chargers[0].Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => RawAsync($"DELETE FROM ChargingFacilities WHERE Id = {setup.Facility.Id}"));
        await Assert.ThrowsAsync<SqliteException>(() => RawAsync($"DELETE FROM HousingUnits WHERE Id = {setup.Units[0].Id}"));

        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
    }

    [Fact]
    public async Task DeletingAReading_NeverDeletesTheMeter()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5000m, 100m));
        var reading = (await _database.Charging.GetReadingsAsync(setup.SubMeters[0].Id)).Single();

        await _database.Charging.DeleteReadingAsync(reading.Id);

        Assert.Empty(await _database.Charging.GetReadingsAsync(setup.SubMeters[0].Id));
        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM Meters"));
    }

    // Readings: storage, decimals, UTC

    [Fact]
    public async Task Reading_IsStoredWithReadTimeAndRegistrationTimeApartAndKeptAfterRestart()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);

        var registered = await _database.Charging.RegisterReadingAsync(
            setup.SubMeters[0].Id, new MeterReadingDetails { ReadAt = At(10, hour: 6), Kwh = 1234.567m, Notes = "  Avlest av styret " });
        await _database.RestartAsync();

        var found = (await _database.Charging.GetReadingsAsync(setup.SubMeters[0].Id)).Single();
        Assert.Equal(registered.Id, found.Id);
        Assert.Equal(At(10, hour: 6), found.ReadAt);
        Assert.Equal(ChargingSetup.Now, found.RegisteredAt);
        Assert.Equal(ChargingSetup.Now, found.UpdatedAt);
        Assert.Equal("Avlest av styret", found.Notes);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.001")]
    [InlineData("0.125")]
    [InlineData("10.5")]
    [InlineData("1234.567")]
    [InlineData("9999999.999")]
    public async Task Kwh_WithUpToThreeDecimals_IsStoredAndReadBackExactly(string value)
    {
        var kwh = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);

        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(10), Kwh = kwh });
        await _database.RestartAsync();

        var found = (await _database.Charging.GetReadingsAsync(setup.MainMeter.Id)).Single();
        Assert.Equal(kwh, found.Kwh);

        // The provider writes the exact decimal text (zero becomes "0.0"), never a floating point number.
        var storedText = (await ScalarAsync("SELECT Kwh FROM MeterReadings"))!;
        Assert.Equal(kwh, decimal.Parse(storedText, System.Globalization.CultureInfo.InvariantCulture));
        Assert.DoesNotContain("E", storedText);
    }

    [Fact]
    public async Task Kwh_IsStoredAsExactTextNotAsAFloatingPointNumber()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(10), Kwh = 0.1m });
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(5), Kwh = 0.3m });

        var readings = await _database.Charging.GetReadingsAsync(setup.MainMeter.Id);

        Assert.Equal("text", await ScalarAsync("SELECT typeof(Kwh) FROM MeterReadings LIMIT 1"));
        Assert.Equal(0.2m, MeterHistory.TotalDocumented(readings));
    }

    [Fact]
    public async Task Times_AreUtcAfterReadingAndStoredWithoutTimeZoneShift()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingAsync(
            setup.MainMeter.Id, new MeterReadingDetails { ReadAt = new DateTime(2026, 11, 1, 6, 15, 0, DateTimeKind.Utc), Kwh = 5m });

        await _database.RestartAsync();

        var found = (await _database.Charging.GetReadingsAsync(setup.MainMeter.Id)).Single();
        Assert.Equal(DateTimeKind.Utc, found.ReadAt.Kind);
        Assert.Equal(DateTimeKind.Utc, found.RegisteredAt.Kind);
        Assert.Equal(DateTimeKind.Utc, found.UpdatedAt.Kind);
        Assert.StartsWith("2026-11-01 06:15:00", await ScalarAsync("SELECT ReadAt FROM MeterReadings"));
    }

    // Duplicates, corrections, deletion

    [Fact]
    public async Task Reading_DuplicateTimeForTheSameMeter_IsRefusedByTheServiceAndByTheDatabase()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        var details = new MeterReadingDetails { ReadAt = At(10), Kwh = 100m };
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, details);

        await Assert.ThrowsAsync<ArgumentException>(() => _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, details with { Kwh = 150m }));

        // The service is bypassed: the unique index refuses it, and the error looks the same.
        var duplicate = new MeterReading(setup.MainMeter.Id, details with { Kwh = 150m }, ChargingSetup.Now);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _database.ReadingRepository.AddAsync(duplicate));
        Assert.Equal("ReadAt", exception.ParamName);
        Assert.Equal("1", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
    }

    [Fact]
    public async Task Reading_SameTimeOnDifferentMeters_IsAllowed()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);

        await _database.Charging.RegisterReadingSetAsync(setup.Set(10, 5000m, 100m));

        Assert.Equal("2", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
    }

    [Fact]
    public async Task Correction_IsStoredKeepsRegistrationTimeAndSetsUpdatedAt()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(40), Kwh = 1000m });
        var wrong = await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(10), Kwh = 1118m });
        _database.Clock.Advance(TimeSpan.FromDays(1));

        await _database.Charging.CorrectReadingAsync(wrong.Id, new MeterReadingDetails { ReadAt = At(10), Kwh = 1181m, Notes = "Rettet fra 1118" });
        await _database.RestartAsync();

        var readings = await _database.Charging.GetReadingsAsync(setup.MainMeter.Id);
        Assert.Equal(1181m, readings[1].Kwh);
        Assert.Equal("Rettet fra 1118", readings[1].Notes);
        Assert.Equal(ChargingSetup.Now, readings[1].RegisteredAt);
        Assert.Equal(ChargingSetup.Now.AddDays(1), readings[1].UpdatedAt);
        Assert.Equal(181m, MeterHistory.BuildIntervals(readings).Single().Kwh);
    }

    [Fact]
    public async Task Correction_ThatBreaksTheChronologyOrReusesATime_IsRejectedAndTheStoredReadingIsUnchanged()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(40), Kwh = 1000m });
        var middle = await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(20), Kwh = 1100m });
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(5), Kwh = 1200m });

        await Assert.ThrowsAsync<MeterReadingSequenceException>(
            () => _database.Charging.CorrectReadingAsync(middle.Id, new MeterReadingDetails { ReadAt = At(20), Kwh = 1300m }));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _database.Charging.CorrectReadingAsync(middle.Id, new MeterReadingDetails { ReadAt = At(40), Kwh = 1100m }));

        Assert.Equal(1100m, (await _database.Charging.GetReadingsAsync(setup.MainMeter.Id))[1].Kwh);
    }

    [Fact]
    public async Task Correction_ToTheTimeOfAnotherReadingThroughTheRepository_IsRefusedByTheDatabase()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(40), Kwh = 1000m });
        var other = await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(20), Kwh = 1100m });
        var changed = (await _database.ReadingRepository.GetByIdAsync(other.Id))!;
        changed.Correct(new MeterReadingDetails { ReadAt = At(40), Kwh = 1100m }, ChargingSetup.Now);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _database.ReadingRepository.UpdateAsync(changed));

        Assert.Equal("ReadAt", exception.ParamName);
        Assert.Equal(At(20), (await _database.Charging.GetReadingsAsync(setup.MainMeter.Id))[1].ReadAt);
    }

    [Fact]
    public async Task Deletion_RemovesTheReadingAndTheRestStillFollowTheChronologyAfterRestart()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(40), Kwh = 1000m });
        var wrong = await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(20), Kwh = 1100m });
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(5), Kwh = 1200m });

        await _database.Charging.DeleteReadingAsync(wrong.Id);
        await _database.RestartAsync();

        var readings = await _database.Charging.GetReadingsAsync(setup.MainMeter.Id);
        Assert.Equal([1000m, 1200m], readings.Select(reading => reading.Kwh));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _database.Charging.DeleteReadingAsync(wrong.Id));
    }

    [Fact]
    public async Task History_IsReturnedInTimeOrderNotRegistrationOrder()
    {
        var setup = await ChargingSetup.CreateAsync(_database, chargerCount: 1);
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(5), Kwh = 1200m });
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(40), Kwh = 1000m });
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(20), Kwh = 1100m });

        await _database.RestartAsync();

        Assert.Equal([1000m, 1100m, 1200m], (await _database.Charging.GetReadingsAsync(setup.MainMeter.Id)).Select(reading => reading.Kwh));
    }

    // Reading sets: complete, partial, completed later, reconciled after restart

    [Fact]
    public async Task ReadingSet_ForNineMeters_IsStoredAndReconciledAfterRestart()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(60, 5000m, ChargingSetup.Same(100m)));
        await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5800m, ChargingSetup.Same(197m)));
        Assert.Equal("18", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));

        await _database.RestartAsync();

        var result = (await _database.Charging.ReconcileLatestPeriodAsync())!;
        Assert.True(result.IsComplete);
        Assert.False(result.HasApproximateReadings);
        Assert.Equal(800m, result.MainKwh);
        Assert.Equal(776m, result.SubMeterKwh);
        Assert.Equal(24m, result.Difference);
        Assert.Equal(3m, result.DifferencePercent);
        Assert.Equal(At(60), result.From);
        Assert.Equal(At(30), result.To);
    }

    [Fact]
    public async Task ReadingSet_PartlyFilled_IsStoredWithoutZeroValuesAndCanBeCompletedLaterAfterRestart()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(60, 5000m, ChargingSetup.Same(100m)));
        var subs = ChargingSetup.Same(197m);
        subs[2] = null;
        subs[6] = null;
        await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5800m, subs));
        Assert.Equal("16", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings"));
        Assert.Equal("0", await ScalarAsync("SELECT COUNT(*) FROM MeterReadings WHERE CAST(Kwh AS REAL) = 0"));
        await _database.RestartAsync();

        var incomplete = (await _database.Charging.ReconcileLatestPeriodAsync())!;
        Assert.False(incomplete.IsComplete);
        Assert.Null(incomplete.Difference);
        Assert.Equal(2, incomplete.MissingMeters.Count);

        var completion = new MeterReadingSetDetails
        {
            ReadAt = At(30),
            SubMeterKwh = new Dictionary<int, decimal?> { [setup.SubMeters[2].Id] = 197m, [setup.SubMeters[6].Id] = 197m }
        };
        await _database.Charging.RegisterReadingSetAsync(completion);
        await _database.RestartAsync();

        var complete = (await _database.Charging.ReconcileLatestPeriodAsync())!;
        Assert.True(complete.IsComplete);
        Assert.Equal(24m, complete.Difference);
    }

    [Fact]
    public async Task ReadingSet_DisabledChargerKeepsItsHistoryAndStillCountsInTheReconciliation()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(60, 5000m, ChargingSetup.Same(100m)));
        await _database.Charging.ChangeChargerStatusAsync(setup.Chargers[3].Id, ChargerStatus.TemporarilyDisabled);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 5800m, ChargingSetup.Same(197m)));
        await _database.RestartAsync();

        Assert.Equal(2, (await _database.Charging.GetReadingsAsync(setup.SubMeters[3].Id)).Count);
        Assert.Equal(ChargerStatus.TemporarilyDisabled, (await _database.Charging.GetChargerAsync(setup.Chargers[3].Id))!.Status);
        Assert.True((await _database.Charging.ReconcileLatestPeriodAsync())!.IsComplete);
    }

    [Fact]
    public async Task ReadingSet_ReadingsAtOtherTimesThanThePeriodBoundary_AreMarkedApproximateAfterRestart()
    {
        var setup = await ChargingSetup.CreateAsync(_database);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(60, 5000m, ChargingSetup.Same(100m)));
        await _database.Charging.RegisterReadingAsync(setup.MainMeter.Id, new MeterReadingDetails { ReadAt = At(30), Kwh = 5800m });
        foreach (var meter in setup.SubMeters)
        {
            await _database.Charging.RegisterReadingAsync(meter.Id, new MeterReadingDetails { ReadAt = At(30, hour: 14), Kwh = 197m });
        }

        await _database.RestartAsync();

        var result = (await _database.Charging.ReconcileLatestPeriodAsync())!;

        Assert.True(result.IsComplete);
        Assert.True(result.HasApproximateReadings);
        Assert.All(result.SubMeters, line => Assert.True(line.Consumption.IsApproximate));
        Assert.False(result.Main!.Consumption.IsApproximate);
        Assert.Equal(776m, result.SubMeterKwh);
    }

    [Fact]
    public async Task ChargingTables_AreEmptyAfterANormalStartup_NothingIsCreatedAutomatically()
    {
        await _database.RestartAsync();

        foreach (var table in new[] { "ChargingFacilities", "EvChargers", "Meters", "MeterReadings" })
        {
            Assert.Equal("0", await ScalarAsync($"SELECT COUNT(*) FROM {table}"));
        }
    }
}
