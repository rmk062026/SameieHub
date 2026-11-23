using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace SameieHub.Infrastructure.Backup;

/// <summary>
/// Backs up the SQLite database with SQLite's own online backup (<see cref="SqliteConnection.BackupDatabase"/>), which
/// takes a consistent snapshot even while the program is writing. The copy is written to a temporary file next to the
/// destination, checked, and only then given its final name. The original is only read.
/// </summary>
public sealed class SqliteDatabaseBackup : IDatabaseBackup
{
    private const string TemporaryExtension = ".partial";

    private readonly string _databasePath;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqliteDatabaseBackup> _logger;
    private readonly SemaphoreSlim _oneBackupAtATime = new(1, 1);

    public SqliteDatabaseBackup(string databasePath, TimeProvider timeProvider, ILogger<SqliteDatabaseBackup> logger)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DatabaseBackupResult> CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var destination = CheckDestination(destinationPath);

            await _oneBackupAtATime.WaitAsync(cancellationToken);
            try
            {
                var result = await Task.Run(() => CreateBackup(destination, cancellationToken), cancellationToken);
                _logger.LogInformation("Database backup created: {FileName}, {SizeBytes} bytes.", Path.GetFileName(result.FilePath), result.SizeBytes);

                return result;
            }
            finally
            {
                _oneBackupAtATime.Release();
            }
        }
        catch (DatabaseBackupException exception)
        {
            // Only the reason and the kind of inner error are logged: messages can contain paths.
            _logger.LogWarning("Database backup failed: {Reason} ({InnerError}).", exception.Reason, exception.InnerException?.GetType().Name ?? "none");

            throw;
        }
    }

    private string CheckDestination(string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath) || !Path.IsPathFullyQualified(destinationPath))
        {
            throw Invalid("The destination must be a full path to a file.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(destinationPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.InvalidDestination, "The destination is not a valid path.", exception);
        }

        var fileName = Path.GetFileName(fullPath);
        if (fileName.Length == 0 || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw Invalid("The destination must end with a valid file name.");
        }

        if (Directory.Exists(fullPath))
        {
            throw Invalid("The destination is a folder, not a file.");
        }

        if (IsTheDatabaseItself(fullPath))
        {
            throw Invalid("The destination cannot be the database itself.");
        }

        var folder = Path.GetDirectoryName(fullPath);
        if (folder == null || !Directory.Exists(folder))
        {
            throw Invalid("The destination folder does not exist.");
        }

        if (File.Exists(fullPath))
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.DestinationExists, "A file with this name already exists. It is not overwritten.");
        }

        return fullPath;
    }

    private bool IsTheDatabaseItself(string fullPath)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            if (string.Equals(fullPath, _databasePath + suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private DatabaseBackupResult CreateBackup(string destination, CancellationToken cancellationToken)
    {
        if (!File.Exists(_databasePath))
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.SourceUnavailable, "The database file was not found.");
        }

        var temporaryFile = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}{TemporaryExtension}");
        try
        {
            CopyDatabase(temporaryFile, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCopy(temporaryFile);
            var lastMigrationId = DatabaseBackupValidator.FindLastMigrationId(temporaryFile) ?? string.Empty;
            Publish(temporaryFile, destination);

            return new DatabaseBackupResult(destination, new FileInfo(destination).Length, _timeProvider.GetUtcNow().UtcDateTime, lastMigrationId);
        }
        finally
        {
            DeleteTemporaryFile(temporaryFile);
        }
    }

    private void CopyDatabase(string temporaryFile, CancellationToken cancellationToken)
    {
        // The source is opened read-only, so it can neither be changed nor created by mistake.
        var sourceString = new SqliteConnectionStringBuilder { DataSource = _databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        var destinationString = new SqliteConnectionStringBuilder { DataSource = temporaryFile, Pooling = false }.ToString();

        using var source = new SqliteConnection(sourceString);
        try
        {
            source.Open();
            using var probe = source.CreateCommand();
            probe.CommandText = "SELECT COUNT(*) FROM sqlite_master";
            probe.ExecuteScalar();
        }
        catch (SqliteException exception)
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.SourceUnavailable, "The database could not be read.", exception);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var target = new SqliteConnection(destinationString);
            target.Open();
            source.BackupDatabase(target);

            // A single, self-contained file: no -wal or -shm files that could be left behind or copied separately.
            using var journal = target.CreateCommand();
            journal.CommandText = "PRAGMA journal_mode = DELETE";
            journal.ExecuteNonQuery();
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.WriteFailed, "The backup file could not be written.", exception);
        }
    }

    private static void ValidateCopy(string temporaryFile)
    {
        var problems = DatabaseBackupValidator.FindProblems(temporaryFile);
        if (problems.Count > 0)
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.ValidationFailed, "The backup failed its checks: " + string.Join(" ", problems));
        }
    }

    private static void Publish(string temporaryFile, string destination)
    {
        try
        {
            // Without overwrite: if someone created the file in the meantime, the move fails instead of replacing it.
            File.Move(temporaryFile, destination);
        }
        catch (IOException exception) when (File.Exists(destination))
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.DestinationExists, "A file with this name already exists. It is not overwritten.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.WriteFailed, "The backup file could not be written.", exception);
        }
    }

    private void DeleteTemporaryFile(string temporaryFile)
    {
        // SQLite may have left a journal next to the temporary file as well.
        foreach (var file in new[] { temporaryFile, temporaryFile + "-journal", temporaryFile + "-wal", temporaryFile + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("A temporary backup file could not be deleted ({InnerError}).", exception.GetType().Name);
            }
        }
    }

    private static DatabaseBackupException Invalid(string message)
    {
        return new DatabaseBackupException(DatabaseBackupFailure.InvalidDestination, message);
    }
}
