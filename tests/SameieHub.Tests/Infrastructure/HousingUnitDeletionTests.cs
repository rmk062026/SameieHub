using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class HousingUnitDeletionTests : IAsyncLifetime
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
    public async Task Delete_ExistingUnit_IsGoneAfterRestart()
    {
        var kept = await _database.Service.RegisterAsync("Storgata 1A");
        var deleted = await _database.Service.RegisterAsync("Storgata 1B");

        await _database.Service.DeleteAsync(deleted.Id);
        await _database.RestartAsync();

        Assert.Null(await _database.Service.GetByIdAsync(deleted.Id));
        var remaining = Assert.Single(await _database.Service.GetAllAsync());
        Assert.Equal(kept.Id, remaining.Id);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsFalseFromRepositoryAndThrowsFromService()
    {
        Assert.False(await _database.Repository.DeleteAsync(999));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _database.Service.DeleteAsync(999));
    }

    [Fact]
    public async Task Delete_UnitWithOwnersAndContactInformation_RemovesAllOfIt()
    {
        var unit = await _database.Service.RegisterAsync(new HousingUnitDetails
        {
            Address = "Storgata 1A",
            OwnerName = "Test Eier",
            OwnerPhoneNumber = "400 00 000",
            OwnerEmail = "eier@example.test",
            SecondOwnerName = "Test Medeier",
            SecondOwnerEmail = "medeier@example.test",
            Notes = "Fiktive testdata."
        });

        await _database.Service.DeleteAsync(unit.Id);

        Assert.Equal("0", await _database.QueryTextAsync("SELECT COUNT(*) FROM HousingUnits"));
        Assert.Equal("0", await _database.QueryTextAsync(
            "SELECT COUNT(*) FROM HousingUnits WHERE OwnerName IS NOT NULL OR OwnerEmail IS NOT NULL OR SecondOwnerName IS NOT NULL"));
    }

    [Fact]
    public async Task Delete_NewestUnit_DoesNotMakeItsIdAvailableAgain()
    {
        var deleted = await _database.Service.RegisterAsync("Storgata 1A");
        await _database.Service.DeleteAsync(deleted.Id);

        var next = await _database.Service.RegisterAsync("Storgata 1B");

        Assert.NotEqual(deleted.Id, next.Id);
        Assert.True(next.Id > deleted.Id);
    }

    [Fact]
    public async Task Delete_ActiveAndInactiveUnits_BothCanBeDeleted()
    {
        var active = await _database.Service.RegisterAsync("Storgata 1A");
        var inactive = await _database.Service.RegisterAsync("Storgata 1B");
        await _database.Service.DeactivateAsync(inactive.Id);

        await _database.Service.DeleteAsync(active.Id);
        await _database.Service.DeleteAsync(inactive.Id);

        Assert.Empty(await _database.Service.GetAllAsync());
    }
}
