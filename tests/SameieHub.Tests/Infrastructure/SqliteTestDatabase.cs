using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Persistence;

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
    /// Creates the database file without applying any migrations, for testing upgrades from older schemas.
    /// </summary>
    public static Task<SqliteTestDatabase> CreateUnmigratedAsync()
    {
        return Task.FromResult(new SqliteTestDatabase());
    }

    public async Task MigrateToFirstMigrationAsync()
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<SameieHubDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        var firstMigration = context.Database.GetMigrations().First();
        await context.GetService<IMigrator>().MigrateAsync(firstMigration);
    }

    public Task MigrateToLatestAsync()
    {
        return _provider.MigrateSameieHubDatabaseAsync();
    }

    public async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string?> QueryTextAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        return (await command.ExecuteScalarAsync())?.ToString();
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
