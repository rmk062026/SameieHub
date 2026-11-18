using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SameieHub.Domain;

namespace SameieHub.Infrastructure.Persistence;

public class SameieHubDbContext : DbContext
{
    public SameieHubDbContext(DbContextOptions<SameieHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<HousingUnit> HousingUnits => Set<HousingUnit>();

    public DbSet<BoardCase> BoardCases => Set<BoardCase>();

    // SQLite stores DateTime as text without a time zone and EF Core reads it back as Unspecified.
    // Everything we store is UTC, so it is marked as UTC again when it is read.
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var housingUnit = modelBuilder.Entity<HousingUnit>();

        housingUnit.HasKey(unit => unit.Id);
        housingUnit.Property(unit => unit.Address).IsRequired();
        housingUnit.Property(unit => unit.HousingType).HasConversion<string>();

        ConfigureBoardCase(modelBuilder);
    }

    private static void ConfigureBoardCase(ModelBuilder modelBuilder)
    {
        var boardCase = modelBuilder.Entity<BoardCase>();

        boardCase.HasKey(entity => entity.Id);
        boardCase.Property(entity => entity.Title).IsRequired();
        boardCase.Property(entity => entity.Status).HasConversion<string>();
        boardCase.Property(entity => entity.Priority).HasConversion<string>();
        boardCase.Property(entity => entity.CreatedAt).HasConversion(UtcDateTimeConverter);
        boardCase.Property(entity => entity.UpdatedAt).HasConversion(UtcDateTimeConverter);

        // Restrict: a housing unit with board cases can never be deleted, and nothing is nulled or cascaded.
        boardCase.HasOne<HousingUnit>()
            .WithMany()
            .HasForeignKey(entity => entity.HousingUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
