using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class HousingUnitTests
{
    [Fact]
    public void Constructor_ValidAddress_SetsAddress()
    {
        var unit = new HousingUnit("Storgata 1A");

        Assert.Equal("Storgata 1A", unit.Address);
    }

    [Fact]
    public void Constructor_AddressWithSurroundingSpaces_TrimsAddress()
    {
        var unit = new HousingUnit("  Storgata 1A  ");

        Assert.Equal("Storgata 1A", unit.Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidAddress_Throws(string? address)
    {
        Assert.ThrowsAny<ArgumentException>(() => new HousingUnit(address!));
    }

    [Fact]
    public void ChangeAddress_ValidAddress_ChangesAddress()
    {
        var unit = new HousingUnit("Storgata 1A");

        unit.ChangeAddress("Storgata 1B");

        Assert.Equal("Storgata 1B", unit.Address);
    }

    [Fact]
    public void ChangeAddress_AddressWithSurroundingSpaces_TrimsAddress()
    {
        var unit = new HousingUnit("Storgata 1A");

        unit.ChangeAddress("  Storgata 1B  ");

        Assert.Equal("Storgata 1B", unit.Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangeAddress_InvalidAddress_ThrowsAndKeepsOldAddress(string? newAddress)
    {
        var unit = new HousingUnit("Storgata 1A");

        Assert.ThrowsAny<ArgumentException>(() => unit.ChangeAddress(newAddress!));

        Assert.Equal("Storgata 1A", unit.Address);
    }
}
