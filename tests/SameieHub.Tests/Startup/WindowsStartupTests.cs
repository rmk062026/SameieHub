using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Infrastructure.Restore;
using SameieHub.Tests.Infrastructure;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>
/// The order and the stop conditions of the Windows start: the lock first, then a pending restore, then the check and
/// backup before migrating. Only temporary databases are used.
/// </summary>
public class WindowsStartupTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubTests", Guid.NewGuid().ToString());
    private ServiceProvider? _provider;

    public WindowsStartupTests()
    {
        Directory.CreateDirectory(_folder);
        DatabasePath = Path.Combine(_folder, "sameiehub.db");
    }

    private string DatabasePath { get; }

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
    public async Task ASecondProgramInstance_IsStoppedBeforeAPendingRestoreIsApplied()
    {
        var restore = new RecordingRestore();
        using var firstInstance = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));

        var result = await WindowsStartup.PrepareAsync(DatabasePath, restore, () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance);

        Assert.NotNull(firstInstance);
        Assert.Null(result.Lock);
        Assert.Equal(StartupFailureReason.AlreadyRunning, result.Failure!.Reason);
        Assert.Equal(0, restore.ApplyCalls);
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public async Task ANormalStart_TakesTheLock_AppliesRestoreAndCreatesTheDatabase()
    {
        var restore = new RecordingRestore();

        using var result = await WindowsStartup.PrepareAsync(DatabasePath, restore, () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance);

        Assert.Null(result.Failure);
        Assert.NotNull(result.Lock);
        Assert.Equal(1, restore.ApplyCalls);
        Assert.True(File.Exists(DatabasePath));
        Assert.Null(InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath)));
    }

    [Fact]
    public async Task ARestartedProgram_WaitsUntilTheOldOneHasReleasedTheLock_AndThenAppliesTheRestoreOnce()
    {
        var restore = new RecordingRestore();
        var oldInstance = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));
        var activated = 0;
        _ = Task.Run(async () =>
        {
            await Task.Delay(400);
            oldInstance!.Dispose();
        });

        using var result = await WindowsStartup.PrepareAsync(
            DatabasePath,
            restore,
            () => CreateUpgrader().UpgradeAsync(),
            NullLogger.Instance,
            () =>
            {
                activated++;

                return Task.FromResult(true);
            },
            waitForLock: TimeSpan.FromSeconds(10));

        Assert.Null(result.Failure);
        Assert.NotNull(result.Lock);
        Assert.False(result.ActivatedRunningInstance);
        Assert.Equal(0, activated);
        Assert.Equal(1, restore.ApplyCalls);
    }

    [Fact]
    public async Task ARestartedProgram_GivesUpAfterTheTimeLimit_WithoutTouchingTheDatabase_AndDoesNotTryAgain()
    {
        var restore = new RecordingRestore();
        using var oldInstance = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));

        var result = await WindowsStartup.PrepareAsync(
            DatabasePath,
            restore,
            () => CreateUpgrader().UpgradeAsync(),
            NullLogger.Instance,
            waitForLock: TimeSpan.FromMilliseconds(400));

        Assert.Null(result.Lock);
        Assert.Equal(StartupFailureReason.AlreadyRunning, result.Failure!.Reason);
        Assert.Equal(0, restore.ApplyCalls);
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public async Task TwoProgramsThatStartAtTheSameTime_NeverBothApplyTheRestore()
    {
        var restore = new RecordingRestore();

        var first = WindowsStartup.PrepareAsync(DatabasePath, restore, () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance, waitForLock: TimeSpan.FromMilliseconds(300));
        var second = WindowsStartup.PrepareAsync(DatabasePath, restore, () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance, waitForLock: TimeSpan.FromMilliseconds(300));
        using var results = new CompositeResults(await Task.WhenAll(first, second));

        Assert.Equal(1, results.All.Count(result => result.Lock != null));
        Assert.Equal(1, results.All.Count(result => result.Failure?.Reason == StartupFailureReason.AlreadyRunning));
        Assert.Equal(1, restore.ApplyCalls);
    }

    private sealed class CompositeResults(StartupResult[] all) : IDisposable
    {
        public StartupResult[] All { get; } = all;

        public void Dispose()
        {
            foreach (var result in All)
            {
                result.Dispose();
            }
        }
    }

    [Fact]
    public async Task AnUnresolvedRestore_StopsTheStartWithoutCreatingADatabase_AndReleasesTheLock()
    {
        var restore = new RecordingRestore { Outcome = RestoreOutcomeKind.Unresolved };

        var result = await WindowsStartup.PrepareAsync(DatabasePath, restore, () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance);

        Assert.Equal(StartupFailureReason.RestoreUnresolved, result.Failure!.Reason);
        Assert.Null(result.Lock);
        Assert.False(File.Exists(DatabasePath));
        using var free = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));
        Assert.NotNull(free);
    }

    [Fact]
    public async Task ADatabaseFromANewerVersion_StopsTheStartAndReleasesTheLock()
    {
        await CreateUpgrader().UpgradeAsync();
        await DatabaseTestFiles.ExecuteAsync(DatabasePath, "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99999999999999_FromTheFuture', '99.0.0')");
        SqliteConnection.ClearAllPools();

        var result = await WindowsStartup.PrepareAsync(DatabasePath, new RecordingRestore(), () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance);

        Assert.Equal(StartupFailureReason.DatabaseNewerThanProgram, result.Failure!.Reason);
        Assert.Null(result.Lock);
        Assert.Contains("nyere versjon", result.Failure.Message);
        using var free = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));
        Assert.NotNull(free);
    }

    [Fact]
    public async Task AFailedMigration_ExplainsWhereTheBackupIs_AndDoesNotClaimTheDatabaseIsUnchangedWithoutChecking()
    {
        var options = new DbContextOptionsBuilder<SameieHubDbContext>().UseSqlite($"Data Source={DatabasePath};Pooling=False").Options;
        await using (var context = new SameieHubDbContext(options))
        {
            var first = context.Database.GetMigrations().First();
            await context.GetService<IMigrator>().MigrateAsync(first);
        }

        await DatabaseTestFiles.ExecuteAsync(DatabasePath, "ALTER TABLE HousingUnits ADD COLUMN OwnerName TEXT");
        SqliteConnection.ClearAllPools();

        var result = await WindowsStartup.PrepareAsync(DatabasePath, new RecordingRestore(), () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance);

        Assert.Equal(StartupFailureReason.MigrationFailed, result.Failure!.Reason);
        Assert.Contains(DatabaseUpgrader.BackupFolderName, result.Failure.Message);
        Assert.Contains("kontrollert", result.Failure.Message);
        Assert.Null(result.Lock);
    }

    [Fact]
    public async Task AnUnexpectedErrorFromTheRestore_StopsTheStartBeforeAnyDatabaseIsCreated_AndReleasesTheLock()
    {
        var restore = new RecordingRestore { ExceptionToThrow = new IOException("fictional disk problem") };
        var upgraded = false;

        var result = await WindowsStartup.PrepareAsync(DatabasePath, restore, () =>
        {
            upgraded = true;

            return Task.CompletedTask;
        }, NullLogger.Instance);

        Assert.Equal(StartupFailureReason.RestoreUnresolved, result.Failure!.Reason);
        Assert.Null(result.Lock);
        Assert.False(upgraded);
        Assert.False(File.Exists(DatabasePath));
        using var free = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));
        Assert.NotNull(free);
    }

    [Fact]
    public async Task AnUnexpectedErrorWhenUpgradingTheDatabase_GivesAReadableMessage_AndReleasesTheLock()
    {
        var result = await WindowsStartup.PrepareAsync(
            DatabasePath,
            new RecordingRestore(),
            () => throw new InvalidOperationException("fictional problem with Fiktivdalen 1A"),
            NullLogger.Instance);

        Assert.Equal(StartupFailureReason.UnexpectedError, result.Failure!.Reason);
        Assert.DoesNotContain("Fiktivdalen", result.Failure.Message);
        Assert.Null(result.Lock);
        using var free = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));
        Assert.NotNull(free);
    }

    [Fact]
    public async Task AnEmptyDatabaseFile_StopsTheStartWithAClearMessage_AndReleasesTheLock()
    {
        await File.WriteAllBytesAsync(DatabasePath, []);

        var result = await WindowsStartup.PrepareAsync(DatabasePath, new RecordingRestore(), () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance);

        Assert.Equal(StartupFailureReason.DatabaseFileEmpty, result.Failure!.Reason);
        Assert.Contains("tom", result.Failure.Message);
        Assert.Equal(0, new FileInfo(DatabasePath).Length);
        using var free = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));
        Assert.NotNull(free);
    }

    [Fact]
    public async Task ASecondStart_ThatReachesTheRunningProgram_AsksItToOpenTheBrowser_AndStopsQuietly()
    {
        var restore = new RecordingRestore();
        var asked = 0;
        using var firstInstance = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));

        var result = await WindowsStartup.PrepareAsync(DatabasePath, restore, () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance, () =>
        {
            asked++;

            return Task.FromResult(true);
        });

        Assert.NotNull(firstInstance);
        Assert.True(result.ActivatedRunningInstance);
        Assert.Null(result.Failure);
        Assert.Null(result.Lock);
        Assert.Equal(1, asked);
        Assert.Equal(0, restore.ApplyCalls);
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public async Task ASecondStart_ThatCannotReachTheRunningProgram_KeepsTheAlreadyRunningMessage()
    {
        using var firstInstance = InstanceLock.TryAcquire(InstanceLock.PathFor(DatabasePath));

        var result = await WindowsStartup.PrepareAsync(
            DatabasePath, new RecordingRestore(), () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance, () => Task.FromResult(false));

        Assert.False(result.ActivatedRunningInstance);
        Assert.Equal(StartupFailureReason.AlreadyRunning, result.Failure!.Reason);
    }

    [Fact]
    public async Task TheRunningProgramIsNotAskedToDoAnything_WhenTheLockIsFree()
    {
        var asked = 0;

        using var result = await WindowsStartup.PrepareAsync(DatabasePath, new RecordingRestore(), () => CreateUpgrader().UpgradeAsync(), NullLogger.Instance, () =>
        {
            asked++;

            return Task.FromResult(true);
        });

        Assert.Null(result.Failure);
        Assert.False(result.ActivatedRunningInstance);
        Assert.Equal(0, asked);
    }

    [Fact]
    public void EveryFailureHasAReadableNorwegianMessage()
    {
        foreach (var reason in Enum.GetValues<StartupFailureReason>())
        {
            var failure = StartupMessages.For(reason);

            Assert.False(string.IsNullOrWhiteSpace(failure.Message), reason.ToString());
            Assert.Equal(reason, failure.Reason);
        }
    }

    private DatabaseUpgrader CreateUpgrader()
    {
        _provider?.Dispose();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSameieHubInfrastructure(DatabasePath);
        _provider = services.BuildServiceProvider();

        return _provider.GetRequiredService<DatabaseUpgrader>();
    }

    private sealed class RecordingRestore : IDatabaseRestore
    {
        public int ApplyCalls { get; private set; }

        public RestoreOutcomeKind Outcome { get; init; } = RestoreOutcomeKind.NothingPending;

        public Exception? ExceptionToThrow { get; init; }

        public RestoreOutcome ApplyPendingRestore()
        {
            ApplyCalls++;
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return new RestoreOutcome(Outcome, DateTime.UtcNow, null);
        }

        public Task<RestorePreparation> PrepareAsync(string backupFilePath, CancellationToken cancellationToken = default, string? displayName = null) =>
            throw new NotSupportedException();

        public PendingRestore? GetPending() => null;

        public void CancelPending()
        {
        }

        public RestoreOutcome? GetLastOutcome() => null;

        public void ClearLastOutcome()
        {
        }
    }
}
