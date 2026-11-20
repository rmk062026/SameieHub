using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class ChargingMigrationTests
{
    private const string PreviousMigration = "AddTaskItems";

    private static readonly string[] ChargingTables = ["ChargingFacilities", "EvChargers", "Meters", "MeterReadings"];

    private const string HousingUnitSnapshotSql =
        "SELECT group_concat(row, ';') FROM (" +
        "SELECT Id || '|' || Address || '|' || IFNULL(OwnerName, '') || '|' || IFNULL(OwnerEmail, '') || '|' || " +
        "IFNULL(SecondOwnerName, '') || '|' || IFNULL(HousingType, '') || '|' || IFNULL(SectionNumber, '') || '|' || IsActive AS row " +
        "FROM HousingUnits ORDER BY Id)";

    private const string BoardCaseSnapshotSql =
        "SELECT group_concat(row, ';') FROM (" +
        "SELECT Id || '|' || Title || '|' || Status || '|' || Priority || '|' || CreatedAt || '|' || UpdatedAt || '|' || " +
        "IFNULL(MeetingDate, '') || '|' || IFNULL(Decision, '') || '|' || IFNULL(DecisionDate, '') || '|' || IFNULL(HousingUnitId, '') AS row " +
        "FROM BoardCases ORDER BY Id)";

    private const string TaskSnapshotSql =
        "SELECT group_concat(row, ';') FROM (" +
        "SELECT Id || '|' || Title || '|' || Category || '|' || Status || '|' || Priority || '|' || IFNULL(DueDate, '') || '|' || " +
        "IFNULL(HousingUnitId, '') || '|' || IFNULL(BoardCaseId, '') || '|' || CreatedAt || '|' || IFNULL(CompletedAt, '') AS row " +
        "FROM TaskItems ORDER BY Id)";

    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesTheFourChargingTablesWithoutAnyRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        foreach (var table in ChargingTables)
        {
            Assert.Equal("1", await database.QueryTextAsync($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'"));
            Assert.Equal("0", await database.QueryTextAsync($"SELECT COUNT(*) FROM {table}"));
        }
    }

    [Fact]
    public async Task Migrate_CreatesTheUniqueIndexesAndTheRestrictedForeignKeys()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        Assert.Equal("1", await database.QueryTextAsync("SELECT \"unique\" FROM pragma_index_list('EvChargers') WHERE name = 'IX_EvChargers_HousingUnitId'"));
        Assert.Equal("1", await database.QueryTextAsync("SELECT \"unique\" FROM pragma_index_list('Meters') WHERE name = 'IX_Meters_EvChargerId'"));
        Assert.Equal("1", await database.QueryTextAsync("SELECT \"unique\" FROM pragma_index_list('Meters') WHERE name = 'IX_Meters_ChargingFacilityId'"));
        Assert.Equal("1", await database.QueryTextAsync("SELECT \"unique\" FROM pragma_index_list('MeterReadings') WHERE name = 'IX_MeterReadings_MeterId_ReadAt'"));

        foreach (var table in new[] { "EvChargers", "Meters", "MeterReadings" })
        {
            Assert.Equal("0", await database.QueryTextAsync(
                $"SELECT COUNT(*) FROM pragma_foreign_key_list('{table}') WHERE on_delete <> 'RESTRICT'"));
            Assert.NotEqual("0", await database.QueryTextAsync($"SELECT COUNT(*) FROM pragma_foreign_key_list('{table}')"));
        }
    }

    [Fact]
    public async Task Migrate_FromTheTaskItemsSchemaWithData_KeepsEveryExistingRowUnchanged()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync(PreviousMigration);
        await InsertExistingDataAsync(database);
        var unitsBefore = await database.QueryTextAsync(HousingUnitSnapshotSql);
        var casesBefore = await database.QueryTextAsync(BoardCaseSnapshotSql);
        var tasksBefore = await database.QueryTextAsync(TaskSnapshotSql);

        await database.MigrateToLatestAsync();

        Assert.Equal(unitsBefore, await database.QueryTextAsync(HousingUnitSnapshotSql));
        Assert.Equal(casesBefore, await database.QueryTextAsync(BoardCaseSnapshotSql));
        Assert.Equal(tasksBefore, await database.QueryTextAsync(TaskSnapshotSql));
        Assert.Equal("2", await database.QueryTextAsync("SELECT COUNT(*) FROM HousingUnits"));
        Assert.Equal("1", await database.QueryTextAsync("SELECT COUNT(*) FROM BoardCases"));
        Assert.Equal("1", await database.QueryTextAsync("SELECT COUNT(*) FROM TaskItems"));
    }

    [Fact]
    public async Task Migrate_FromTheTaskItemsSchemaWithData_CreatesNoChargingData()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync(PreviousMigration);
        await InsertExistingDataAsync(database);

        await database.MigrateToLatestAsync();

        foreach (var table in ChargingTables)
        {
            Assert.Equal("0", await database.QueryTextAsync($"SELECT COUNT(*) FROM {table}"));
        }
    }

    [Fact]
    public async Task Migrate_ExistingHousingUnitsCanGetChargersAndReadingsAfterTheUpgrade()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync(PreviousMigration);
        await InsertExistingDataAsync(database);
        await database.MigrateToLatestAsync();
        var unit = (await database.Service.GetAllAsync()).First(existing => existing.IsActive);
        await database.Charging.SetUpFacilityAsync("Felles ladeanlegg");

        var charger = await database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails { ChargerNumber = "L1" }, 1);
        var meter = (await database.Charging.GetSubMeterAsync(charger.Id))!;
        await database.Charging.RegisterReadingAsync(meter.Id, new MeterReadingDetails { ReadAt = ChargingSetup.At(10), Kwh = 12.345m });
        await database.RestartAsync();

        Assert.Equal(unit.Id, (await database.Charging.GetChargerForHousingUnitAsync(unit.Id))!.HousingUnitId);
        Assert.Equal(12.345m, (await database.Charging.GetReadingsAsync(meter.Id)).Single().Kwh);
        Assert.Equal("Storgata 1A", (await database.Service.GetByIdAsync(unit.Id))!.Address);
    }

    [Fact]
    public async Task Migrate_RunningAgainAfterTheUpgrade_ChangesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync(PreviousMigration);
        await InsertExistingDataAsync(database);
        await database.MigrateToLatestAsync();
        var unitsBefore = await database.QueryTextAsync(HousingUnitSnapshotSql);

        await database.RestartAsync();

        Assert.Equal(unitsBefore, await database.QueryTextAsync(HousingUnitSnapshotSql));
        Assert.Equal("1", await database.QueryTextAsync("SELECT COUNT(*) FROM TaskItems"));
    }

    private static async Task InsertExistingDataAsync(SqliteTestDatabase database)
    {
        await database.ExecuteSqlAsync(
            "INSERT INTO HousingUnits (Address, OwnerName, OwnerEmail, SecondOwnerName, HousingType, SectionNumber, IsActive) VALUES " +
            "('Storgata 1A', 'Test Eier', 'eier@example.test', 'Test Medeier', 'Duplex', 12, 1), " +
            "('Storgata 1B', NULL, NULL, NULL, NULL, NULL, 0)");
        await database.ExecuteSqlAsync(
            "INSERT INTO BoardCases (Title, Description, Status, Priority, CreatedAt, UpdatedAt, MeetingDate, Decision, DecisionDate, HousingUnitId, Notes) VALUES " +
            "('Maling av porter', 'Fiktiv beskrivelse.', 'Decided', 'High', '2026-11-18 10:30:00', '2026-11-19 08:00:00', '2026-12-03', 'Fiktivt vedtak.', '2026-12-03', 1, 'Fiktive merknader.')");
        await database.ExecuteSqlAsync(
            "INSERT INTO TaskItems (Title, Description, Category, Status, Priority, DueDate, AssignedTo, HousingUnitId, BoardCaseId, CreatedAt, UpdatedAt, CompletedAt, Notes) VALUES " +
            "('Rense takrenner', NULL, 'Maintenance', 'Completed', 'High', '2026-12-01', 'Test Person', 1, 1, '2026-11-18 10:30:00', '2026-11-19 08:00:00', '2026-11-19 08:00:00', NULL)");
    }
}
