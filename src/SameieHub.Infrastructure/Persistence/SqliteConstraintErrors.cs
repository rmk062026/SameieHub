using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SameieHub.Infrastructure.Persistence;

public static class SqliteConstraintErrors
{
    private const int SqliteConstraintErrorCode = 19;

    // Inserting or updating a row with a reference to a missing row.
    private const int ForeignKeyExtendedCode = 787;

    // Deleting a row that other rows refer to, when the key is ON DELETE RESTRICT.
    // SQLite implements RESTRICT with a trigger, so it reports the trigger code. The database has no
    // other triggers, so this code only means a restricted foreign key.
    private const int ForeignKeyRestrictExtendedCode = 1811;

    /// <summary>
    /// Tells a foreign key violation from other constraint errors (unique, not null and so on) by SQLite's
    /// extended error code, which is more reliable than the text of the error message.
    /// </summary>
    public static bool IsForeignKeyViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        return sqliteException.SqliteErrorCode == SqliteConstraintErrorCode
            && (sqliteException.SqliteExtendedErrorCode == ForeignKeyExtendedCode
                || sqliteException.SqliteExtendedErrorCode == ForeignKeyRestrictExtendedCode);
    }
}
