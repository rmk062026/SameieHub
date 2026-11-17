using SameieHub.Domain;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Tests.Infrastructure;

public class EfHousingUnitRepositoryTests : IAsyncLifetime
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

    [Fact]
    public async Task AddAsync_NewUnit_StartsWithoutIdAndGetsPositiveIdFromDatabase()
    {
        var unit = new HousingUnit("Storgata 1A");
        Assert.Equal(0, unit.Id);

        await _database.Repository.AddAsync(unit);

        Assert.True(unit.Id > 0);
    }

    [Fact]
    public async Task AddAsync_TwoUnits_GetDifferentIds()
    {
        var first = new HousingUnit("Storgata 1A");
        var second = new HousingUnit("Storgata 1B");

        await _database.Repository.AddAsync(first);
        await _database.Repository.AddAsync(second);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task GetByIdAsync_StoredUnit_ReturnsUnitWithSameIdAndAddress()
    {
        var unit = new HousingUnit("Storgata 1A");
        await _database.Repository.AddAsync(unit);

        var found = await _database.Repository.GetByIdAsync(unit.Id);

        Assert.NotNull(found);
        Assert.Equal(unit.Id, found.Id);
        Assert.Equal("Storgata 1A", found.Address);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var found = await _database.Repository.GetByIdAsync(999);

        Assert.Null(found);
    }

    [Fact]
    public async Task GetAllAsync_NoUnits_ReturnsEmptyList()
    {
        var units = await _database.Repository.GetAllAsync();

        Assert.Empty(units);
    }

    [Fact]
    public async Task GetAllAsync_TwoStoredUnits_ReturnsBoth()
    {
        await _database.Repository.AddAsync(new HousingUnit("Storgata 1A"));
        await _database.Repository.AddAsync(new HousingUnit("Storgata 1B"));

        var units = await _database.Repository.GetAllAsync();

        Assert.Equal(["Storgata 1A", "Storgata 1B"], units.Select(unit => unit.Address));
    }

    [Fact]
    public async Task UpdateAsync_ChangedAddress_IsStored()
    {
        var unit = new HousingUnit("Storgata 1A");
        await _database.Repository.AddAsync(unit);
        unit.ChangeAddress("Storgata 2A");

        await _database.Repository.UpdateAsync(unit);

        var found = await _database.Repository.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 2A", found!.Address);
    }

    [Fact]
    public async Task GetByIdAsync_AfterRestart_ReturnsDataFromDatabaseFile()
    {
        var unit = new HousingUnit("Storgata 1A");
        await _database.Repository.AddAsync(unit);

        await _database.RestartAsync();

        var found = await _database.Repository.GetByIdAsync(unit.Id);
        Assert.NotNull(found);
        Assert.Equal("Storgata 1A", found.Address);
    }

    [Fact]
    public async Task MigrateAsync_RunTwice_KeepsExistingData()
    {
        await _database.Repository.AddAsync(new HousingUnit("Storgata 1A"));

        await _database.RestartAsync();
        await _database.RestartAsync();

        Assert.Single(await _database.Repository.GetAllAsync());
    }

    [Fact]
    public async Task Service_RegisterAsync_InvalidAddress_StoresNothing()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _database.Service.RegisterAsync("   "));

        Assert.Empty(await _database.Service.GetAllAsync());
    }

    [Fact]
    public async Task Service_ChangeAddressAsync_ValidAddress_IsStoredPermanently()
    {
        var unit = await _database.Service.RegisterAsync("Storgata 1A");

        await _database.Service.ChangeAddressAsync(unit.Id, "Storgata 2A");
        await _database.RestartAsync();

        var found = await _database.Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 2A", found!.Address);
    }

    [Fact]
    public async Task Service_ChangeAddressAsync_InvalidAddress_KeepsStoredAddress()
    {
        var unit = await _database.Service.RegisterAsync("Storgata 1A");

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _database.Service.ChangeAddressAsync(unit.Id, ""));

        var found = await _database.Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 1A", found!.Address);
    }

    [Fact]
    public async Task Service_ChangeAddressAsync_UnknownId_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _database.Service.ChangeAddressAsync(999, "Storgata 2A"));
    }

    [Fact]
    public void DefaultDatabasePath_IsInsideLocalAppData()
    {
        var path = DatabaseLocation.GetDefaultPath();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localAppData, path);
        Assert.EndsWith("sameiehub.db", path);
    }
}
