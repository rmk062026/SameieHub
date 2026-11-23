using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Domain;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// The backup engine against real temporary SQLite databases. The ordinary AppData database is never used:
/// every test gives the engine the path of its own temporary database.
/// </summary>
public class DatabaseBackupTests : IAsyncLifetime
{
    private const string OwnerName = "Fiktiv Eier";
    private const string OwnerPhone = "+47 000 00 999";
    private const string OwnerEmail = "fiktiv.eier@example.com";

    private SqliteTestDatabase _database = null!;
    private string _folder = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
        _folder = Path.Combine(Path.GetTempPath(), "SameieHubBackupTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_folder);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private SqliteDatabaseBackup NewBackup(string? sourcePath = null, ILoggerProvider? logs = null)
    {
        ILogger<SqliteDatabaseBackup> logger = NullLogger<SqliteDatabaseBackup>.Instance;
        if (logs != null)
        {
            logger = new LoggerFactory([logs]).CreateLogger<SqliteDatabaseBackup>();
        }

        return new SqliteDatabaseBackup(sourcePath ?? _database.DatabasePath, _database.Clock, logger);
    }

    private string Destination(string name = "backup.db") => Path.Combine(_folder, name);

    /// <summary>Fills every module with fictional data, including values that must survive exactly.</summary>
    private async Task FillAllModulesAsync()
    {
        var unit = await _database.Service.RegisterAsync(new HousingUnitDetails
        {
            Address = "Fiktivdalen 10A",
            UnitNumber = "10A",
            SectionNumber = 1,
            HousingType = HousingType.Duplex,
            AreaSquareMeters = 118.5m,
            OwnerName = OwnerName,
            OwnerPhoneNumber = OwnerPhone,
            OwnerEmail = OwnerEmail,
            Notes = "Æøå i merknaden"
        });
        var boardCase = await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak med vedtak", HousingUnitId = unit.Id });
        await _database.BoardCases.RegisterDecisionAsync(boardCase.Id, "Vedtatt av styret", new DateOnly(2026, 11, 10));
        await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave", HousingUnitId = unit.Id, BoardCaseId = boardCase.Id, AssignedTo = "Fiktiv Ansvarlig" });

        var setup = await ChargingSetup.CreateAsync(_database);
        await _database.Charging.RegisterReadingSetAsync(setup.Set(60, 0m, ChargingSetup.Same(0m)));
        await _database.Charging.RegisterReadingSetAsync(setup.Set(30, 0.001m, ChargingSetup.Same(0.001m)));
        await _database.Charging.RegisterReadingSetAsync(setup.Set(1, 12345.678m, ChargingSetup.Same(1234.5m)));
    }

    private static async Task<Dictionary<string, string>> SnapshotAsync(string databaseFile)
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

    private static async Task<string?> ScalarAsync(string databaseFile, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (await command.ExecuteScalarAsync())?.ToString();
    }

    private string[] FilesInFolder() => Directory.GetFiles(_folder).OrderBy(file => file).ToArray();

    // The normal result

    [Fact]
    public async Task CreateBackup_ValidDatabase_GivesAValidatedSingleFileAndNoLeftovers()
    {
        await FillAllModulesAsync();

        var result = await NewBackup().CreateBackupAsync(Destination());

        Assert.Equal(Destination(), result.FilePath);
        Assert.True(result.SizeBytes > 0);
        Assert.Equal(new FileInfo(Destination()).Length, result.SizeBytes);
        Assert.Equal(_database.Clock.GetUtcNow().UtcDateTime, result.CreatedAtUtc);
        Assert.Equal(await ScalarAsync(_database.DatabasePath, "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1"), result.LastMigrationId);
        Assert.Empty(DatabaseBackupValidator.FindProblems(Destination()));
        Assert.Equal([Destination()], FilesInFolder());
    }

    [Fact]
    public async Task CreateBackup_EveryModuleAndEveryValue_IsCopiedExactly()
    {
        await FillAllModulesAsync();

        await NewBackup().CreateBackupAsync(Destination());

        var original = await SnapshotAsync(_database.DatabasePath);
        var backup = await SnapshotAsync(Destination());
        foreach (var table in original.Keys)
        {
            Assert.NotEqual("[]", original[table]);
            Assert.Equal(original[table], backup[table]);
        }

        // The stored text of the readings is identical too, so no value is rounded on the way.
        const string kwhSql = "SELECT group_concat(Kwh, ';') FROM (SELECT Kwh FROM MeterReadings ORDER BY Id)";
        Assert.Equal(await ScalarAsync(_database.DatabasePath, kwhSql), await ScalarAsync(Destination(), kwhSql));
        Assert.Equal("27", await ScalarAsync(Destination(), "SELECT COUNT(*) FROM MeterReadings"));
    }

    [Fact]
    public async Task CreateBackup_WhileTheDatabaseIsInUse_GivesAConsistentSnapshot()
    {
        for (var number = 1; number <= 60; number++)
        {
            await _database.Service.RegisterAsync($"Fiktivdalen {number}");
        }

        var units = await _database.Service.GetAllAsync();
        await _database.Charging.SetUpFacilityAsync("Felles ladeanlegg");
        using var stop = new CancellationTokenSource();
        var firstChargerRegistered = new TaskCompletionSource();

        var writer = Task.Run(async () =>
        {
            foreach (var unit in units)
            {
                stop.Token.ThrowIfCancellationRequested();
                await _database.Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails { ChargerNumber = $"L{unit.Id}" }, 1);
                firstChargerRegistered.TrySetResult();
            }
        });
        await firstChargerRegistered.Task;
        Assert.False(writer.IsCompleted, "The writer must still be running while the backups are made.");

        var backups = new List<string>();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var file = Destination($"live-{attempt}.db");
            await NewBackup().CreateBackupAsync(file);
            backups.Add(file);
        }

