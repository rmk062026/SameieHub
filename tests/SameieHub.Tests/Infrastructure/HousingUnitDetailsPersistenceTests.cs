using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class HousingUnitDetailsPersistenceTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static HousingUnitDetails FullDetails() => new()
    {
        Address = "Storgata 1A",
        UnitNumber = "H0101",
        SectionNumber = 12,
        HousingType = HousingType.Duplex,
        AreaSquareMeters = 85.5m,
        OwnerName = "Test Eier",
        OwnerPhoneNumber = "+47 400 00 000",
        OwnerEmail = "eier@example.test",
        SecondOwnerName = "Test Medeier",
        SecondOwnerPhoneNumber = "400 00 001",
        SecondOwnerEmail = "medeier@example.test",
        Notes = "Fiktive testdata."
    };

    [Fact]
    public async Task Register_AllDetails_AreReadBackAfterRestart()
    {
        var registered = await _database.Service.RegisterAsync(FullDetails());

        await _database.RestartAsync();

        var found = await _database.Service.GetByIdAsync(registered.Id);
        Assert.NotNull(found);
        Assert.Equal("Storgata 1A", found.Address);
        Assert.Equal("H0101", found.UnitNumber);
        Assert.Equal(12, found.SectionNumber);
        Assert.Equal(HousingType.Duplex, found.HousingType);
        Assert.Equal(85.5m, found.AreaSquareMeters);
        Assert.Equal("Test Eier", found.OwnerName);
        Assert.Equal("+47 400 00 000", found.OwnerPhoneNumber);
        Assert.Equal("eier@example.test", found.OwnerEmail);
        Assert.Equal("Test Medeier", found.SecondOwnerName);
        Assert.Equal("400 00 001", found.SecondOwnerPhoneNumber);
        Assert.Equal("medeier@example.test", found.SecondOwnerEmail);
        Assert.Equal("Fiktive testdata.", found.Notes);
        Assert.True(found.IsActive);
    }

    [Fact]
    public async Task Register_OnlyAddress_StoresNullOptionalValuesAndIsActive()
    {
        var registered = await _database.Service.RegisterAsync("Storgata 1A");

        await _database.RestartAsync();

        var found = await _database.Service.GetByIdAsync(registered.Id);
        Assert.Equal("Storgata 1A", found!.Address);
        Assert.Null(found.UnitNumber);
        Assert.Null(found.SectionNumber);
        Assert.Null(found.HousingType);
        Assert.Null(found.AreaSquareMeters);
        Assert.Null(found.OwnerName);
        Assert.Null(found.SecondOwnerName);
        Assert.True(found.IsActive);
    }

    [Fact]
    public async Task Register_HousingType_IsStoredAsReadableText()
    {
        await _database.Service.RegisterAsync(FullDetails() with { HousingType = HousingType.TerracedHouse });

        var stored = await _database.QueryTextAsync("SELECT HousingType FROM HousingUnits");

        Assert.Equal("TerracedHouse", stored);
    }

    [Fact]
    public async Task UpdateDetails_RemovingSecondOwner_IsStoredAndFirstOwnerIsUnchanged()
    {
        var unit = await _database.Service.RegisterAsync(FullDetails());

        await _database.Service.UpdateDetailsAsync(unit.Id, FullDetails() with
        {
            SecondOwnerName = null,
            SecondOwnerPhoneNumber = null,
            SecondOwnerEmail = null
        });
        await _database.RestartAsync();

        var found = await _database.Service.GetByIdAsync(unit.Id);
        Assert.Null(found!.SecondOwnerName);
        Assert.Null(found.SecondOwnerPhoneNumber);
        Assert.Null(found.SecondOwnerEmail);
        Assert.Equal("Test Eier", found.OwnerName);
        Assert.Equal("eier@example.test", found.OwnerEmail);
    }

    [Fact]
    public async Task UpdateDetails_InvalidValues_KeepStoredValues()
    {
        var unit = await _database.Service.RegisterAsync(FullDetails());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _database.Service.UpdateDetailsAsync(unit.Id, FullDetails() with { Address = "Ny adresse", OwnerEmail = "feil" }));
        await _database.RestartAsync();

        var found = await _database.Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 1A", found!.Address);
        Assert.Equal("eier@example.test", found.OwnerEmail);
    }

    [Fact]
    public async Task DeactivateAndActivate_ArePersisted()
    {
        var unit = await _database.Service.RegisterAsync(FullDetails());

        await _database.Service.DeactivateAsync(unit.Id);
        await _database.RestartAsync();
        var inactive = await _database.Service.GetByIdAsync(unit.Id);

        await _database.Service.ActivateAsync(unit.Id);
        await _database.RestartAsync();
        var active = await _database.Service.GetByIdAsync(unit.Id);

        Assert.False(inactive!.IsActive);
        Assert.Equal("Test Eier", inactive.OwnerName);
        Assert.True(active!.IsActive);
    }

    [Fact]
    public async Task GetAll_ReturnsActiveAndInactiveUnits()
    {
        var first = await _database.Service.RegisterAsync("Storgata 1A");
        await _database.Service.RegisterAsync("Storgata 1B");
        await _database.Service.DeactivateAsync(first.Id);

        var units = await _database.Service.GetAllAsync();

        Assert.Equal(2, units.Count);
        Assert.Single(HousingUnitFilter.Apply(units, null, HousingUnitStatusFilter.Inactive));
    }
}

public class HousingUnitMigrationTests
{
    [Fact]
    public async Task Migrate_FromFirstSchema_KeepsExistingUnitsAndMakesThemActive()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToFirstMigrationAsync();
        await database.ExecuteSqlAsync("INSERT INTO HousingUnits (Address) VALUES ('Storgata 1A'), ('Storgata 1B')");

        await database.MigrateToLatestAsync();

        var units = await database.Service.GetAllAsync();
        Assert.Equal(["Storgata 1A", "Storgata 1B"], units.Select(unit => unit.Address));
        Assert.All(units, unit =>
        {
            Assert.True(unit.IsActive);
            Assert.True(unit.Id > 0);
            Assert.Null(unit.OwnerName);
            Assert.Null(unit.SecondOwnerName);
            Assert.Null(unit.HousingType);
            Assert.Null(unit.AreaSquareMeters);
        });
    }

    [Fact]
    public async Task Migrate_FromFirstSchema_UpgradedUnitsCanBeEdited()
    {
        await using var database = await SqliteTestDatabase.CreateUnmigratedAsync();
        await database.MigrateToFirstMigrationAsync();
        await database.ExecuteSqlAsync("INSERT INTO HousingUnits (Address) VALUES ('Storgata 1A')");
        await database.MigrateToLatestAsync();
        var unit = (await database.Service.GetAllAsync()).Single();

        await database.Service.UpdateDetailsAsync(unit.Id, new HousingUnitDetails { Address = "Storgata 1A", OwnerName = "Test Eier" });

        var found = await database.Service.GetByIdAsync(unit.Id);
        Assert.Equal("Test Eier", found!.OwnerName);
    }
}
