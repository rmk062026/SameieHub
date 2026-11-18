using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class BoardCaseMigrationTests
{
    private const string HousingUnitSnapshotSql =
        "SELECT group_concat(row, ';') FROM (" +
        "SELECT Id || '|' || Address || '|' || IFNULL(OwnerName, '') || '|' || IFNULL(OwnerEmail, '') || '|' || " +
        "IFNULL(SecondOwnerName, '') || '|' || IFNULL(HousingType, '') || '|' || IFNULL(SectionNumber, '') || '|' || IsActive AS row " +
        "FROM HousingUnits ORDER BY Id)";

    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesEmptyBoardCasesTable()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        Assert.Equal("0", await database.QueryTextAsync("SELECT COUNT(*) FROM BoardCases"));
        Assert.Empty(await database.BoardCases.GetAllAsync());
    }

    [Fact]
    public async Task Migrate_FromHousingUnitSchemaWithData_KeepsHousingUnitsAndPersonalDataUnchanged()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddHousingUnitDetails");
        await database.ExecuteSqlAsync(
            "INSERT INTO HousingUnits (Address, OwnerName, OwnerEmail, SecondOwnerName, HousingType, SectionNumber, IsActive) VALUES " +
            "('Storgata 1A', 'Test Eier', 'eier@example.test', 'Test Medeier', 'Duplex', 12, 1), " +
            "('Storgata 1B', NULL, NULL, NULL, NULL, NULL, 0)");
        var before = await database.QueryTextAsync(HousingUnitSnapshotSql);

        await database.MigrateToLatestAsync();

        Assert.Equal(before, await database.QueryTextAsync(HousingUnitSnapshotSql));
        Assert.Equal("0", await database.QueryTextAsync("SELECT COUNT(*) FROM BoardCases"));
    }

    [Fact]
    public async Task Migrate_FromHousingUnitSchemaWithData_ExistingUnitsCanGetBoardCases()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddHousingUnitDetails");
        await database.ExecuteSqlAsync("INSERT INTO HousingUnits (Address, IsActive) VALUES ('Storgata 1A', 1)");
        await database.MigrateToLatestAsync();
        var unit = (await database.Service.GetAllAsync()).Single();

        var registered = await database.BoardCases.RegisterAsync(
            new BoardCaseDetails { Title = "Maling av fellesarealer", HousingUnitId = unit.Id });

        Assert.Equal(unit.Id, (await database.BoardCases.GetByIdAsync(registered.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task Migrate_FromHousingUnitSchema_RunningMigrationsAgainChangesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddHousingUnitDetails");
        await database.ExecuteSqlAsync("INSERT INTO HousingUnits (Address, IsActive) VALUES ('Storgata 1A', 1)");
        await database.MigrateToLatestAsync();
        await database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Vedtekter" });

        await database.RestartAsync();

        Assert.Single(await database.Service.GetAllAsync());
        Assert.Single(await database.BoardCases.GetAllAsync());
    }
}
