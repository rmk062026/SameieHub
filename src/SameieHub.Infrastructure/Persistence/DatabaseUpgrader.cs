using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Restore;

namespace SameieHub.Infrastructure.Persistence;

/// <summary>
/// Moves the database to the schema this program version expects, and protects the data while doing it: a database from a
/// newer version is never opened for writing, and an older one is backed up (with SQLite's backup API, so the copy is
/// consistent even when a WAL file exists) before the first migration runs. Used by the Windows host; the development
/// start keeps migrating directly.
/// </summary>
public sealed class DatabaseUpgrader
{
    public const string BackupFolderName = "pre-migration";
    public const string BackupFilePrefix = "sameiehub-before-upgrade-";

    private const int BackupsToKeep = 3;

    private readonly string _databasePath;
    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseUpgrader> _logger;

    public DatabaseUpgrader(
        string databasePath,
        IDbContextFactory<SameieHubDbContext> contextFactory,
        TimeProvider timeProvider,
        ILogger<DatabaseUpgrader> logger)
    {
        _databasePath = databasePath;
        _contextFactory = contextFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private string BackupFolder => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_databasePath))!, BackupFolderName);

    /// <exception cref="DatabaseUpgradeException">The database was not migrated. <see cref="DatabaseUpgradeException.Reason"/> says why.</exception>
    public async Task UpgradeAsync(CancellationToken cancellationToken = default)
    {
        var status = Inspect();
        var backupPath = status == BackupFileStatus.Older ? CreateBackup() : null;
        var lastMigrationBefore = File.Exists(_databasePath) && status != null ? DatabaseBackupValidator.FindLastMigrationId(_databasePath) : null;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var verified = status == BackupFileStatus.Older && IsUnchangedSince(lastMigrationBefore);
            _logger.LogError("The database migration failed ({ExceptionType}). Live database verified: {Verified}.", exception.GetType().Name, verified);

            throw new DatabaseUpgradeException(
                DatabaseUpgradeFailure.MigrationFailed,
                "The database could not be migrated.",
                backupPath,
                verified,
                exception);
        }

        if (backupPath != null)
        {
            DeleteOldBackups();
        }
    }

    /// <summary>
    /// Null means there is no database file at all, so migrating creates one. A file that exists is never created over or filled:
    /// an empty or invalid file stops the start, because it may be what is left of a database.
    /// </summary>
    private BackupFileStatus? Inspect()
    {
        if (!File.Exists(_databasePath))
        {
            return null;
        }

        if (new FileInfo(_databasePath).Length == 0)
        {
            _logger.LogError("The database file exists but is empty, so it was not changed.");
            throw new DatabaseUpgradeException(DatabaseUpgradeFailure.EmptyDatabaseFile, "The database file is empty.");
        }

        var inspection = BackupFileInspector.Inspect(_databasePath);
        switch (inspection.Status)
        {
            case BackupFileStatus.Newer:
                _logger.LogError("The database was made by a newer version of the program, so it was not opened for writing.");
                throw new DatabaseUpgradeException(DatabaseUpgradeFailure.NewerThanProgram, "The database was made by a newer version of the program.");
            case BackupFileStatus.NotSameieHub:
                _logger.LogError("The file at the database path is not a SameieHub database.");
                throw new DatabaseUpgradeException(DatabaseUpgradeFailure.NotSameieHubDatabase, "The file is not a SameieHub database.");
            case BackupFileStatus.Invalid:
                _logger.LogError("The database file is damaged or not readable ({ProblemCount} problems).", inspection.Problems.Count);
                throw new DatabaseUpgradeException(DatabaseUpgradeFailure.DamagedDatabase, "The database file is damaged or cannot be read.");
            default:
                return inspection.Status;
        }
    }

    /// <summary>
    /// Copies with SQLite's backup API from a read-only connection, so the copy is consistent even when a WAL file exists. The
    /// normal backup engine cannot be used here: it only accepts a database that already has every migration, and this copy is
    /// by definition of an older schema. The copy is written under a temporary name and only gets its final name when it is a
    /// readable, consistent copy of an older SameieHub database.
    /// </summary>
    private string CreateBackup()
    {
        var destination = Path.Combine(BackupFolder, $"{BackupFilePrefix}{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMdd-HHmmss}.db");
        var temporaryFile = Path.Combine(BackupFolder, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.partial");

        try
        {
            Directory.CreateDirectory(BackupFolder);
            CopyDatabase(temporaryFile);
            if (BackupFileInspector.Inspect(temporaryFile).Status != BackupFileStatus.Older)
            {
                throw new IOException("The copy failed its checks.");
            }

            // Without overwrite: an existing file is never replaced.
            File.Move(temporaryFile, destination);
            _logger.LogInformation("A backup was made before the database was upgraded ({SizeBytes} bytes).", new FileInfo(destination).Length);

            return destination;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            _logger.LogError("The backup before the upgrade could not be made ({ExceptionType}). The database was not migrated.", exception.GetType().Name);

            throw new DatabaseUpgradeException(
                DatabaseUpgradeFailure.BackupFailed,
                "A backup could not be made before the upgrade, so the database was not migrated.",
                innerException: exception);
        }
        finally
        {
            DeleteTemporaryFiles(temporaryFile);
        }
    }

    private void CopyDatabase(string temporaryFile)
    {
        var sourceString = new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        var targetString = new SqliteConnectionStringBuilder { DataSource = temporaryFile, Pooling = false }.ToString();

        using var source = new SqliteConnection(sourceString);
        using var target = new SqliteConnection(targetString);
        source.Open();
        target.Open();
        source.BackupDatabase(target);

        // A single file: no -wal or -shm files that could be left behind.
        using var journal = target.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode = DELETE";
        journal.ExecuteNonQuery();
    }

    private void DeleteTemporaryFiles(string temporaryFile)
    {
        foreach (var file in new[] { temporaryFile, temporaryFile + "-journal", temporaryFile + "-wal", temporaryFile + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("A temporary backup file could not be deleted ({ExceptionType}).", exception.GetType().Name);
            }
        }
    }

    /// <summary>After a failed migration: the history must be as before and the file must still pass SQLite's own checks.</summary>
    private bool IsUnchangedSince(string? lastMigrationBefore)
    {
        var inspection = BackupFileInspector.Inspect(_databasePath);

        return inspection.Status == BackupFileStatus.Older
            && DatabaseBackupValidator.FindLastMigrationId(_databasePath) == lastMigrationBefore;
    }

    private void DeleteOldBackups()
    {
        if (!Directory.Exists(BackupFolder))
        {
            return;
        }

        var oldFiles = Directory.GetFiles(BackupFolder, BackupFilePrefix + "*.db")
            .OrderByDescending(file => file, StringComparer.Ordinal)
            .Skip(BackupsToKeep);

        foreach (var file in oldFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("An old backup could not be deleted ({ExceptionType}).", exception.GetType().Name);
            }
        }
    }
}