        stop.Cancel();
        try
        {
            await writer;
        }
        catch (OperationCanceledException)
        {
        }

        foreach (var file in backups)
        {
            // A charger and its sub meter are stored in one transaction, so a consistent snapshot never has one without the other.
            var chargers = await ScalarAsync(file, "SELECT COUNT(*) FROM EvChargers");
            var subMeters = await ScalarAsync(file, "SELECT COUNT(*) FROM Meters WHERE EvChargerId IS NOT NULL");
            Assert.Equal(chargers, subMeters);
            Assert.Empty(DatabaseBackupValidator.FindProblems(file));
        }
    }

    [Fact]
    public async Task CreateBackup_DoesNotChangeTheOriginal_ThatCanStillBeWrittenTo()
    {
        await FillAllModulesAsync();
        var before = await SnapshotAsync(_database.DatabasePath);

        await NewBackup().CreateBackupAsync(Destination());

        Assert.Equal(before, await SnapshotAsync(_database.DatabasePath));
        await _database.Service.RegisterAsync("Fiktivdalen 99");
        Assert.Equal("2", await ScalarAsync(_database.DatabasePath, "SELECT COUNT(*) FROM HousingUnits WHERE Address IN ('Fiktivdalen 99', 'Fiktivdalen 10A')"));
    }

    // Destination

    [Theory]
    [InlineData("empty")]
    [InlineData("blank")]
    [InlineData("relative")]
    [InlineData("missingFolder")]
    [InlineData("directory")]
    [InlineData("invalidName")]
    [InlineData("sameAsSource")]
    public async Task CreateBackup_InvalidDestination_IsRefusedAndNothingIsCreated(string kind)
    {
        var destination = kind switch
        {
            "empty" => string.Empty,
            "blank" => "   ",
            "relative" => "backup.db",
            "missingFolder" => Path.Combine(_folder, "finnes-ikke", "backup.db"),
            "directory" => _folder,
            "invalidName" => Path.Combine(_folder, "ugyldig|navn.db"),
            _ => _database.DatabasePath
        };
        var filesBefore = FilesInFolder();

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(() => NewBackup().CreateBackupAsync(destination));

        Assert.Equal(DatabaseBackupFailure.InvalidDestination, exception.Reason);
        Assert.Equal(filesBefore, FilesInFolder());
        Assert.False(Directory.Exists(Path.Combine(_folder, "finnes-ikke")));
        Assert.Empty(DatabaseBackupValidator.FindProblems(_database.DatabasePath));
    }

    [Fact]
    public async Task CreateBackup_ExistingFile_IsNeverOverwritten()
    {
        await FillAllModulesAsync();
        await File.WriteAllTextAsync(Destination(), "dette er en eldre sikkerhetskopi");

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(() => NewBackup().CreateBackupAsync(Destination()));

        Assert.Equal(DatabaseBackupFailure.DestinationExists, exception.Reason);
        Assert.Equal("dette er en eldre sikkerhetskopi", await File.ReadAllTextAsync(Destination()));
        Assert.Equal([Destination()], FilesInFolder());
    }

    [Fact]
    public async Task CreateBackup_CancelledBeforeItStarts_CreatesNothing()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewBackup().CreateBackupAsync(Destination(), cancelled.Token));

        Assert.Empty(FilesInFolder());
    }

    // Failures never look like a successful backup

    [Fact]
    public async Task CreateBackup_SourceDoesNotExist_FailsWithoutCreatingAnyFileOrDatabase()
    {
        var missingSource = Path.Combine(_folder, "kilde", "finnes-ikke.db");
        Directory.CreateDirectory(Path.GetDirectoryName(missingSource)!);

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(() => NewBackup(missingSource).CreateBackupAsync(Destination()));

        Assert.Equal(DatabaseBackupFailure.SourceUnavailable, exception.Reason);
        Assert.False(File.Exists(Destination()));
        Assert.False(File.Exists(missingSource));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Destination())!, "*.partial"));
    }

    [Fact]
    public async Task CreateBackup_SourceIsNotADatabase_FailsWithoutAnyFile()
    {
        var notDatabase = Path.Combine(_folder, "kilde.db");
        await File.WriteAllTextAsync(notDatabase, "dette er ikke en SQLite-database, bare tekst som er lang nok til å se ut som en fil");

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(() => NewBackup(notDatabase).CreateBackupAsync(Destination()));

        Assert.Equal(DatabaseBackupFailure.SourceUnavailable, exception.Reason);
        Assert.Equal([notDatabase], FilesInFolder());
    }

    [Fact]
    public async Task CreateBackup_SourceWithBrokenReferences_IsReportedAsFailedAndLeavesNoFile()
    {
        await FillAllModulesAsync();
        await _database.ExecuteSqlAsync("PRAGMA foreign_keys = OFF; UPDATE BoardCases SET HousingUnitId = 9999");

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(() => NewBackup().CreateBackupAsync(Destination()));

        Assert.Equal(DatabaseBackupFailure.ValidationFailed, exception.Reason);
        Assert.Empty(FilesInFolder());
    }

    [Fact]
    public async Task CreateBackup_SourceWithoutTheExpectedSchema_IsReportedAsFailedAndLeavesNoFile()
    {
        await _database.DisposeAsync();
        _database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await _database.ExecuteSqlAsync("CREATE TABLE Annet (Id INTEGER PRIMARY KEY)");

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(() => NewBackup().CreateBackupAsync(Destination()));

        Assert.Equal(DatabaseBackupFailure.ValidationFailed, exception.Reason);
        Assert.Empty(FilesInFolder());
    }

    // Personal data

    [Fact]
    public async Task CreateBackup_LogsNeitherPersonalDataNorTheFolder_WhetherItSucceedsOrFails()
    {
        await FillAllModulesAsync();
        var logs = new CapturingLoggerProvider();

        await NewBackup(logs: logs).CreateBackupAsync(Destination());
        await Assert.ThrowsAsync<DatabaseBackupException>(() => NewBackup(logs: logs).CreateBackupAsync(Destination()));

        Assert.NotEmpty(logs.Messages);
        var everything = string.Join("\n", logs.Messages);
        foreach (var secret in new[] { OwnerName, OwnerPhone, OwnerEmail, "Fiktiv Ansvarlig", _folder, Path.GetTempPath() })
        {
            Assert.DoesNotContain(secret, everything);
        }
    }

    [Fact]
    public async Task CreateBackup_NeverTouchesTheOrdinaryAppDataDatabase()
    {
        var ordinary = DatabaseLocation.GetDefaultPath();
        var before = new FileInfo(ordinary);
        var existedBefore = before.Exists;
        var lengthBefore = existedBefore ? before.Length : 0;
        var writtenBefore = existedBefore ? before.LastWriteTimeUtc : default;

        await FillAllModulesAsync();
        await NewBackup().CreateBackupAsync(Destination());

        var after = new FileInfo(ordinary);
        Assert.Equal(existedBefore, after.Exists);
        if (existedBefore)
        {
            Assert.Equal(lengthBefore, after.Length);
            Assert.Equal(writtenBefore, after.LastWriteTimeUtc);
        }

        Assert.NotEqual(Path.GetFullPath(ordinary), Path.GetFullPath(_database.DatabasePath));
        Assert.NotEqual(Path.GetFullPath(ordinary), Path.GetFullPath(Destination()));
    }
}
