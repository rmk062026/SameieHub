using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Tests.Infrastructure;

/// <summary>Reads database files in tests without changing them. Shared by the backup and restore tests.</summary>
internal static class DatabaseTestFiles
{
    /// <summary>Every row of every table as JSON, so two databases can be compared value by value.</summary>
    public static async Task<Dictionary<string, string>> SnapshotAsync(string databaseFile)
    {
        var options = new DbContextOptionsBuilder<SameieHubDbContext>()
            .UseSqlite($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False")
            .Options;
        await using var context = new SameieHubDbContext(options);

        return new Dictionary<string, string>
        {
            ["HousingUnits"] = JsonSerializer.Serialize(await context.HousingUnits.AsNoTracking().OrderBy(row => row.Id).ToListAsync()),
            ["BoardCases"] = JsonSerializer.Serialize(await context.BoardCases.AsNoTracking().OrderBy(row => row.Id).ToListAsync()),
            ["TaskItems"] = JsonSerializer.Serialize(await context.TaskItems.AsNoTracking().OrderBy(row => row.Id).ToListAsync()),
            ["ChargingFacilities"] = JsonSerializer.Serialize(await context.ChargingFacilities.AsNoTracking().OrderBy(row => row.Id).ToListAsync()),
            ["EvChargers"] = JsonSerializer.Serialize(await context.EvChargers.AsNoTracking().OrderBy(row => row.Id).ToListAsync()),
            ["Meters"] = JsonSerializer.Serialize(await context.Meters.AsNoTracking().OrderBy(row => row.Id).ToListAsync()),
            ["MeterReadings"] = JsonSerializer.Serialize(await context.MeterReadings.AsNoTracking().OrderBy(row => row.Id).ToListAsync())
        };
    }

    public static async Task<string?> ScalarAsync(string databaseFile, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (await command.ExecuteScalarAsync())?.ToString();
    }

    public static async Task ExecuteAsync(string databaseFile, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={databaseFile};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>A consistent single-file copy made with SQLite's backup API (also works for an older schema).</summary>
    public static async Task CopyAsync(string sourceFile, string destinationFile)
    {
        await using var source = new SqliteConnection($"Data Source={sourceFile};Mode=ReadOnly;Pooling=False");
        await using var destination = new SqliteConnection($"Data Source={destinationFile};Pooling=False");
        await source.OpenAsync();
        await destination.OpenAsync();
        source.BackupDatabase(destination);
        await using var command = destination.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = DELETE";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<string> HashAsync(string file)
    {
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}
