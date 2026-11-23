using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SameieHub.Domain;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Infrastructure.Restore;

namespace SameieHub.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSameieHubInfrastructure(this IServiceCollection services, string databasePath)
    {
        var folder = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        services.AddDbContextFactory<SameieHubDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Foreign Keys=True"));
        services.AddScoped<IHousingUnitRepository, EfHousingUnitRepository>();
        services.AddScoped<IBoardCaseRepository, EfBoardCaseRepository>();
        services.AddScoped<ITaskItemRepository, EfTaskItemRepository>();
        services.AddScoped<IChargingFacilityRepository, EfChargingFacilityRepository>();
        services.AddScoped<IEvChargerRepository, EfEvChargerRepository>();
        services.AddScoped<IMeterRepository, EfMeterRepository>();
        services.AddScoped<IMeterReadingRepository, EfMeterReadingRepository>();
        services.AddSingleton<IDatabaseBackup>(provider => new SqliteDatabaseBackup(
            databasePath,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SqliteDatabaseBackup>>()));
        services.AddSingleton(provider => new BackupTransfer(
            databasePath,
            provider.GetRequiredService<IDatabaseBackup>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<BackupTransfer>>()));
        services.AddSingleton<IDatabaseRestore>(provider => new SqliteDatabaseRestore(
            databasePath,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<SqliteDatabaseRestore>>()));

        return services;
    }

    /// <summary>
    /// Creates the database or applies pending migrations. Migrations are the only way the schema is created.
    /// </summary>
    public static async Task MigrateSameieHubDatabaseAsync(this IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<SameieHubDbContext>>();

        await using var context = await factory.CreateDbContextAsync();
        await context.Database.MigrateAsync();
    }
}
