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

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

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
        ConfigureTaskItem(modelBuilder);
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

    private static void ConfigureTaskItem(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();

        task.HasKey(entity => entity.Id);
        task.Property(entity => entity.Title).IsRequired();
        task.Property(entity => entity.Category).HasConversion<string>();
        task.Property(entity => entity.Status).HasConversion<string>();
        task.Property(entity => entity.Priority).HasConversion<string>();
        task.Property(entity => entity.CreatedAt).HasConversion(UtcDateTimeConverter);
        task.Property(entity => entity.UpdatedAt).HasConversion(UtcDateTimeConverter);
        task.Property(entity => entity.CompletedAt).HasConversion(UtcDateTimeConverter);

        // Restrict on both: housing units and board cases that have tasks can never be deleted.
        task.HasOne<HousingUnit>()
            .WithMany()
            .HasForeignKey(entity => entity.HousingUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        task.HasOne<BoardCase>()
            .WithMany()
            .HasForeignKey(entity => entity.BoardCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
