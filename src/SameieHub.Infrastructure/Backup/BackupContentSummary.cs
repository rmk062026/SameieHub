using Microsoft.Data.Sqlite;

namespace SameieHub.Infrastructure.Backup;

/// <summary>
/// How many of each kind of thing a backup holds, so a person can see what a backup contains before it is used. Only counts, never
/// names or other data. A table an older backup does not have yet gives null.
/// </summary>
public record BackupContentSummary(int? HousingUnits, int? BoardCases, int? Tasks, int? Chargers, int? MeterReadings)
{
    /// <summary>Reads the counts from a database file that has already been inspected. The file is only read.</summary>
    public static BackupContentSummary Read(string databaseFile)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = databaseFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        return new BackupContentSummary(
            Count(connection, "HousingUnits"),
            Count(connection, "BoardCases"),
            Count(connection, "TaskItems"),
            Count(connection, "EvChargers"),
            Count(connection, "MeterReadings"));
    }

    private static int? Count(SqliteConnection connection, string table)
    {
        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        exists.Parameters.AddWithValue("$name", table);
        if (Convert.ToInt32(exists.ExecuteScalar()) == 0)
        {
            return null;
        }

        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";

        return Convert.ToInt32(count.ExecuteScalar());
    }
}
