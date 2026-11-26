using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Infrastructure.Restore;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// Checks what happens to the database before the program migrates it. Every test uses its own temporary database;
/// the ordinary database in AppData is never touched.
/// </summary>
public class DatabaseUpgraderTests : IDisposable
{
    private const string FictionalAddress = "Fiktivdalen 1A";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubTests", Guid.NewGuid().ToString());
    private readonly CapturingLoggerProvider _logs = new();
    private ServiceProvider? _provider;

    public DatabaseUpgraderTests()
    {
        Directory.CreateDirectory(_folder);
        DatabasePath = Path.Combine(_folder, "sameiehub.db");
    }

    private string DatabasePath { get; }

    private string BackupFolder => Path.Combine(_folder, DatabaseUpgrader.BackupFolderName);

    public void Dispose()
    {
        _provider?.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Upgrade_NoDatabaseYet_CreatesItWithoutABackup()
    {
        await CreateUpgrader().UpgradeAsync();

        Assert.True(File.Exists(DatabasePath));
        Assert.False(Directory.Exists(BackupFolder));
        Assert.Equal(DatabaseBackupValidator.FindLastMigrationId(DatabasePath), await LatestKnownMigrationAsync());
    }

    [Fact]
    public async Task Upgrade_AnExistingEmptyFile_IsRefusedAndNeitherFilledNorReplaced()
    {
        await File.WriteAllBytesAsync(DatabasePath, []);

        var exception = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => CreateUpgrader().UpgradeAsync());

        Assert.Equal(DatabaseUpgradeFailure.EmptyDatabaseFile, exception.Reason);
        Assert.Equal(0, new FileInfo(DatabasePath).Length);
        Assert.False(Directory.Exists(BackupFolder));
        Assert.Equal([DatabasePath], Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task Upgrade_ADatabaseThatIsAlreadyCurrent_IsMigratedWithoutABackup()
    {
        await CreateUpgrader().UpgradeAsync();
        SqliteConnection.ClearAllPools();

        await CreateUpgrader().UpgradeAsync();

        Assert.False(Directory.Exists(BackupFolder));
    }

    [Fact]
    public async Task Upgrade_AnOlderDatabase_IsBackedUpConsistentlyBeforeItIsMigrated()
    {
        await CreateOlderDatabaseAsync("AddTaskItems");

        await CreateUpgrader().UpgradeAsync();

        var backup = Assert.Single(Directory.GetFiles(BackupFolder));
        Assert.StartsWith(DatabaseUpgrader.BackupFilePrefix, Path.GetFileName(backup));
        Assert.Equal(BackupFileStatus.Older, BackupFileInspector.Inspect(backup).Status);
        Assert.EndsWith("_AddTaskItems", DatabaseBackupValidator.FindLastMigrationId(backup));
        Assert.Equal(FictionalAddress, await DatabaseTestFiles.ScalarAsync(backup, "SELECT Address FROM HousingUnits"));
        Assert.Equal(await LatestKnownMigrationAsync(), DatabaseBackupValidator.FindLastMigrationId(DatabasePath));
        Assert.Equal(FictionalAddress, await DatabaseTestFiles.ScalarAsync(DatabasePath, "SELECT Address FROM HousingUnits"));
    }

    [Fact]
    public async Task Upgrade_TheBackupIncludesRowsThatOnlyExistInTheWalFile()
    {
        await CreateOlderDatabaseAsync("AddTaskItems");
        await using var openConnection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        await openConnection.OpenAsync();
        await using (var command = openConnection.CreateCommand())
        {
            command.CommandText = "INSERT INTO HousingUnits (Address, IsActive) VALUES ('Fiktivdalen 2B', 1)";
            await command.ExecuteNonQueryAsync();
        }

        await CreateUpgrader().UpgradeAsync();

        var backup = Assert.Single(Directory.GetFiles(BackupFolder));
        Assert.Equal("2", await DatabaseTestFiles.ScalarAsync(backup, "SELECT COUNT(*) FROM HousingUnits"));
    }

    [Fact]
    public async Task Upgrade_ADatabaseFromANewerVersion_IsRefusedAndNothingIsChanged()
    {
        await CreateUpgrader().UpgradeAsync();
        await DatabaseTestFiles.ExecuteAsync(DatabasePath, "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99999999999999_FromTheFuture', '99.0.0')");
        SqliteConnection.ClearAllPools();
        var before = await DatabaseTestFiles.HashAsync(DatabasePath);

        var exception = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => CreateUpgrader().UpgradeAsync());

        Assert.Equal(DatabaseUpgradeFailure.NewerThanProgram, exception.Reason);
        Assert.Equal(before, await DatabaseTestFiles.HashAsync(DatabasePath));
        Assert.False(Directory.Exists(BackupFolder));
    }

    [Fact]
    public async Task Upgrade_ADatabaseThatIsNotFromSameieHub_IsRefusedAndNotMigrated()
    {
        await DatabaseTestFiles.ExecuteAsync(DatabasePath, "CREATE TABLE Other (Id INTEGER PRIMARY KEY)");
        SqliteConnection.ClearAllPools();

        var exception = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => CreateUpgrader().UpgradeAsync());

        Assert.Equal(DatabaseUpgradeFailure.NotSameieHubDatabase, exception.Reason);
        Assert.Equal("0", await DatabaseTestFiles.ScalarAsync(DatabasePath, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'HousingUnits'"));
    }

    [Fact]
    public async Task Upgrade_ADamagedFile_IsRefusedAndLeftAsItIs()
    {
        await File.WriteAllTextAsync(DatabasePath, "this is not a database, and it is long enough to not look like an empty file");
        var before = await DatabaseTestFiles.HashAsync(DatabasePath);

        var exception = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => CreateUpgrader().UpgradeAsync());

        Assert.Equal(DatabaseUpgradeFailure.DamagedDatabase, exception.Reason);
        Assert.Equal(before, await DatabaseTestFiles.HashAsync(DatabasePath));
    }

    [Fact]
    public async Task Upgrade_WhenTheBackupFolderCannotBeCreated_StopsBeforeMigrating()
    {
        await CreateOlderDatabaseAsync("AddTaskItems");
        await File.WriteAllTextAsync(BackupFolder, "a file where the folder should be");
        var lastMigrationBefore = DatabaseBackupValidator.FindLastMigrationId(DatabasePath);

        var exception = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => CreateUpgrader().UpgradeAsync());

        Assert.Equal(DatabaseUpgradeFailure.BackupFailed, exception.Reason);
        Assert.Equal(lastMigrationBefore, DatabaseBackupValidator.FindLastMigrationId(DatabasePath));
    }

