using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Infrastructure.Persistence;

public class SameieHubDbContext : DbContext
{
    public SameieHubDbContext(DbContextOptions<SameieHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<HousingUnit> HousingUnits => Set<HousingUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var housingUnit = modelBuilder.Entity<HousingUnit>();

        housingUnit.HasKey(unit => unit.Id);
        housingUnit.Property(unit => unit.Address).IsRequired();
    }
}
