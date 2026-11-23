using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Infrastructure.Backup;

/// <summary>
/// Checks that a file is a complete SameieHub database: SQLite's own integrity and foreign key checks, every expected
/// table, and every migration the program knows. Row counts are not used as a quality measure. The file is only read.
/// </summary>
public static class DatabaseBackupValidator
{
    /// <summary>Returns what is wrong with the file. An empty list means it passed every check.</summary>
    public static IReadOnlyList<string> FindProblems(string databaseFile)
    {
        var problems = new List<string>();
        if (!File.Exists(databaseFile))
        {
            problems.Add("The file does not exist.");

            return problems;
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = databaseFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            CheckIntegrity(connection, problems);
            CheckReferences(connection, problems);
            CheckSchema(connection, problems);
        }
        catch (SqliteException exception)
        {
            problems.Add($"The file could not be read as a SQLite database (error {exception.SqliteErrorCode}).");
        }

        return problems;
    }

    /// <summary>The newest migration recorded in the file, or null if none is recorded.</summary>
    public static string? FindLastMigrationId(string databaseFile)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databaseFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        return ReadStrings(connection, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1").FirstOrDefault();
    }

    private static void CheckIntegrity(SqliteConnection connection, List<string> problems)
    {
        var results = ReadStrings(connection, "PRAGMA integrity_check");
        if (results.Count != 1 || results[0] != "ok")
        {
            problems.Add($"SQLite's integrity check failed ({results.Count} messages).");
        }
    }

    private static void CheckReferences(SqliteConnection connection, List<string> problems)
    {
        var broken = ReadStrings(connection, "PRAGMA foreign_key_check").Count;
        if (broken > 0)
        {
            problems.Add($"{broken} rows have a broken reference (foreign key).");
        }
    }

    private static void CheckSchema(SqliteConnection connection, List<string> problems)
    {
        var options = new DbContextOptionsBuilder<SameieHubDbContext>().UseSqlite("Data Source=:memory:").Options;
        using var model = new SameieHubDbContext(options);

        var tables = ReadStrings(connection, "SELECT name FROM sqlite_master WHERE type = 'table'").ToHashSet();
        foreach (var expected in model.Model.GetEntityTypes().Select(type => type.GetTableName()).OfType<string>())
        {
            if (!tables.Contains(expected))
            {
                problems.Add($"The table {expected} is missing.");
            }
        }

        if (!tables.Contains("__EFMigrationsHistory"))
        {
            problems.Add("The migration history is missing.");

            return;
        }

        var applied = ReadStrings(connection, "SELECT MigrationId FROM __EFMigrationsHistory").ToHashSet();
        foreach (var migration in model.Database.GetMigrations())
        {
            if (!applied.Contains(migration))
            {
                problems.Add($"The migration {migration} has not been applied.");
            }
        }
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
