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
}
