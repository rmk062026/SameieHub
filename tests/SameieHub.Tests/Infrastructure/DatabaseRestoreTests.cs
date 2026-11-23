using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Domain;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Infrastructure.Restore;
using SameieHub.Tests.Fakes;
using static SameieHub.Tests.Infrastructure.DatabaseTestData;
using static SameieHub.Tests.Infrastructure.DatabaseTestFiles;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// The restore engine against real temporary SQLite databases. "Stopping the program" is simulated by closing every
/// connection to the test database, and a crash is simulated by an exception at a named step. The ordinary AppData
/// database is never used.
/// </summary>
public class DatabaseRestoreTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private string _folder = null!;
    private string _backup = null!;
    private Dictionary<string, string> _stateAtBackup = null!;
    private Dictionary<string, string> _stateNow = null!;

    private string LivePath => _database.DatabasePath;

    private string RestoreFolder => Path.Combine(Path.GetDirectoryName(LivePath)!, SqliteDatabaseRestore.FolderName);

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
        _folder = Path.Combine(Path.GetTempPath(), "SameieHubRestoreTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_folder);

        await FillAllModulesAsync(_database);
        _backup = Path.Combine(_folder, "sikkerhetskopi.db");
        await new SqliteDatabaseBackup(LivePath, _database.Clock, NullLogger<SqliteDatabaseBackup>.Instance).CreateBackupAsync(_backup);
        _stateAtBackup = await SnapshotAsync(_backup);

        // The program goes on after the backup, so the live database differs from the backup.
        await _database.Service.RegisterAsync("Fiktivdalen 55 (etter sikkerhetskopien)");
        _stateNow = await SnapshotAsync(LivePath);
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

    private SqliteDatabaseRestore NewRestore(string? databasePath = null, Action<string>? onStep = null, ILoggerProvider? logs = null)
    {
        ILogger<SqliteDatabaseRestore> logger = NullLogger<SqliteDatabaseRestore>.Instance;
        if (logs != null)
        {
            logger = new LoggerFactory([logs]).CreateLogger<SqliteDatabaseRestore>();
        }

        return new SqliteDatabaseRestore(databasePath ?? LivePath, _database.Clock, logger, onStep);
    }

    /// <summary>The program is stopped: no connection to the database is open any more.</summary>
    private Task StopProgramAsync() => _database.StopForResetAsync();

    private static Action<string> CrashAt(string step) => reached =>
    {
        if (reached == step)
        {
            throw new SimulatedCrashException(step);
        }
    };

    private string[] SideFilesNextToTheLiveDatabase() =>
        new[] { "-wal", "-shm", "-journal" }.Select(suffix => LivePath + suffix).Where(File.Exists).ToArray();

    private static void CreateSideFiles(string databasePath)
    {
        // SQLite normally removes its side files when the last connection closes, but a virus scanner or a crash can leave them.
        // A real one is never overwritten: it may hold committed data, and the restore must move it along with the database.
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            if (!File.Exists(databasePath + suffix))
            {
                File.WriteAllBytes(databasePath + suffix, Array.Empty<byte>());
            }
        }
    }

    // The normal restore

    [Fact]
    public async Task Restore_ReplacesTheDatabaseWithTheBackupAndKeepsEveryValueExactly()
    {
        var restore = NewRestore();

        var preparation = await restore.PrepareAsync(_backup);
        await StopProgramAsync();
        var outcome = restore.ApplyPendingRestore();

        Assert.Equal(BackupFileStatus.Current, preparation.Status);
        Assert.False(preparation.WasUpgraded);
        Assert.Equal(RestoreOutcomeKind.Applied, outcome.Kind);
        var restored = await SnapshotAsync(LivePath);
        foreach (var table in _stateAtBackup.Keys)
        {
            Assert.NotEqual("[]", _stateAtBackup[table]);
            Assert.Equal(_stateAtBackup[table], restored[table]);
        }

        Assert.NotEqual(_stateNow["HousingUnits"], restored["HousingUnits"]);
        const string kwhSql = "SELECT group_concat(Kwh, ';') FROM (SELECT Kwh FROM MeterReadings ORDER BY Id)";
        Assert.Equal(await ScalarAsync(_backup, kwhSql), await ScalarAsync(LivePath, kwhSql));
        Assert.Empty(DatabaseBackupValidator.FindProblems(LivePath));
        Assert.Empty(SideFilesNextToTheLiveDatabase());
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.json")));
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "journal.json")));
        Assert.Null(restore.GetPending());
    }

    [Fact]
    public async Task Restore_MovesTheCurrentDatabaseAndItsSideFilesAsideBeforeReplacingIt()
    {
        var restore = NewRestore();
        await restore.PrepareAsync(_backup);
        await StopProgramAsync();
        CreateSideFiles(LivePath);

        var outcome = restore.ApplyPendingRestore();

        var preserved = Path.Combine(RestoreFolder, outcome.PreviousDatabaseFolderName!);
        var preservedDatabase = Path.Combine(preserved, Path.GetFileName(LivePath));
        Assert.Equal(_stateNow, await SnapshotAsync(preservedDatabase));
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            Assert.True(File.Exists(preservedDatabase + suffix));
        }

        // A side file of the old database next to the new one could corrupt it, so none may remain.
        Assert.Empty(SideFilesNextToTheLiveDatabase());
    }

    [Fact]
    public async Task Restore_NeverChangesTheBackupFile()
    {
        var before = await HashAsync(_backup);
        var restore = NewRestore();

        await restore.PrepareAsync(_backup);
        await StopProgramAsync();
        restore.ApplyPendingRestore();

        Assert.Equal(before, await HashAsync(_backup));
    }

    [Fact]
    public async Task Restore_IntoAFreshInstallationWithoutADatabase_Works()
    {
        var freshFolder = Path.Combine(_folder, "ny-installasjon");
        Directory.CreateDirectory(freshFolder);
        var freshPath = Path.Combine(freshFolder, "sameiehub.db");
        var restore = NewRestore(freshPath);

        await restore.PrepareAsync(_backup);
        var outcome = restore.ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.Applied, outcome.Kind);
        Assert.Null(outcome.PreviousDatabaseFolderName);
        Assert.Equal(_stateAtBackup, await SnapshotAsync(freshPath));
    }

    // Backups that are refused

    [Theory]
    [InlineData("missing", DatabaseRestoreFailure.InvalidBackup)]
    [InlineData("text", DatabaseRestoreFailure.InvalidBackup)]
    [InlineData("truncated", DatabaseRestoreFailure.InvalidBackup)]
    [InlineData("otherDatabase", DatabaseRestoreFailure.NotSameieHubDatabase)]
    [InlineData("newerVersion", DatabaseRestoreFailure.NewerVersion)]
    [InlineData("liveDatabase", DatabaseRestoreFailure.SourceIsTheLiveDatabase)]
    public async Task Prepare_BackupThatCannotBeUsed_IsRefusedAndNothingIsChanged(string kind, DatabaseRestoreFailure expected)
    {
        var file = Path.Combine(_folder, "valgt.db");
        switch (kind)
        {
            case "text":
                await File.WriteAllTextAsync(file, "dette er ikke en database, bare tekst som er lang nok til å se ut som en fil");
                break;
            case "truncated":
                await File.WriteAllBytesAsync(file, (await File.ReadAllBytesAsync(_backup))[..((int)new FileInfo(_backup).Length / 2)]);
                break;
            case "otherDatabase":
                await ExecuteAsync(file, "CREATE TABLE Kunder (Id INTEGER PRIMARY KEY, Navn TEXT)");
                break;
            case "newerVersion":
                await CopyAsync(_backup, file);
                await ExecuteAsync(file, "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29990101000000_FraEnNyereVersjon', '99.0.0')");
                break;
            case "liveDatabase":
                file = LivePath;
                break;
        }

        // The program is still running, so the file itself may change when SQLite checkpoints; the stored data must not.
        var liveBefore = await SnapshotAsync(LivePath);
        var restore = NewRestore();

        var exception = await Assert.ThrowsAsync<DatabaseRestoreException>(() => restore.PrepareAsync(file));

        Assert.Equal(expected, exception.Reason);
        Assert.Null(restore.GetPending());
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
        Assert.Equal(liveBefore, await SnapshotAsync(LivePath));
    }

    [Fact]
    public async Task Prepare_TheDemoDatabase_IsNeverUsedAsTargetOrAsSource()
    {
        var demoPath = DemoDatabaseLocation.GetPath();
        var demoFolder = Path.GetDirectoryName(demoPath)!;
        var demoBefore = new FileInfo(demoPath);
        var existed = demoBefore.Exists;
        var length = existed ? demoBefore.Length : 0;

        var asTarget = await Assert.ThrowsAsync<DatabaseRestoreException>(() => NewRestore(demoPath).PrepareAsync(_backup));
        var asSource = await Assert.ThrowsAsync<DatabaseRestoreException>(() => NewRestore().PrepareAsync(demoPath));

        Assert.Equal(DatabaseRestoreFailure.DemoDatabaseNotAllowed, asTarget.Reason);
        Assert.Equal(DatabaseRestoreFailure.DemoDatabaseNotAllowed, asSource.Reason);
        Assert.False(Directory.Exists(Path.Combine(demoFolder, SqliteDatabaseRestore.FolderName)));
        Assert.Equal(existed, File.Exists(demoPath));
        if (existed)
        {
            Assert.Equal(length, new FileInfo(demoPath).Length);
        }
    }

    // Older versions

    [Fact]
    public async Task Restore_OlderVersion_IsUpgradedOnAWorkingCopyAndNeverInTheBackupFile()
    {
        var olderFile = Path.Combine(_folder, "eldre.db");
        await using (var older = await SqliteTestDatabase.CreateUnmigratedAsync())
        {
            await older.MigrateToMigrationAsync("AddTaskItems");
            await older.Service.RegisterAsync(new HousingUnitDetails { Address = "Fiktivdalen 3", OwnerName = OwnerName, OwnerPhoneNumber = OwnerPhone });
            await older.StopForResetAsync();
            await CopyAsync(older.DatabasePath, olderFile);
        }

        var hashBefore = await HashAsync(olderFile);
        var restore = NewRestore();

        var preparation = await restore.PrepareAsync(olderFile);
        await StopProgramAsync();
        var outcome = restore.ApplyPendingRestore();

        Assert.Equal(BackupFileStatus.Older, preparation.Status);
        Assert.True(preparation.WasUpgraded);
        Assert.Equal(hashBefore, await HashAsync(olderFile));
        Assert.Equal(RestoreOutcomeKind.Applied, outcome.Kind);
        Assert.Empty(DatabaseBackupValidator.FindProblems(LivePath));
        var restored = await SnapshotAsync(LivePath);
        Assert.Contains(OwnerName, restored["HousingUnits"]);
        Assert.Equal("[]", restored["ChargingFacilities"]);
    }

    // Failures before publishing leave the live database as it was

    [Fact]
    public async Task Prepare_FailureWhileStaging_LeavesNoPendingRestoreAndTheStrayFileIsCleanedUp()
    {
        await Assert.ThrowsAsync<SimulatedCrashException>(() => NewRestore(onStep: CrashAt("staged-copy-written")).PrepareAsync(_backup));

        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.json")));
        Assert.True(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
        await StopProgramAsync();
        var liveBefore = await HashAsync(LivePath);
        var outcome = NewRestore().ApplyPendingRestore();
        Assert.Equal(RestoreOutcomeKind.NothingPending, outcome.Kind);
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
        Assert.Equal(liveBefore, await HashAsync(LivePath));
    }

    [Fact]
    public async Task Prepare_WhenTheRestoreFolderCannotBeCreated_FailsAndKeepsTheDatabase()
    {
        await File.WriteAllTextAsync(RestoreFolder, "en fil står i veien for mappen");
        var liveBefore = await SnapshotAsync(LivePath);

        var exception = await Assert.ThrowsAsync<DatabaseRestoreException>(() => NewRestore().PrepareAsync(_backup));

        Assert.Equal(DatabaseRestoreFailure.WriteFailed, exception.Reason);
        Assert.Equal(liveBefore, await SnapshotAsync(LivePath));
    }

    [Fact]
    public async Task Apply_NothingPending_ChangesNothingAndCreatesNoFolder()
    {
        await StopProgramAsync();
        var liveBefore = await HashAsync(LivePath);

        var outcome = NewRestore().ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.NothingPending, outcome.Kind);
        Assert.Equal(liveBefore, await HashAsync(LivePath));
        Assert.False(Directory.Exists(RestoreFolder));
    }

    [Fact]
    public async Task Apply_PreparedFileThatChangedAfterPreparing_IsRejectedAndTheDatabaseIsKept()
    {
        var restore = NewRestore();
        await restore.PrepareAsync(_backup);
        await using (var stream = new FileStream(Path.Combine(RestoreFolder, "pending.db"), FileMode.Append))
        {
            stream.WriteByte(0);
        }

        await StopProgramAsync();
        var liveBefore = await HashAsync(LivePath);
        var outcome = restore.ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.Rejected, outcome.Kind);
        Assert.Equal(liveBefore, await HashAsync(LivePath));
        Assert.Null(restore.GetPending());
        Assert.Equal(_stateNow, await SnapshotAsync(LivePath));
    }

    [Fact]
    public async Task Apply_WhenTheDatabaseIsStillInUse_RollsBackAndKeepsTheOriginalWithItsSideFiles()
    {
        var restore = NewRestore();
        await restore.PrepareAsync(_backup);
        await StopProgramAsync();
        CreateSideFiles(LivePath);

        RestoreOutcome outcome;
        await using (new FileStream(LivePath + "-journal", FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // The last file to be moved is locked, so the move fails after the first ones were moved.
            outcome = restore.ApplyPendingRestore();
        }

        Assert.Equal(RestoreOutcomeKind.RolledBack, outcome.Kind);
        Assert.Equal(_stateNow, await SnapshotAsync(LivePath));
        Assert.Equal(3, SideFilesNextToTheLiveDatabase().Length);
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "journal.json")));
        Assert.Null(restore.GetPending());
    }

    // A crash while publishing

    [Theory]
    [InlineData("journal-written", false)]
    [InlineData("first-old-file-moved", false)]
    [InlineData("all-old-files-moved", true)]
    [InlineData("new-database-placed", true)]
    public async Task Apply_CrashAtACriticalPoint_IsRecoveredAtTheNextStartup(string crashPoint, bool expectRestored)
    {
        await NewRestore().PrepareAsync(_backup);
        await StopProgramAsync();
        CreateSideFiles(LivePath);

        Assert.Throws<SimulatedCrashException>(() => NewRestore(onStep: CrashAt(crashPoint)).ApplyPendingRestore());
        var outcome = NewRestore().ApplyPendingRestore();

        Assert.Equal(expectRestored ? RestoreOutcomeKind.AppliedAfterRecovery : RestoreOutcomeKind.RolledBack, outcome.Kind);
        Assert.True(File.Exists(LivePath), "There is always exactly one database after recovery.");
        var expectedState = expectRestored ? _stateAtBackup : _stateNow;
        var actualState = await SnapshotAsync(LivePath);
        foreach (var table in expectedState.Keys)
        {
            Assert.True(expectedState[table] == actualState[table], $"Table {table} differs: expected {expectedState[table].Length} chars, got {actualState[table].Length}.");
        }

        Assert.Equal(expectRestored ? 0 : 3, SideFilesNextToTheLiveDatabase().Length);
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "journal.json")));
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.json")));
        Assert.Equal(RestoreOutcomeKind.NothingPending, NewRestore().ApplyPendingRestore().Kind);
    }

    // Safety check (M5B-S): a restore that cannot be resolved safely must stop the program and keep every file

    private string PreviousFolder() => Directory.GetDirectories(RestoreFolder, "before-restore-*").Single();

    private async Task<string> CrashAfterAsync(string step)
    {
        await NewRestore().PrepareAsync(_backup);
        await StopProgramAsync();
        CreateSideFiles(LivePath);
        Assert.Throws<SimulatedCrashException>(() => NewRestore(onStep: CrashAt(step)).ApplyPendingRestore());

        return PreviousFolder();
    }

    [Fact]
    public async Task Recovery_AnArbitraryFileOnTheDatabasePath_IsNoProofThatTheNewDatabaseIsInPlace()
    {
        await CrashAfterAsync("all-old-files-moved");
        await ExecuteAsync(LivePath, "CREATE TABLE Tom (Id INTEGER PRIMARY KEY)");

        var outcome = NewRestore().ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.Unresolved, outcome.Kind);
        Assert.True(File.Exists(Path.Combine(RestoreFolder, "journal.json")));
        Assert.True(File.Exists(Path.Combine(RestoreFolder, "pending.db")), "The prepared database is kept.");
        Assert.True(File.Exists(Path.Combine(PreviousFolder(), Path.GetFileName(LivePath))), "The old database is kept.");
    }

    [Fact]
    public async Task Recovery_RollbackIsOnlyConfirmedWhenTheOriginalFilesAreActuallyBack()
    {
        await CrashAfterAsync("first-old-file-moved");
        await ExecuteAsync(LivePath, "CREATE TABLE Tom (Id INTEGER PRIMARY KEY)");

        var outcome = NewRestore().ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.Unresolved, outcome.Kind);
        Assert.True(File.Exists(Path.Combine(RestoreFolder, "journal.json")));
        Assert.True(File.Exists(Path.Combine(PreviousFolder(), Path.GetFileName(LivePath))), "The old database is kept.");
    }

    [Theory]
    [InlineData("dette er ikke json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"State\":\"Ukjent\",\"PreviousFolderName\":\"x\",\"HadDatabase\":true,\"Files\":[]}")]
    public async Task Recovery_DamagedOrIncompleteJournal_IsUnresolvedAndNothingIsDeleted(string journalContent)
    {
        await NewRestore().PrepareAsync(_backup);
        await StopProgramAsync();
        await File.WriteAllTextAsync(Path.Combine(RestoreFolder, "journal.json"), journalContent);
        var liveBefore = await HashAsync(LivePath);

        var outcome = NewRestore().ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.Unresolved, outcome.Kind);
        Assert.Equal(journalContent, await File.ReadAllTextAsync(Path.Combine(RestoreFolder, "journal.json")));
        Assert.True(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
        Assert.True(File.Exists(Path.Combine(RestoreFolder, "pending.json")));
        Assert.Equal(liveBefore, await HashAsync(LivePath));
    }

    [Fact]
    public async Task Recovery_InterruptedWhileRollingBack_CanBeResumedAndEndsWithTheOriginal()
    {
        await CrashAfterAsync("first-old-file-moved");

        Assert.Throws<SimulatedCrashException>(() => NewRestore(onStep: CrashAt("old-file-put-back")).ApplyPendingRestore());
        var outcome = NewRestore().ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.RolledBack, outcome.Kind);
        Assert.Equal(_stateNow, await SnapshotAsync(LivePath));
        Assert.Equal(3, SideFilesNextToTheLiveDatabase().Length);
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "journal.json")));
    }

    [Fact]
    public async Task Recovery_LockedFile_IsUnresolvedUntilItIsFreeAndNothingIsLostMeanwhile()
    {
        var previous = await CrashAfterAsync("first-old-file-moved");
        RestoreOutcome whileLocked;
        await using (new FileStream(Path.Combine(previous, Path.GetFileName(LivePath)), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            whileLocked = NewRestore().ApplyPendingRestore();
        }

        Assert.Equal(RestoreOutcomeKind.Unresolved, whileLocked.Kind);
        Assert.True(File.Exists(Path.Combine(RestoreFolder, "journal.json")));
        Assert.False(File.Exists(LivePath), "No database is created or invented in the meantime.");

        var afterwards = NewRestore().ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.RolledBack, afterwards.Kind);
        Assert.Equal(_stateNow, await SnapshotAsync(LivePath));
    }

    [Fact]
    public async Task Recovery_NewDatabaseInPlaceButJournalGone_IsRecognisedByItsChecksum()
    {
        await CrashAfterAsync("new-database-placed");
        File.Delete(Path.Combine(RestoreFolder, "journal.json"));

        var outcome = NewRestore().ApplyPendingRestore();

        Assert.Equal(RestoreOutcomeKind.AppliedAfterRecovery, outcome.Kind);
        Assert.Equal(_stateAtBackup, await SnapshotAsync(LivePath));
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.json")));
    }

    // Pending restore and the last result

    [Fact]
    public async Task Pending_CanBeReadAndCancelled_AndTheLastOutcomeIsRemembered()
    {
        var restore = NewRestore();
        await restore.PrepareAsync(_backup);

        var pending = restore.GetPending();
        restore.CancelPending();

        Assert.Equal("sikkerhetskopi.db", pending!.BackupFileName);
        Assert.Null(restore.GetPending());
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.db")));

        await restore.PrepareAsync(_backup);
        await StopProgramAsync();
        var outcome = restore.ApplyPendingRestore();

        Assert.Equal(outcome.Kind, restore.GetLastOutcome()!.Kind);
    }

    // Personal data and the ordinary database

    [Fact]
    public async Task Restore_LogsNeitherPersonalDataNorPaths()
    {
        var logs = new CapturingLoggerProvider();
        var restore = NewRestore(logs: logs);

        await restore.PrepareAsync(_backup);
        await StopProgramAsync();
        restore.ApplyPendingRestore();
        await Assert.ThrowsAsync<DatabaseRestoreException>(() => restore.PrepareAsync(Path.Combine(_folder, "finnes-ikke.db")));

        Assert.NotEmpty(logs.Messages);
        var everything = string.Join("\n", logs.Messages);
        foreach (var secret in new[] { OwnerName, OwnerPhone, OwnerEmail, AssignedTo, _folder, Path.GetTempPath(), "sikkerhetskopi.db" })
        {
            Assert.DoesNotContain(secret, everything);
        }
    }

    [Fact]
    public async Task Restore_NeverTouchesTheOrdinaryAppDataDatabase()
    {
        var ordinary = new FileInfo(DatabaseLocation.GetDefaultPath());
        var existed = ordinary.Exists;
        var length = existed ? ordinary.Length : 0;
        var written = existed ? ordinary.LastWriteTimeUtc : default;
        var restore = NewRestore();

        await restore.PrepareAsync(_backup);
        await StopProgramAsync();
        restore.ApplyPendingRestore();

        var after = new FileInfo(DatabaseLocation.GetDefaultPath());
        Assert.Equal(existed, after.Exists);
        if (existed)
        {
            Assert.Equal(length, after.Length);
            Assert.Equal(written, after.LastWriteTimeUtc);
        }

        Assert.NotEqual(Path.GetFullPath(DatabaseLocation.GetDefaultPath()), Path.GetFullPath(LivePath));
    }

    private sealed class SimulatedCrashException : Exception
    {
        public SimulatedCrashException(string step)
            : base("Simulated crash at " + step)
        {
        }
    }
}