public enum DatabaseUpgradeFailure
{
    /// <summary>The database has migrations this program does not know. It is not opened for writing.</summary>
    NewerThanProgram,

    /// <summary>A SQLite file that is not a SameieHub database.</summary>
    NotSameieHubDatabase,

    /// <summary>Not readable as SQLite, damaged, or with broken references.</summary>
    DamagedDatabase,

    /// <summary>The database file exists but has no content (0 bytes). It is neither filled nor replaced.</summary>
    EmptyDatabaseFile,

    /// <summary>The backup before the upgrade could not be made, so nothing was migrated.</summary>
    BackupFailed,

    /// <summary>A migration failed. The backup (<see cref="DatabaseUpgradeException.BackupPath"/>) is kept.</summary>
    MigrationFailed
}

/// <summary>The message never contains a path or any data from the database. The backup path is a separate property so the screen can show it.</summary>
public class DatabaseUpgradeException : Exception
{
    public DatabaseUpgradeException(
        DatabaseUpgradeFailure reason,
        string message,
        string? backupPath = null,
        bool liveDatabaseVerified = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        BackupPath = backupPath;
        LiveDatabaseVerified = liveDatabaseVerified;
    }

    public DatabaseUpgradeFailure Reason { get; }

    /// <summary>The backup made before the migration started, or null when none was made.</summary>
    public string? BackupPath { get; }

    /// <summary>
    /// True only when the database was checked after the failure: it still passes SQLite's checks and has exactly the migration
    /// history it had before. Row contents are not compared.
    /// </summary>
    public bool LiveDatabaseVerified { get; }
}
