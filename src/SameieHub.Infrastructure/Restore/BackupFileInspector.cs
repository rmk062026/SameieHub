using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Infrastructure.Restore;

public record BackupInspection(BackupFileStatus Status, IReadOnlyList<string> Problems);

/// <summary>
/// Tells what kind of file a backup is: damaged, another program's database, from a newer version, older, or current. The
/// migration history decides the version, so no separate version number is needed. The file is only read.
/// </summary>
public static class BackupFileInspector
{
    public static BackupInspection Inspect(string file)
    {
        if (!File.Exists(file))
        {
            return Result(BackupFileStatus.Invalid, "The file does not exist.");
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = file, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            var integrity = ReadStrings(connection, "PRAGMA integrity_check");
            if (integrity.Count != 1 || integrity[0] != "ok")
            {
                return Result(BackupFileStatus.Invalid, "SQLite's integrity check failed.");
            }

            var brokenReferences = ReadStrings(connection, "PRAGMA foreign_key_check").Count;
            if (brokenReferences > 0)
            {
                return Result(BackupFileStatus.Invalid, $"{brokenReferences} rows have a broken reference (foreign key).");
            }

            return Classify(connection);
        }
        catch (SqliteException exception)
        {
            return Result(BackupFileStatus.Invalid, $"The file could not be read as a SQLite database (error {exception.SqliteErrorCode}).");
        }
    }

    private static BackupInspection Classify(SqliteConnection connection)
    {
        var tables = ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'table'").ToHashSet();
        if (!tables.Contains("__EFMigrationsHistory"))
        {
            return Result(BackupFileStatus.NotSameieHub, "The file has no migration history, so it is not a SameieHub database.");
        }

        var options = new DbContextOptionsBuilder<SameieHubDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var model = new SameieHubDbContext(options);
        var known = model.Database.GetMigrations().OrderBy(id => id, StringComparer.Ordinal).ToList();
        var applied = ReadStrings(connection, "SELECT MigrationId FROM __EFMigrationsHistory").OrderBy(id => id, StringComparer.Ordinal).ToList();

        if (applied.Count == 0 || applied[0] != known[0])
        {
            return Result(BackupFileStatus.NotSameieHub, "The migration history is not SameieHub's.");
        }

        if (applied.Except(known).Any())
        {
            return Result(BackupFileStatus.Newer, "The file was made by a newer version of the program.");
        }

        if (!applied.SequenceEqual(known.Take(applied.Count)))
        {
            return Result(BackupFileStatus.Invalid, "The migration history has gaps.");
        }

        return applied.Count == known.Count
            ? new BackupInspection(BackupFileStatus.Current, [])
            : new BackupInspection(BackupFileStatus.Older, []);
    }

    private static BackupInspection Result(BackupFileStatus status, string problem)
    {
        return new BackupInspection(status, [problem]);
    }

    private static List<string> ReadStrings(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read())
        {
            values.Add(reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty);
        }

        return values;
    }
}
