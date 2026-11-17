using SameieHub.Domain;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Domain;

public class HousingUnitServiceTests
{
    private readonly HousingUnitService _service = new(new FakeHousingUnitRepository());

    [Fact]
    public async Task RegisterAsync_ValidAddress_ReturnsUnitWithAssignedId()
    {
        var unit = await _service.RegisterAsync("Storgata 1A");

        Assert.True(unit.Id > 0);
        Assert.Equal("Storgata 1A", unit.Address);
    }

    [Fact]
    public async Task RegisterAsync_TwoUnits_AssignsDifferentIds()
    {
        var first = await _service.RegisterAsync("Storgata 1A");
        var second = await _service.RegisterAsync("Storgata 1B");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_InvalidAddress_ThrowsAndStoresNothing(string? address)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.RegisterAsync(address!));

        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUnit_ReturnsUnitWithSameId()
    {
        var registered = await _service.RegisterAsync("Storgata 1A");

        var found = await _service.GetByIdAsync(registered.Id);

        Assert.NotNull(found);
        Assert.Equal(registered.Id, found.Id);
        Assert.Equal("Storgata 1A", found.Address);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var unit = await _service.GetByIdAsync(999);

        Assert.Null(unit);
    }

    [Fact]
    public async Task GetAllAsync_NoUnits_ReturnsEmptyList()
    {
        var units = await _service.GetAllAsync();

        Assert.Empty(units);
    }

    [Fact]
    public async Task GetAllAsync_TwoUnits_ReturnsBothWithTheirIds()
    {
        var first = await _service.RegisterAsync("Storgata 1A");
        var second = await _service.RegisterAsync("Storgata 1B");

        var units = await _service.GetAllAsync();

        Assert.Equal([first.Id, second.Id], units.Select(unit => unit.Id));
        Assert.Equal(["Storgata 1A", "Storgata 1B"], units.Select(unit => unit.Address));
    }

    [Fact]
    public async Task ChangeAddressAsync_ExistingUnit_ChangesAddress()
    {
        var unit = await _service.RegisterAsync("Storgata 1A");

        await _service.ChangeAddressAsync(unit.Id, "Storgata 2A");

        var found = await _service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 2A", found!.Address);
    }

    [Fact]
    public async Task ChangeAddressAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeAddressAsync(999, "Storgata 2A"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChangeAddressAsync_InvalidAddress_ThrowsAndKeepsOldAddress(string? newAddress)
    {
        var unit = await _service.RegisterAsync("Storgata 1A");

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.ChangeAddressAsync(unit.Id, newAddress!));

        var found = await _service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 1A", found!.Address);
    }

    private static HousingUnitDetails FullDetails() => new()
    {
        Address = "Storgata 1A",
        UnitNumber = "H0101",
        SectionNumber = 12,
        HousingType = HousingType.Duplex,
        AreaSquareMeters = 85.5m,
        OwnerName = "Test Eier",
        OwnerPhoneNumber = "400 00 000",
        OwnerEmail = "eier@example.test",
        SecondOwnerName = "Test Medeier",
        Notes = "Fiktive testdata."
    };

    [Fact]
    public async Task RegisterAsync_AllDetails_StoresEverythingAndIsActive()
    {
        var registered = await _service.RegisterAsync(FullDetails());

        var found = await _service.GetByIdAsync(registered.Id);
        Assert.Equal("H0101", found!.UnitNumber);
        Assert.Equal(HousingType.Duplex, found.HousingType);
        Assert.Equal("Test Medeier", found.SecondOwnerName);
        Assert.True(found.IsActive);
    }

    [Fact]
    public async Task RegisterAsync_InvalidDetails_ThrowsAndStoresNothing()
    {
        var invalid = FullDetails() with { OwnerEmail = "not-an-email" };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterAsync(invalid));

        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task UpdateDetailsAsync_ExistingUnit_ReplacesDetails()
    {
        var unit = await _service.RegisterAsync(FullDetails());

        await _service.UpdateDetailsAsync(unit.Id, FullDetails() with { Address = "Storgata 2A", SecondOwnerName = null });

        var found = await _service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 2A", found!.Address);
        Assert.Null(found.SecondOwnerName);
        Assert.Equal("Test Eier", found.OwnerName);
    }

    [Fact]
    public async Task UpdateDetailsAsync_InvalidDetails_ThrowsAndKeepsStoredValues()
    {
        var unit = await _service.RegisterAsync(FullDetails());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(unit.Id, FullDetails() with { Address = "Ny adresse", AreaSquareMeters = -1 }));

        var found = await _service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 1A", found!.Address);
        Assert.Equal(85.5m, found.AreaSquareMeters);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateDetailsAsync(999, FullDetails()));
    }

    [Fact]
    public async Task DeactivateAsync_ActiveUnit_IsStoredAsInactiveWithDataKept()
    {
        var unit = await _service.RegisterAsync(FullDetails());

        await _service.DeactivateAsync(unit.Id);

        var found = await _service.GetByIdAsync(unit.Id);
        Assert.False(found!.IsActive);
        Assert.Equal("Test Eier", found.OwnerName);
    }

    [Fact]
    public async Task ActivateAsync_InactiveUnit_IsStoredAsActive()
    {
        var unit = await _service.RegisterAsync(FullDetails());
        await _service.DeactivateAsync(unit.Id);

        await _service.ActivateAsync(unit.Id);

        var found = await _service.GetByIdAsync(unit.Id);
        Assert.True(found!.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeactivateAsync(999));
    }

    [Fact]
    public async Task ActivateAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ActivateAsync(999));
    }
}
