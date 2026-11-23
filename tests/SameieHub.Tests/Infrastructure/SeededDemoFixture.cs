using SameieHub.Domain;
using SameieHub.Infrastructure.Demo;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// One fully seeded demo database that read-only tests share, so the demo is created once per test class
/// instead of once per test. It only offers reads. A test that changes data, or that tests creation, reset,
/// migration or failures, must use its own <see cref="SqliteTestDatabase"/> instead.
/// </summary>
public sealed class SeededDemoFixture : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private string _fingerprintAfterSeeding = string.Empty;

    /// <summary>The fixed time the demo was created at (the test clock never moves).</summary>
    public DateTime Now => _database.Clock.GetUtcNow().UtcDateTime;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();

        var seeder = new DemoDatabaseSeeder(
            _database.Repository,
            _database.BoardCaseRepository,
            _database.TaskRepository,
            _database.FacilityRepository,
            _database.ChargerRepository,
            _database.MeterRepository,
            _database.ReadingRepository,
            _database.Clock);
        await seeder.SeedAsync();

        _fingerprintAfterSeeding = await FingerprintAsync();
    }

    /// <summary>Fails the test class if anything changed the shared database, so a mistake is not silent.</summary>
    public async Task DisposeAsync()
    {
        try
        {
            var fingerprintNow = await FingerprintAsync();
            Assert.Equal(_fingerprintAfterSeeding, fingerprintNow);
        }
        finally
        {
            await _database.DisposeAsync();
        }
    }

    public Task<IReadOnlyList<HousingUnit>> UnitsAsync() => _database.Service.GetAllAsync();

    public Task<IReadOnlyList<BoardCase>> BoardCasesAsync() => _database.BoardCases.GetAllAsync();

    public Task<IReadOnlyList<TaskItem>> TasksAsync() => _database.Tasks.GetAllAsync();

    public Task<ChargingFacility?> FacilityAsync() => _database.Charging.GetFacilityAsync();

    public Task<IReadOnlyList<EvCharger>> ChargersAsync() => _database.Charging.GetChargersAsync();

    public Task<IReadOnlyList<Meter>> MetersAsync() => _database.Charging.GetMetersAsync();

    public Task<Meter?> MainMeterAsync() => _database.Charging.GetMainMeterAsync();

    public Task<Meter?> SubMeterAsync(int chargerId) => _database.Charging.GetSubMeterAsync(chargerId);

    public Task<IReadOnlyList<MeterReading>> AllReadingsAsync() => _database.Charging.GetAllReadingsAsync();

    public Task<IReadOnlyList<MeterReading>> ReadingsAsync(int meterId) => _database.Charging.GetReadingsAsync(meterId);

    public Task<ChargingReconciliation> ReconcileAsync(DateTime from, DateTime to) => _database.Charging.ReconcileAsync(from, to);

    public Task<ChargingReconciliation?> ReconcileLatestPeriodAsync() => _database.Charging.ReconcileLatestPeriodAsync();

    private async Task<string> FingerprintAsync()
    {
        var parts = new List<string?>
        {
            await _database.QueryTextAsync("SELECT COUNT(*) FROM HousingUnits"),
            await _database.QueryTextAsync("SELECT COUNT(*) FROM BoardCases"),
            await _database.QueryTextAsync("SELECT COUNT(*) FROM TaskItems"),
            await _database.QueryTextAsync("SELECT COUNT(*) FROM ChargingFacilities"),
            await _database.QueryTextAsync("SELECT COUNT(*) FROM EvChargers"),
            await _database.QueryTextAsync("SELECT COUNT(*) FROM Meters"),
            await _database.QueryTextAsync("SELECT COUNT(*) FROM MeterReadings"),
            await _database.QueryTextAsync("SELECT MAX(UpdatedAt) FROM BoardCases"),
            await _database.QueryTextAsync("SELECT MAX(UpdatedAt) FROM TaskItems"),
            await _database.QueryTextAsync("SELECT MAX(UpdatedAt) FROM EvChargers"),
            await _database.QueryTextAsync("SELECT MAX(UpdatedAt) FROM MeterReadings"),
            await _database.QueryTextAsync("SELECT group_concat(Kwh, ',') FROM (SELECT Kwh FROM MeterReadings ORDER BY Id)")
        };

        return string.Join("|", parts);
    }
}
