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

    private const string BoardCaseSnapshotSql =
        "SELECT group_concat(row, ';') FROM (" +
        "SELECT Id || '|' || Title || '|' || IFNULL(Description, '') || '|' || Status || '|' || Priority || '|' || " +
        "CreatedAt || '|' || UpdatedAt || '|' || IFNULL(MeetingDate, '') || '|' || IFNULL(Decision, '') || '|' || " +
        "IFNULL(HousingUnitId, '') || '|' || IFNULL(Notes, '') AS row FROM BoardCases ORDER BY Id)";

    [Fact]
    public async Task Migrate_FromBoardCaseSchemaWithData_KeepsCasesUnchangedAndDecisionDateIsNull()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddBoardCases");
        await database.ExecuteSqlAsync("INSERT INTO HousingUnits (Address, IsActive) VALUES ('Storgata 1A', 1)");
        await database.ExecuteSqlAsync(
            "INSERT INTO BoardCases (Title, Description, Status, Priority, CreatedAt, UpdatedAt, MeetingDate, Decision, HousingUnitId, Notes) VALUES " +
            "('Maling av porter', 'Fiktiv beskrivelse.', 'Decided', 'High', '2026-11-18 10:30:00', '2026-11-19 08:00:00', '2026-12-03', 'Fiktivt vedtak.', 1, 'Fiktive merknader.'), " +
            "('Vedtekter', NULL, 'New', 'Normal', '2026-11-18 11:00:00', '2026-11-18 11:00:00', NULL, NULL, NULL, NULL)");
        var before = await database.QueryTextAsync(BoardCaseSnapshotSql);

        await database.MigrateToLatestAsync();

        Assert.Equal(before, await database.QueryTextAsync(BoardCaseSnapshotSql));
        Assert.Equal("2", await database.QueryTextAsync("SELECT COUNT(*) FROM BoardCases WHERE DecisionDate IS NULL"));
        var cases = await database.BoardCases.GetAllAsync();
        Assert.All(cases, boardCase => Assert.Null(boardCase.DecisionDate));
        Assert.Equal("Fiktivt vedtak.", cases[0].Decision);
        Assert.Equal(DateTimeKind.Utc, cases[0].CreatedAt.Kind);
    }

    [Fact]
    public async Task Migrate_FromBoardCaseSchemaWithData_ExistingDecisionCanGetADate()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddBoardCases");
        await database.ExecuteSqlAsync(
            "INSERT INTO BoardCases (Title, Status, Priority, CreatedAt, UpdatedAt, Decision) VALUES " +
            "('Vedtekter', 'New', 'Normal', '2026-11-18 11:00:00', '2026-11-18 11:00:00', 'Fiktivt vedtak.')");
        await database.MigrateToLatestAsync();
        var boardCase = (await database.BoardCases.GetAllAsync()).Single();

        await database.BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 3));

        Assert.Equal(new DateOnly(2026, 12, 3), (await database.BoardCases.GetByIdAsync(boardCase.Id))!.DecisionDate);
    }
}
