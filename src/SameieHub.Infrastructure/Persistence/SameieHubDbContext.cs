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

    public DbSet<ChargingFacility> ChargingFacilities => Set<ChargingFacility>();

    public DbSet<EvCharger> EvChargers => Set<EvCharger>();

    public DbSet<Meter> Meters => Set<Meter>();

    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();

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
        ConfigureCharging(modelBuilder);
    }

    public const string OneFacilityConstraint = "CK_ChargingFacilities_OneFacility";
    public const string MeterKindConstraint = "CK_Meters_KindFields";
    public const string NonNegativeKwhConstraint = "CK_MeterReadings_NonNegativeKwh";

    /// <summary>
    /// The charging tables. The rules the database enforces itself: one facility, one charger per housing unit,
    /// one main meter per facility, one sub meter per charger, cabinet 1 or 2, no duplicate reading times for a
    /// meter, and restricted foreign keys everywhere (nothing is ever deleted by cascade).
    /// </summary>
    private static void ConfigureCharging(ModelBuilder modelBuilder)
    {
        var facility = modelBuilder.Entity<ChargingFacility>();
        facility.HasKey(entity => entity.Id);
        facility.Property(entity => entity.Name).IsRequired();
        facility.Property(entity => entity.CreatedAt).HasConversion(UtcDateTimeConverter);

        // Autoincrement ids start at 1 and are never reused, so a second facility would get Id 2 and is refused.
        facility.ToTable(table => table.HasCheckConstraint(OneFacilityConstraint, "\"Id\" = 1"));

        var charger = modelBuilder.Entity<EvCharger>();
        charger.HasKey(entity => entity.Id);
        charger.Property(entity => entity.Status).HasConversion<string>();
        charger.Property(entity => entity.CreatedAt).HasConversion(UtcDateTimeConverter);
        charger.Property(entity => entity.UpdatedAt).HasConversion(UtcDateTimeConverter);
        charger.HasOne<ChargingFacility>()
            .WithMany()
            .HasForeignKey(entity => entity.ChargingFacilityId)
            .OnDelete(DeleteBehavior.Restrict);
        charger.HasOne<HousingUnit>()
            .WithMany()
            .HasForeignKey(entity => entity.HousingUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        charger.HasIndex(entity => entity.HousingUnitId).IsUnique();

        var meter = modelBuilder.Entity<Meter>();
        meter.HasKey(entity => entity.Id);
        meter.Property(entity => entity.Name).IsRequired();
        meter.Property(entity => entity.Kind).HasConversion<string>();
        meter.Property(entity => entity.CreatedAt).HasConversion(UtcDateTimeConverter);
        meter.HasOne<ChargingFacility>()
            .WithMany()
            .HasForeignKey(entity => entity.ChargingFacilityId)
            .OnDelete(DeleteBehavior.Restrict);
        meter.HasOne<EvCharger>()
            .WithMany()
            .HasForeignKey(entity => entity.EvChargerId)
            .OnDelete(DeleteBehavior.Restrict);

        // SQLite allows many NULLs in a unique index, so the main meter (no charger) is not affected.
        meter.HasIndex(entity => entity.EvChargerId).IsUnique();
        meter.HasIndex(entity => entity.ChargingFacilityId).IsUnique().HasFilter("\"Kind\" = 'Main'");
        meter.ToTable(table => table.HasCheckConstraint(
            MeterKindConstraint,
            "(\"Kind\" = 'Main' AND \"EvChargerId\" IS NULL AND \"CabinetNumber\" IS NULL) "
            + "OR (\"Kind\" = 'Sub' AND \"EvChargerId\" IS NOT NULL AND \"CabinetNumber\" IS NOT NULL AND \"CabinetNumber\" IN (1, 2))"));

        // Kwh is stored as exact TEXT by the SQLite provider, for example '1234.567', so no decimals are lost.
        var reading = modelBuilder.Entity<MeterReading>();
        reading.HasKey(entity => entity.Id);
        reading.Property(entity => entity.ReadAt).HasConversion(UtcDateTimeConverter);
        reading.Property(entity => entity.RegisteredAt).HasConversion(UtcDateTimeConverter);
        reading.Property(entity => entity.UpdatedAt).HasConversion(UtcDateTimeConverter);
        reading.HasOne<Meter>()
            .WithMany()
            .HasForeignKey(entity => entity.MeterId)
            .OnDelete(DeleteBehavior.Restrict);
        reading.HasIndex(entity => new { entity.MeterId, entity.ReadAt }).IsUnique();
        reading.ToTable(table => table.HasCheckConstraint(NonNegativeKwhConstraint, "CAST(\"Kwh\" AS REAL) >= 0"));
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
