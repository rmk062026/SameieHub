using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SameieHub.Infrastructure.Persistence;

public static class SqliteConstraintErrors
{
    private const int SqliteConstraintErrorCode = 19;

    // Inserting or updating a row with a reference to a missing row.
    private const int ForeignKeyExtendedCode = 787;

    // Deleting a row that other rows refer to, when the key is ON DELETE RESTRICT.
    // SQLite implements RESTRICT with a trigger, so it reports the trigger code. That code is shared by
    // all triggers, so the message must also name the foreign key.
    private const int ForeignKeyRestrictExtendedCode = 1811;

    // SQLITE_CONSTRAINT_UNIQUE and SQLITE_CONSTRAINT_PRIMARYKEY.
    private const int UniqueExtendedCode = 2067;
    private const int PrimaryKeyExtendedCode = 1555;

    // SQLITE_CONSTRAINT_CHECK.
    private const int CheckExtendedCode = 275;

    /// <summary>
    /// True when a unique index (or primary key) was violated. The message of a unique violation names the
    /// table and columns, for example "UNIQUE constraint failed: EvChargers.HousingUnitId", so
    /// <paramref name="tableAndColumns"/> tells which index it was.
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException exception, string tableAndColumns)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        var isUnique = sqliteException.SqliteErrorCode == SqliteConstraintErrorCode
            && (sqliteException.SqliteExtendedErrorCode == UniqueExtendedCode
                || sqliteException.SqliteExtendedErrorCode == PrimaryKeyExtendedCode);

        return isUnique && sqliteException.Message.Contains(tableAndColumns, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when a CHECK constraint was violated. <paramref name="constraintName"/> is the name of the constraint.</summary>
    public static bool IsCheckViolation(DbUpdateException exception, string constraintName)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        return sqliteException.SqliteErrorCode == SqliteConstraintErrorCode
            && sqliteException.SqliteExtendedErrorCode == CheckExtendedCode
            && sqliteException.Message.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
    }

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

        if (sqliteException.SqliteErrorCode != SqliteConstraintErrorCode)
        {
            return false;
        }

        if (sqliteException.SqliteExtendedErrorCode == ForeignKeyExtendedCode)
        {
            return true;
        }

        return sqliteException.SqliteExtendedErrorCode == ForeignKeyRestrictExtendedCode
            && sqliteException.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
    }
}
