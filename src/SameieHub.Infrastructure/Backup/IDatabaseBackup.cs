namespace SameieHub.Infrastructure.Backup;

/// <summary>Creates a consistent backup of the SameieHub database. Restoring is a separate step that is not part of this contract.</summary>
public interface IDatabaseBackup
{
    /// <summary>
    /// Writes a complete, validated copy of the database to <paramref name="destinationPath"/>. The database can be in use
    /// while this runs. An existing file is never overwritten, and the file only appears under its final name when it has
    /// passed every check.
    /// </summary>
    /// <param name="destinationPath">An absolute path to a file in an existing folder. The caller chooses it; nothing is chosen automatically.</param>
    /// <exception cref="DatabaseBackupException">The backup was not made. <see cref="DatabaseBackupException.Reason"/> says why, and no file is left behind.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled before a file appeared.</exception>
    Task<DatabaseBackupResult> CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);
}

/// <param name="FilePath">The full path of the finished backup file.</param>
/// <param name="SizeBytes">The size of the file.</param>
/// <param name="CreatedAtUtc">When the backup was made (UTC).</param>
/// <param name="LastMigrationId">The newest migration in the backup, which tells which version of the schema it has.</param>
public record DatabaseBackupResult(string FilePath, long SizeBytes, DateTime CreatedAtUtc, string LastMigrationId);

/// <summary>Why a backup was not made. The screen that asks for the backup chooses the wording for each reason.</summary>
public enum DatabaseBackupFailure
{
    /// <summary>The path is empty, relative, in a missing folder, a folder, has an invalid file name, or is the database itself.</summary>
    InvalidDestination,

    /// <summary>A file with this name already exists. It is never overwritten.</summary>
    DestinationExists,

    /// <summary>The database could not be opened or read as a SQLite database.</summary>
    SourceUnavailable,

    /// <summary>The copy was made but failed a check, so it was deleted.</summary>
    ValidationFailed,

    /// <summary>The file could not be written, for example because of missing permission or a full disk.</summary>
    WriteFailed
}

/// <summary>A backup was not made. The message never contains a folder name or any data from the database.</summary>
public class DatabaseBackupException : Exception
{
    public DatabaseBackupException(DatabaseBackupFailure reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }

    public DatabaseBackupFailure Reason { get; }
}
