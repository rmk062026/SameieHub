using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Tests.Fakes;

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

    /// <summary>The clock used by the board case service. It only moves when the test moves it.</summary>
    public ManualTimeProvider Clock { get; } = new(new DateTime(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc));

    public HousingUnitService Service => _scope.ServiceProvider.GetRequiredService<HousingUnitService>();

    public IHousingUnitRepository Repository => _scope.ServiceProvider.GetRequiredService<IHousingUnitRepository>();

    public BoardCaseService BoardCases => _scope.ServiceProvider.GetRequiredService<BoardCaseService>();

    public IBoardCaseRepository BoardCaseRepository => _scope.ServiceProvider.GetRequiredService<IBoardCaseRepository>();

    public TaskItemService Tasks => _scope.ServiceProvider.GetRequiredService<TaskItemService>();

    public ITaskItemRepository TaskRepository => _scope.ServiceProvider.GetRequiredService<ITaskItemRepository>();

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
        await MigrateToAsync(migrations => migrations.First());
    }

    /// <summary>
    /// Applies migrations up to and including the one with the given name, for example "AddHousingUnitDetails".
    /// </summary>
    public async Task MigrateToMigrationAsync(string migrationName)
    {
        await MigrateToAsync(migrations => migrations.Single(id => id.EndsWith("_" + migrationName)));
    }

    private async Task MigrateToAsync(Func<IEnumerable<string>, string> chooseMigration)
    {
        var factory = _provider.GetRequiredService<IDbContextFactory<SameieHubDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        var targetMigration = chooseMigration(context.Database.GetMigrations());
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
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
        services.AddScoped<BoardCaseService>();
        services.AddScoped<TaskItemService>();
        services.AddSingleton<TimeProvider>(Clock);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
