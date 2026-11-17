using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SameieHub.Infrastructure.Persistence;

/// <summary>
/// Used only by the "dotnet ef" tools when creating migrations. Never opens a real database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SameieHubDbContext>
{
    public SameieHubDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SameieHubDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new SameieHubDbContext(options);
    }
}
