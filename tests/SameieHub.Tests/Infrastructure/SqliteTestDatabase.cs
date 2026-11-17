using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Infrastructure;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// A real SQLite database in a temporary folder, created through the same migrations as the application.
/// </summary>
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubTests", Guid.NewGuid().ToString());
    private ServiceProvider _provider;
    private IServiceScope _scope;

    private SqliteTestDatabase()
    {
        DatabasePath = Path.Combine(_folder, "test.db");
        _provider = BuildProvider();
        _scope = _provider.CreateScope();
    }

    public string DatabasePath { get; }

    public HousingUnitService Service => _scope.ServiceProvider.GetRequiredService<HousingUnitService>();

    public IHousingUnitRepository Repository => _scope.ServiceProvider.GetRequiredService<IHousingUnitRepository>();

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var database = new SqliteTestDatabase();
        await database._provider.MigrateSameieHubDatabaseAsync();

        return database;
    }

    /// <summary>
    /// Replaces all services, like restarting the application against the same database file.
    /// </summary>
    public async Task RestartAsync()
    {
        await StopAsync();
        _provider = BuildProvider();
        _scope = _provider.CreateScope();
        await _provider.MigrateSameieHubDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private async Task StopAsync()
    {
        _scope.Dispose();
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSameieHubInfrastructure(DatabasePath);
        services.AddScoped<HousingUnitService>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
