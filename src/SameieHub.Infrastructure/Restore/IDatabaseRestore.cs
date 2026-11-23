namespace SameieHub.Infrastructure.Restore;

/// <summary>
/// Restores the database from a backup in two steps. <see cref="PrepareAsync"/> can run while the program is running and
/// changes nothing that is in use. <see cref="ApplyPendingRestore"/> makes the change when the program starts, before any
/// connection to the database is open. The person has to close the program and start it again for the restore to happen.
/// </summary>
public interface IDatabaseRestore
{
    /// <summary>
    /// Checks the chosen backup and prepares a restore from it, upgrading an older backup on a working copy. The backup file and
    /// the live database are never changed. A restore that was prepared earlier is replaced.
    /// </summary>
    /// <exception cref="DatabaseRestoreException">The backup cannot be used. Nothing was changed.</exception>
    Task<RestorePreparation> PrepareAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carries out a prepared restore, or finishes or undoes one that was interrupted. Must be called before the database is
    /// opened. Returns <see cref="RestoreOutcomeKind.NothingPending"/> when nothing was prepared.
    /// </summary>
    RestoreOutcome ApplyPendingRestore();

    /// <summary>The restore that waits for the next start, or null.</summary>
    PendingRestore? GetPending();

    /// <summary>Deletes a prepared restore that has not been carried out.</summary>
    void CancelPending();

    /// <summary>What happened the last time a restore was carried out or undone, or null if it never happened.</summary>
    RestoreOutcome? GetLastOutcome();
}

/// <summary>What the chosen file turned out to be.</summary>
public enum BackupFileStatus
{
    /// <summary>Not a SQLite database, damaged, or with broken references.</summary>
    Invalid,

    /// <summary>A valid SQLite database that is not a SameieHub database.</summary>
    NotSameieHub,

    /// <summary>A SameieHub database from a newer program version than this one.</summary>
    Newer,

    /// <summary>A SameieHub database with an older schema that can be upgraded.</summary>
    Older,

    /// <summary>A SameieHub database with the schema of this program version.</summary>
    Current
}

/// <param name="Status">The kind of file. Only <see cref="BackupFileStatus.Older"/> and <see cref="BackupFileStatus.Current"/> can be restored.</param>
/// <param name="WasUpgraded">True if the working copy was upgraded to the current schema.</param>
/// <param name="BackupFileName">The file name (not the folder) of the chosen backup.</param>
/// <param name="PreparedAtUtc">When the restore was prepared.</param>
/// <param name="LastMigrationId">The newest migration in the prepared database.</param>
public record RestorePreparation(BackupFileStatus Status, bool WasUpgraded, string BackupFileName, DateTime PreparedAtUtc, string LastMigrationId);

public record PendingRestore(string BackupFileName, DateTime PreparedAtUtc, bool WasUpgraded);

public enum RestoreOutcomeKind
{
    /// <summary>No restore was waiting.</summary>
    NothingPending,

    /// <summary>The database was replaced.</summary>
    Applied,

    /// <summary>An interrupted restore was finished at startup.</summary>
    AppliedAfterRecovery,

    /// <summary>The restore could not be completed (or was interrupted early) and the original database was put back.</summary>
    RolledBack,

    /// <summary>The prepared file did not pass the checks at startup, so it was not used. The database is unchanged.</summary>
    Rejected,

    /// <summary>Something unexpected happened. Nothing more was changed, and the files are left for inspection.</summary>
    Failed
}

/// <param name="PreviousDatabaseFolderName">The folder (inside the restore folder) that holds the database as it was before the restore.</param>
public record RestoreOutcome(RestoreOutcomeKind Kind, DateTime AtUtc, string? PreviousDatabaseFolderName);

public enum DatabaseRestoreFailure
{
    /// <summary>Missing, not a database, damaged, or failed its checks.</summary>
    InvalidBackup,

    /// <summary>A valid SQLite database that is not a SameieHub database.</summary>
    NotSameieHubDatabase,

    /// <summary>Made by a newer version of the program. Use that version, or an older backup.</summary>
    NewerVersion,

    /// <summary>The older backup could not be upgraded.</summary>
    UpgradeFailed,

    /// <summary>The demo database is never used as the target or the source of a restore.</summary>
    DemoDatabaseNotAllowed,

    /// <summary>The chosen file is the database the program is using.</summary>
    SourceIsTheLiveDatabase,

    /// <summary>Files could not be written.</summary>
    WriteFailed
}

/// <summary>A restore could not be prepared. The message never contains a path or any data from a database.</summary>
public class DatabaseRestoreException : Exception
{
    public DatabaseRestoreException(DatabaseRestoreFailure reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }

    public DatabaseRestoreFailure Reason { get; }
}
