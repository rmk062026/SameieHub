using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class TaskItemMigrationTests
{
    private const string HousingUnitSnapshotSql =
        "SELECT group_concat(row, ';') FROM (" +
        "SELECT Id || '|' || Address || '|' || IFNULL(OwnerName, '') || '|' || IFNULL(OwnerEmail, '') || '|' || " +
        "IFNULL(SecondOwnerName, '') || '|' || IFNULL(HousingType, '') || '|' || IFNULL(SectionNumber, '') || '|' || IsActive AS row " +
        "FROM HousingUnits ORDER BY Id)";

    private const string BoardCaseSnapshotSql =
        "SELECT group_concat(row, ';') FROM (" +
        "SELECT Id || '|' || Title || '|' || IFNULL(Description, '') || '|' || Status || '|' || Priority || '|' || " +
        "CreatedAt || '|' || UpdatedAt || '|' || IFNULL(MeetingDate, '') || '|' || IFNULL(Decision, '') || '|' || " +
        "IFNULL(DecisionDate, '') || '|' || IFNULL(HousingUnitId, '') || '|' || IFNULL(Notes, '') AS row FROM BoardCases ORDER BY Id)";

    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesEmptyTaskItemsTable()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        Assert.Equal("0", await database.QueryTextAsync("SELECT COUNT(*) FROM TaskItems"));
        Assert.Empty(await database.Tasks.GetAllAsync());
    }

    [Fact]
    public async Task Migrate_FromBoardCaseDecisionDateSchemaWithData_KeepsEverythingUnchanged()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddBoardCaseDecisionDate");
        await InsertExistingDataAsync(database);
        var unitsBefore = await database.QueryTextAsync(HousingUnitSnapshotSql);
        var casesBefore = await database.QueryTextAsync(BoardCaseSnapshotSql);

        await database.MigrateToLatestAsync();

        Assert.Equal(unitsBefore, await database.QueryTextAsync(HousingUnitSnapshotSql));
        Assert.Equal(casesBefore, await database.QueryTextAsync(BoardCaseSnapshotSql));
        Assert.Equal("0", await database.QueryTextAsync("SELECT COUNT(*) FROM TaskItems"));
        var boardCase = (await database.BoardCases.GetAllAsync()).Single();
        Assert.Equal("Fiktivt vedtak.", boardCase.Decision);
        Assert.Equal(new DateOnly(2026, 12, 3), boardCase.DecisionDate);
    }

    [Fact]
    public async Task Migrate_FromBoardCaseDecisionDateSchemaWithData_ExistingObjectsCanGetTasks()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddBoardCaseDecisionDate");
        await InsertExistingDataAsync(database);
        await database.MigrateToLatestAsync();
        var unit = (await database.Service.GetAllAsync()).First(existing => existing.IsActive);
        var boardCase = (await database.BoardCases.GetAllAsync()).Single();

        var task = await database.Tasks.RegisterAsync(new TaskItemDetails
        {
            Title = "Rense takrenner",
            HousingUnitId = unit.Id,
            BoardCaseId = boardCase.Id
        });

        var found = await database.Tasks.GetByIdAsync(task.Id);
        Assert.Equal(unit.Id, found!.HousingUnitId);
        Assert.Equal(boardCase.Id, found.BoardCaseId);
    }

    [Fact]
    public async Task Migrate_RunningMigrationsAgain_ChangesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToMigrationAsync("AddBoardCaseDecisionDate");
        await InsertExistingDataAsync(database);
        await database.MigrateToLatestAsync();
        await database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Rense takrenner" });
        var unitsBefore = await database.QueryTextAsync(HousingUnitSnapshotSql);

        await database.RestartAsync();

        Assert.Equal(unitsBefore, await database.QueryTextAsync(HousingUnitSnapshotSql));
        Assert.Single(await database.Tasks.GetAllAsync());
        Assert.Single(await database.BoardCases.GetAllAsync());
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
    }
}
