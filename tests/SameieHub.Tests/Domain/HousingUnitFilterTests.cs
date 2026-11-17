using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class HousingUnitFilterTests
{
    private readonly HousingUnit _active;
    private readonly HousingUnit _inactive;
    private readonly List<HousingUnit> _units;

    public HousingUnitFilterTests()
    {
        _active = new HousingUnit(new HousingUnitDetails
        {
            Address = "Storgata 1A",
            UnitNumber = "H0101",
            SectionNumber = 12,
            OwnerName = "Kari Testesen",
            SecondOwnerName = "Ola Prøvesen"
        });

        _inactive = new HousingUnit(new HousingUnitDetails
        {
            Address = "Parkveien 5",
            OwnerName = "Eva Eksempel"
        });
        _inactive.Deactivate();

        _units = [_active, _inactive];
    }

    [Fact]
    public void Apply_NoSearchTextAndAll_ReturnsEverything()
    {
        var result = HousingUnitFilter.Apply(_units, null, HousingUnitStatusFilter.All);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Apply_ActiveFilter_ReturnsOnlyActive()
    {
        var result = HousingUnitFilter.Apply(_units, "", HousingUnitStatusFilter.Active);

        Assert.Same(_active, Assert.Single(result));
    }

    [Fact]
    public void Apply_InactiveFilter_ReturnsOnlyInactive()
    {
        var result = HousingUnitFilter.Apply(_units, "  ", HousingUnitStatusFilter.Inactive);

        Assert.Same(_inactive, Assert.Single(result));
    }

    [Theory]
    [InlineData("storgata")]
    [InlineData("STORGATA")]
    [InlineData("H0101")]
    [InlineData("12")]
    [InlineData("kari")]
    [InlineData("ola")]
    [InlineData("  prøvesen  ")]
    public void Apply_SearchText_FindsMatchInAddressUnitSectionAndBothOwners(string searchText)
    {
        var result = HousingUnitFilter.Apply(_units, searchText, HousingUnitStatusFilter.All);

        Assert.Same(_active, Assert.Single(result));
    }

    [Fact]
    public void Apply_SearchTextCombinedWithStatus_AppliesBoth()
    {
        var result = HousingUnitFilter.Apply(_units, "eva", HousingUnitStatusFilter.Active);

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_NoMatch_ReturnsEmptyList()
    {
        var result = HousingUnitFilter.Apply(_units, "finnes-ikke", HousingUnitStatusFilter.All);

        Assert.Empty(result);
    }
}