    [Fact]
    public async Task Upgrade_WhenAMigrationFails_StopsKeepsTheBackupAndRollsTheFailedMigrationBack()
    {
        await CreateOlderDatabaseAsync("InitialCreate");
        // OwnerName is the sixth column AddHousingUnitDetails adds, so the migration fails after five other changes were made.
        await DatabaseTestFiles.ExecuteAsync(DatabasePath, "ALTER TABLE HousingUnits ADD COLUMN OwnerName TEXT");
        SqliteConnection.ClearAllPools();

        var exception = await Assert.ThrowsAsync<DatabaseUpgradeException>(() => CreateUpgrader().UpgradeAsync());

        Assert.Equal(DatabaseUpgradeFailure.MigrationFailed, exception.Reason);
        Assert.NotNull(exception.BackupPath);
        Assert.True(File.Exists(exception.BackupPath));
        Assert.EndsWith("_InitialCreate", DatabaseBackupValidator.FindLastMigrationId(exception.BackupPath!));
        Assert.Equal(FictionalAddress, await DatabaseTestFiles.ScalarAsync(exception.BackupPath!, "SELECT Address FROM HousingUnits"));
        Assert.True(exception.LiveDatabaseVerified);
        Assert.EndsWith("_InitialCreate", DatabaseBackupValidator.FindLastMigrationId(DatabasePath));
        Assert.Equal("0", await DatabaseTestFiles.ScalarAsync(DatabasePath, "SELECT COUNT(*) FROM pragma_table_info('HousingUnits') WHERE name = 'AreaSquareMeters'"));
    }

    [Fact]
    public async Task Upgrade_AfterASuccessfulMigration_KeepsOnlyTheThreeNewestBackups()
    {
        await CreateOlderDatabaseAsync("AddTaskItems");
        Directory.CreateDirectory(BackupFolder);
        foreach (var name in new[] { "20260101-100000", "20260102-100000", "20260103-100000", "20260104-100000" })
        {
            await File.WriteAllTextAsync(Path.Combine(BackupFolder, $"{DatabaseUpgrader.BackupFilePrefix}{name}.db"), "old");
        }

        await File.WriteAllTextAsync(Path.Combine(BackupFolder, "something-else.db"), "not ours");

        await CreateUpgrader().UpgradeAsync();

        var kept = Directory.GetFiles(BackupFolder).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(4, kept.Count);
        Assert.Contains("something-else.db", kept);
        Assert.DoesNotContain($"{DatabaseUpgrader.BackupFilePrefix}20260101-100000.db", kept);
        Assert.DoesNotContain($"{DatabaseUpgrader.BackupFilePrefix}20260102-100000.db", kept);
    }

    [Fact]
    public async Task Upgrade_ALogShowsReasonsOnly_NeverDataFromTheDatabase()
    {
        await CreateOlderDatabaseAsync("InitialCreate");
        await DatabaseTestFiles.ExecuteAsync(DatabasePath, "ALTER TABLE HousingUnits ADD COLUMN OwnerName TEXT");
        SqliteConnection.ClearAllPools();

        await Assert.ThrowsAsync<DatabaseUpgradeException>(() => CreateUpgrader().UpgradeAsync());

        Assert.NotEmpty(_logs.Messages);
        Assert.All(_logs.Messages, message =>
        {
            Assert.DoesNotContain(FictionalAddress, message);
            Assert.DoesNotContain(_folder, message);
        });
    }

    private DatabaseUpgrader CreateUpgrader()
    {
        _provider?.Dispose();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(_logs));
        services.AddSingleton(TimeProvider.System);
        services.AddSameieHubInfrastructure(DatabasePath);
        _provider = services.BuildServiceProvider();

        return _provider.GetRequiredService<DatabaseUpgrader>();
    }

    private async Task CreateOlderDatabaseAsync(string lastMigrationName)
    {
        var options = new DbContextOptionsBuilder<SameieHubDbContext>().UseSqlite($"Data Source={DatabasePath};Pooling=False").Options;
        await using (var context = new SameieHubDbContext(options))
        {
            var target = context.Database.GetMigrations().Single(id => id.EndsWith("_" + lastMigrationName));
            await context.GetService<IMigrator>().MigrateAsync(target);
        }

        await DatabaseTestFiles.ExecuteAsync(DatabasePath, $"INSERT INTO HousingUnits (Address) VALUES ('{FictionalAddress}')");
        SqliteConnection.ClearAllPools();
    }

    private static Task<string> LatestKnownMigrationAsync()
    {
        var options = new DbContextOptionsBuilder<SameieHubDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var context = new SameieHubDbContext(options);

        return Task.FromResult(context.Database.GetMigrations().Max()!);
    }
}
