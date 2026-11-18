using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Infrastructure.Persistence;

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
