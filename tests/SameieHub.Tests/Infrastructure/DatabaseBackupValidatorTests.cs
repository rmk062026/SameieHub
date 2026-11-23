using Microsoft.Data.Sqlite;
using SameieHub.Infrastructure.Backup;
using static SameieHub.Tests.Infrastructure.DatabaseTestFiles;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// The checks a backup file must pass. The same validator can later check a file before it is restored.
/// </summary>
public class DatabaseBackupValidatorTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubBackupTests", Guid.NewGuid().ToString());

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
        Directory.CreateDirectory(_folder);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private async Task<string> CopyOfTheDatabaseAsync()
    {
        var copy = Path.Combine(_folder, "kopi.db");
        await CopyAsync(_database.DatabasePath, copy);

        return copy;
    }

    [Fact]
    public async Task FindProblems_MigratedDatabase_HasNone()
    {
        Assert.Empty(DatabaseBackupValidator.FindProblems(await CopyOfTheDatabaseAsync()));
    }

    [Fact]
    public void FindProblems_FileThatIsNotADatabase_IsReported()
    {
        var file = Path.Combine(_folder, "tekst.db");
        File.WriteAllText(file, "bare tekst, ingen database, men lang nok til at SQLite ikke tolker den som tom");

        Assert.NotEmpty(DatabaseBackupValidator.FindProblems(file));
    }

    [Fact]
    public void FindProblems_MissingFile_IsReportedAndNothingIsCreated()
    {
        var file = Path.Combine(_folder, "finnes-ikke.db");

        Assert.NotEmpty(DatabaseBackupValidator.FindProblems(file));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task FindProblems_TruncatedFile_IsReported()
    {
        var copy = await CopyOfTheDatabaseAsync();
        var bytes = await File.ReadAllBytesAsync(copy);
        await File.WriteAllBytesAsync(copy, bytes[..(bytes.Length / 2)]);

        Assert.NotEmpty(DatabaseBackupValidator.FindProblems(copy));
    }

    [Fact]
    public async Task FindProblems_MissingTable_NamesTheTable()
    {
        var copy = await CopyOfTheDatabaseAsync();
        await ExecuteAsync(copy, "PRAGMA foreign_keys = OFF; DROP TABLE MeterReadings");

        var problems = DatabaseBackupValidator.FindProblems(copy);

        Assert.Contains(problems, problem => problem.Contains("MeterReadings"));
    }

    [Fact]
    public async Task FindProblems_BrokenReference_IsReported()
    {
        var unit = await _database.Service.RegisterAsync("Fiktivdalen 1");
        await _database.BoardCases.RegisterAsync(new SameieHub.Domain.BoardCaseDetails { Title = "Sak", HousingUnitId = unit.Id });
        var copy = await CopyOfTheDatabaseAsync();
        await ExecuteAsync(copy, "PRAGMA foreign_keys = OFF; UPDATE BoardCases SET HousingUnitId = 9999");

        var problems = DatabaseBackupValidator.FindProblems(copy);

        Assert.Contains(problems, problem => problem.Contains("reference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FindProblems_DatabaseThatIsNotFullyMigrated_IsReported()
    {
        await _database.DisposeAsync();
        _database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await _database.MigrateToMigrationAsync("AddTaskItems");
        var copy = await CopyOfTheDatabaseAsync();

        var problems = DatabaseBackupValidator.FindProblems(copy);

        Assert.Contains(problems, problem => problem.Contains("migration", StringComparison.OrdinalIgnoreCase));
    }
}
