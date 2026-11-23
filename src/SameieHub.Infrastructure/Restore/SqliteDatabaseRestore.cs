using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Infrastructure.Restore;

/// <summary>
/// Restore in two steps (see <see cref="IDatabaseRestore"/>). Everything lives in the folder <see cref="FolderName"/> next to the
/// database, on the same disk, so every move is atomic. The old database is moved aside, never copied or deleted, together with its
/// -wal, -shm and -journal files. The steps are written to a journal, so a crash at any point is undone or finished at the next start.
/// </summary>
public sealed class SqliteDatabaseRestore : IDatabaseRestore
{
    public const string FolderName = "restore";

    private const string PendingFileName = "pending.db";
    private const string ManifestFileName = "pending.json";
    private const string JournalFileName = "journal.json";
    private const string LastOutcomeFileName = "last-restore.json";
    private const string MovingOld = "MovingOld";
    private const string PlacingNew = "PlacingNew";
    private static readonly string[] SideFileSuffixes = ["-wal", "-shm", "-journal"];

    private readonly string _databasePath;
    private readonly string _restoreFolder;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteDatabaseRestore> _logger;
    private readonly Action<string>? _onStep;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    /// <param name="onStep">Called at named steps of the restore. Only tests use it, to simulate a crash by throwing.</param>
    public SqliteDatabaseRestore(string databasePath, TimeProvider timeProvider, ILogger<SqliteDatabaseRestore> logger, Action<string>? onStep = null)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _restoreFolder = Path.Combine(Path.GetDirectoryName(_databasePath)!, FolderName);
        _timeProvider = timeProvider;
        _logger = logger;
        _onStep = onStep;
    }

    private string PendingFile => Path.Combine(_restoreFolder, PendingFileName);

    private string ManifestFile => Path.Combine(_restoreFolder, ManifestFileName);

    private string JournalFile => Path.Combine(_restoreFolder, JournalFileName);

    private string LastOutcomeFile => Path.Combine(_restoreFolder, LastOutcomeFileName);

    // Step 1: prepare

    public async Task<RestorePreparation> PrepareAsync(string backupFilePath, CancellationToken cancellationToken = default, string? displayName = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _oneAtATime.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() => Prepare(backupFilePath, displayName, cancellationToken), cancellationToken);
        }
        catch (DatabaseRestoreException exception)
        {
            _logger.LogWarning("Restore could not be prepared: {Reason} ({InnerError}).", exception.Reason, exception.InnerException?.GetType().Name ?? "none");

            throw;
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    private RestorePreparation Prepare(string backupFilePath, string? displayName, CancellationToken cancellationToken)
    {
        RefuseTheDemoDatabase(backupFilePath);
        var source = CheckSourceFile(backupFilePath);
        var status = Inspect(source);

        EnsureRestoreFolder();
        if (File.Exists(JournalFile))
        {
            throw new DatabaseRestoreException(DatabaseRestoreFailure.WriteFailed, "An earlier restore has not been completed yet. Start the program first.");
        }

        DiscardPending();
        cancellationToken.ThrowIfCancellationRequested();

        CopyToWorkingFile(source);
        Step("staged-copy-written");

        var wasUpgraded = status == BackupFileStatus.Older;
        if (wasUpgraded)
        {
            Upgrade();
        }

        MakeSingleFile(PendingFile);
        var problems = DatabaseBackupValidator.FindProblems(PendingFile);
        if (problems.Count > 0)
        {
            DiscardPending();
            throw new DatabaseRestoreException(DatabaseRestoreFailure.InvalidBackup, "The prepared database failed its checks: " + string.Join(" ", problems));
        }

        var lastMigration = DatabaseBackupValidator.FindLastMigrationId(PendingFile) ?? string.Empty;
        var preparedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var manifest = new PendingManifest(ShownName(displayName, source), preparedAt, wasUpgraded, Hash(PendingFile), lastMigration);
        WriteJson(ManifestFile, manifest);

        _logger.LogInformation("Restore prepared (upgraded: {WasUpgraded}).", wasUpgraded);

        return new RestorePreparation(status, wasUpgraded, manifest.BackupFileName, preparedAt, lastMigration);
    }

    /// <summary>The name shown to the person: only a file name, never a path, and not too long.</summary>
    private static string ShownName(string? displayName, string source)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(source) : Path.GetFileName(displayName.Replace('\\', '/'));

        return name.Length > 80 ? name[..80] : name;
    }

    private void RefuseTheDemoDatabase(string backupFilePath)
    {
        var demoPath = DemoDatabaseLocation.GetPath();
        var sourceIsDemo = !string.IsNullOrWhiteSpace(backupFilePath) && Path.IsPathFullyQualified(backupFilePath)
            && DemoDatabaseSeeder.IsDemoDatabasePath(backupFilePath, demoPath);
        if (DemoDatabaseSeeder.IsDemoDatabasePath(_databasePath, demoPath) || sourceIsDemo)
        {
            throw new DatabaseRestoreException(DatabaseRestoreFailure.DemoDatabaseNotAllowed, "The demo database is never used for a restore.");
        }
    }

    private string CheckSourceFile(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath) || !Path.IsPathFullyQualified(backupFilePath))
        {
            throw new DatabaseRestoreException(DatabaseRestoreFailure.InvalidBackup, "The backup must be given as a full path to a file.");
        }

        var source = Path.GetFullPath(backupFilePath);
        foreach (var live in new[] { _databasePath }.Concat(SideFileSuffixes.Select(suffix => _databasePath + suffix)))
        {
            if (string.Equals(source, live, StringComparison.OrdinalIgnoreCase))
            {
                throw new DatabaseRestoreException(DatabaseRestoreFailure.SourceIsTheLiveDatabase, "The chosen file is the database the program is using.");
            }
        }

        if (source.StartsWith(_restoreFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
        {
            throw new DatabaseRestoreException(DatabaseRestoreFailure.InvalidBackup, "The backup file was not found.");
        }

        return source;
    }

    private static BackupFileStatus Inspect(string source)
    {
        var inspection = BackupFileInspector.Inspect(source);

        return inspection.Status switch
        {
            BackupFileStatus.Invalid => throw new DatabaseRestoreException(DatabaseRestoreFailure.InvalidBackup, "The file is not a usable database: " + string.Join(" ", inspection.Problems)),
            BackupFileStatus.NotSameieHub => throw new DatabaseRestoreException(DatabaseRestoreFailure.NotSameieHubDatabase, "The file is a database, but not a SameieHub database."),
            BackupFileStatus.Newer => throw new DatabaseRestoreException(DatabaseRestoreFailure.NewerVersion, "The backup was made by a newer version of SameieHub and cannot be restored by this version."),
            _ => inspection.Status
        };
    }

    private void EnsureRestoreFolder()
    {
        try
        {
            Directory.CreateDirectory(_restoreFolder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DatabaseRestoreException(DatabaseRestoreFailure.WriteFailed, "The restore folder could not be created.", exception);
        }
    }

    /// <summary>The working copy is made with SQLite's backup API from a read-only connection, never with File.Copy.</summary>
    private void CopyToWorkingFile(string source)
    {
        try
        {
            var sourceString = new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            var targetString = new SqliteConnectionStringBuilder { DataSource = PendingFile, Pooling = false }.ToString();
            using var from = new SqliteConnection(sourceString);
            using var to = new SqliteConnection(targetString);
            from.Open();
            to.Open();
            from.BackupDatabase(to);
        }
        catch (SqliteException exception)
        {
            DiscardPending();
            throw new DatabaseRestoreException(DatabaseRestoreFailure.InvalidBackup, "The backup could not be read.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiscardPending();
            throw new DatabaseRestoreException(DatabaseRestoreFailure.WriteFailed, "The working copy could not be written.", exception);
        }
    }

    /// <summary>An older backup is upgraded with EF Core's own migrations, on the working copy only.</summary>
    private void Upgrade()
    {
        try
        {
            var options = new DbContextOptionsBuilder<SameieHubDbContext>()
                .UseSqlite(new SqliteConnectionStringBuilder { DataSource = PendingFile, ForeignKeys = true, Pooling = false }.ToString())
                .Options;
            using var context = new SameieHubDbContext(options);
            context.Database.Migrate();
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or IOException)
        {
            DiscardPending();
            throw new DatabaseRestoreException(DatabaseRestoreFailure.UpgradeFailed, "The older backup could not be upgraded.", exception);
        }
    }

    /// <summary>One self-contained file: all changes written into it, and no -wal or -shm files that could be left behind.</summary>
    private static void MakeSingleFile(string file)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        foreach (var sql in new[] { "PRAGMA wal_checkpoint(TRUNCATE)", "PRAGMA journal_mode = DELETE" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    public PendingRestore? GetPending()
    {
        var manifest = ReadManifest();

        return manifest == null || !File.Exists(PendingFile)
            ? null
            : new PendingRestore(manifest.BackupFileName, manifest.PreparedAtUtc, manifest.WasUpgraded);
    }

    public void CancelPending()
    {
        _oneAtATime.Wait();
        try
        {
            DiscardPending();
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    // Step 2: apply at startup

    /// <summary>
    /// Three kinds of result matter to the caller. A restore that was carried out (<see cref="RestoreOutcomeKind.Applied"/>,
    /// <see cref="RestoreOutcomeKind.AppliedAfterRecovery"/>) leaves a database that was checked by its checksum. Nothing done, rejected,
    /// or rolled back leaves the original database, and a rollback is only reported when every original file is back with the checksum
    /// it had before. Everything else is <see cref="RestoreOutcomeKind.Unresolved"/>: nothing more is changed, every file is kept, and
    /// the program must not start.
    /// </summary>
    public RestoreOutcome ApplyPendingRestore()
    {
        _oneAtATime.Wait();
        try
        {
            if (!Directory.Exists(_restoreFolder))
            {
                return Nothing();
            }

            if (File.Exists(JournalFile))
            {
                return Record(Recover());
            }

            var manifest = ReadManifest();
            if (manifest == null)
            {
                DeleteStrayFiles();

                return Nothing();
            }

            return Record(Publish(manifest));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Not even the cause can be trusted to have left the database in a known state. Nothing more is touched.
            _logger.LogError("Restore could not be resolved safely ({InnerError}).", exception.GetType().Name);

            return Record(Unresolved());
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    private RestoreOutcome Publish(PendingManifest manifest)
    {
        if (!File.Exists(PendingFile) && File.Exists(_databasePath) && Hash(_databasePath) == manifest.Sha256)
        {
            // The new database was placed, and the program stopped before the manifest was deleted.
            DeleteManifest();

            return new RestoreOutcome(RestoreOutcomeKind.AppliedAfterRecovery, Now(), null);
        }

        if (!IsPreparedFileStillGood(manifest))
        {
            DiscardPending();

            return new RestoreOutcome(RestoreOutcomeKind.Rejected, Now(), null);
        }

        var existing = LiveFiles().Where(File.Exists).ToList();
        var previousName = NewPreviousFolderName();
        var previousFolder = Path.Combine(_restoreFolder, previousName);
        RestoreJournal journal;
        try
        {
            // The checksums are taken before anything moves, so a later rollback can prove that the originals are back.
            var files = existing.Select(file => new JournalEntry(Path.GetFileName(file), Hash(file))).ToList();
            journal = new RestoreJournal(MovingOld, previousName, File.Exists(_databasePath), files);
            WriteJournal(journal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing has been moved yet.
            _logger.LogWarning("Restore was not started ({InnerError}).", exception.GetType().Name);
            DeleteFile(JournalFile + ".tmp");
            DeleteFile(JournalFile);
            DiscardPending();

            return new RestoreOutcome(RestoreOutcomeKind.RolledBack, Now(), null);
        }

        Step("journal-written");

        try
        {
            if (existing.Count > 0)
            {
                Directory.CreateDirectory(previousFolder);
            }

            var moved = 0;
            foreach (var file in existing)
            {
                File.Move(file, Path.Combine(previousFolder, Path.GetFileName(file)));
                moved++;
                if (moved == 1)
                {
                    Step("first-old-file-moved");
                }
            }

            WriteJournal(journal with { State = PlacingNew });
            Step("all-old-files-moved");

            File.Move(PendingFile, _databasePath);
            Step("new-database-placed");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Typically a file that is still in use. Everything that was moved is put back, and that is checked.
            _logger.LogWarning("Restore was rolled back because a file could not be moved ({InnerError}).", exception.GetType().Name);

            return RollBack(journal);
        }

        // The journal goes first: if the program stops between the two, the checksum in the manifest still proves what is in place.
        DeleteJournal();
        DeleteManifest();

        return new RestoreOutcome(RestoreOutcomeKind.Applied, Now(), existing.Count > 0 ? previousName : null);
    }

    /// <summary>
    /// Finishes or undoes a restore that was interrupted. It is only finished when the file on the database path is proven to be the
    /// prepared database by its checksum, and it is only undone when every original file is back with its recorded checksum.
    /// </summary>
    private RestoreOutcome Recover()
    {
        var journal = ReadJournal();
        if (journal == null)
        {
            _logger.LogError("The restore journal is damaged or incomplete.");

            return Unresolved();
        }

        if (journal.State == PlacingNew && TryFinishPlacingTheNewDatabase())
        {
            DeleteJournal();
            DeleteManifest();
            _logger.LogInformation("An interrupted restore was finished.");

            return new RestoreOutcome(RestoreOutcomeKind.AppliedAfterRecovery, Now(), journal.Files.Count > 0 ? journal.PreviousFolderName : null);
        }

        return RollBack(journal);
    }

    /// <summary>True only if the prepared database is now in place, proven by the checksum from the manifest.</summary>
    private bool TryFinishPlacingTheNewDatabase()
    {
        var manifest = ReadManifest();
        if (manifest == null)
        {
            return false;
        }

        if (File.Exists(_databasePath))
        {
            if (Hash(_databasePath) != manifest.Sha256)
            {
                return false;
            }

            DeleteFile(PendingFile);

            return true;
        }

        if (!File.Exists(PendingFile) || Hash(PendingFile) != manifest.Sha256)
        {
            return false;
        }

        File.Move(PendingFile, _databasePath);

        return true;
    }

    private RestoreOutcome RollBack(RestoreJournal journal)
    {
        var previousFolder = Path.Combine(_restoreFolder, journal.PreviousFolderName);
        PutOldFilesBack(previousFolder, journal.Files);

        if (!AreTheOriginalsBack(journal))
        {
            _logger.LogError("The original database could not be confirmed after the rollback. Nothing more is changed.");

            return Unresolved();
        }

        DeleteJournal();
        DiscardPending();
        _logger.LogWarning("A restore was undone and the original database was confirmed.");

        return new RestoreOutcome(RestoreOutcomeKind.RolledBack, Now(), null);
    }

    private void PutOldFilesBack(string previousFolder, IEnumerable<JournalEntry> files)
    {
        foreach (var file in files)
        {
            var moved = Path.Combine(previousFolder, file.Name);
            var original = Path.Combine(DatabaseFolder, file.Name);
            if (File.Exists(moved) && !File.Exists(original))
            {
                File.Move(moved, original);
                Step("old-file-put-back");
            }
        }

        if (Directory.Exists(previousFolder) && !Directory.EnumerateFileSystemEntries(previousFolder).Any())
        {
            Directory.Delete(previousFolder);
        }
    }

    /// <summary>Every original file must be where it was, with exactly the bytes it had before the restore started.</summary>
    private bool AreTheOriginalsBack(RestoreJournal journal)
    {
        foreach (var file in journal.Files)
        {
            var path = Path.Combine(DatabaseFolder, file.Name);
            if (!File.Exists(path) || Hash(path) != file.Sha256)
            {
                return false;
            }
        }

        // If there was a database, it is among the files above. If there was none, nothing may be in its place.
        return journal.HadDatabase || !File.Exists(_databasePath);
    }

    private bool IsPreparedFileStillGood(PendingManifest manifest)
    {
        if (!File.Exists(PendingFile) || Hash(PendingFile) != manifest.Sha256)
        {
            return false;
        }

        return BackupFileInspector.Inspect(PendingFile).Status == BackupFileStatus.Current
            && DatabaseBackupValidator.FindProblems(PendingFile).Count == 0;
    }

    private RestoreJournal? ReadJournal()
    {
        try
        {
            var journal = ReadJson<RestoreJournal>(JournalFile);
            var valid = journal != null
                && (journal.State == MovingOld || journal.State == PlacingNew)
                && !string.IsNullOrWhiteSpace(journal.PreviousFolderName)
                && journal.PreviousFolderName == Path.GetFileName(journal.PreviousFolderName)
                && journal.Files != null
                && journal.Files.All(file => !string.IsNullOrWhiteSpace(file.Name) && file.Name == Path.GetFileName(file.Name) && !string.IsNullOrEmpty(file.Sha256));

            return valid ? journal : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private RestoreOutcome Unresolved() => new(RestoreOutcomeKind.Unresolved, Now(), null);

    public RestoreOutcome? GetLastOutcome()
    {
        return File.Exists(LastOutcomeFile) ? ReadJson<RestoreOutcome>(LastOutcomeFile) : null;
    }

    public void ClearLastOutcome()
    {
        DeleteFile(LastOutcomeFile);
    }

    private RestoreOutcome Record(RestoreOutcome outcome)
    {
        _logger.LogInformation("Restore result: {Kind}.", outcome.Kind);
        try
        {
            WriteJson(LastOutcomeFile, outcome);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("The result of the restore could not be saved ({InnerError}).", exception.GetType().Name);
        }

        return outcome;
    }

    // Files

    private string DatabaseFolder => Path.GetDirectoryName(_databasePath)!;

    private IEnumerable<string> LiveFiles()
    {
        yield return _databasePath;
        foreach (var suffix in SideFileSuffixes)
        {
            yield return _databasePath + suffix;
        }
    }

    private string NewPreviousFolderName()
    {
        var baseName = $"before-restore-{_timeProvider.GetUtcNow():yyyyMMddHHmmss}";
        var name = baseName;
        for (var number = 2; Directory.Exists(Path.Combine(_restoreFolder, name)); number++)
        {
            name = $"{baseName}-{number}";
        }

        return name;
    }

    private void DiscardPending()
    {
        DeleteManifest();
        DeleteStrayFiles();
    }

    private void DeleteManifest()
    {
        DeleteFile(ManifestFile);
    }

    private void DeleteJournal()
    {
        DeleteFile(JournalFile);
    }

    private void DeleteStrayFiles()
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            DeleteFile(PendingFile + suffix);
        }

        DeleteFile(ManifestFile + ".tmp");
    }

    private void DeleteFile(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("A restore file could not be deleted ({InnerError}).", exception.GetType().Name);
        }
    }

    private void WriteJournal(RestoreJournal journal)
    {
        WriteJson(JournalFile, journal);
    }

    private PendingManifest? ReadManifest()
    {
        if (!File.Exists(ManifestFile))
        {
            return null;
        }

        try
        {
            return ReadJson<PendingManifest>(ManifestFile);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Written to a temporary file and then moved over the real one, so a reader never sees half a file.</summary>
    private static void WriteJson<T>(string file, T value)
    {
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value));
        File.Move(temporary, file, overwrite: true);
    }

    private static T? ReadJson<T>(string file)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(file));
    }

    private static string Hash(string file)
    {
        using var stream = File.OpenRead(file);

        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private void Step(string name)
    {
        _onStep?.Invoke(name);
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;

    private RestoreOutcome Nothing() => new(RestoreOutcomeKind.NothingPending, Now(), null);

    private sealed record PendingManifest(string BackupFileName, DateTime PreparedAtUtc, bool WasUpgraded, string Sha256, string LastMigrationId);

    private sealed record JournalEntry(string Name, string Sha256);

    private sealed record RestoreJournal(string State, string PreviousFolderName, bool HadDatabase, List<JournalEntry> Files);
}
