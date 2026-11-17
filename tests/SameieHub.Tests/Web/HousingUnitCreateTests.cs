using Bunit;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitCreateTests : HousingUnitUiTestBase
{
    [Fact]
    public async Task Submit_ValidAddress_StoresUnitAndGoesToList()
    {
        var cut = Render<HousingUnitCreate>();

        cut.Find("#address").Change("  Storgata 1A  ");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("/boenheter?status=opprettet", CurrentPath));
        var units = await Service.GetAllAsync();
        Assert.Equal("Storgata 1A", Assert.Single(units).Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_EmptyOrBlankAddress_ShowsMessageKeepsInputAndStoresNothing(string address)
    {
        var cut = Render<HousingUnitCreate>();

        cut.Find("#address").Change(address);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Adressen må fylles ut.", cut.Find(".validation-message").TextContent));
        Assert.Equal(address, cut.Find("#address").GetAttribute("value"));
        Assert.Empty(await Service.GetAllAsync());
    }

    [Fact]
    public void Submit_DatabaseUnavailable_ShowsErrorAndKeepsInput()
    {
        var cut = Render<HousingUnitCreate>();
        Repository.IsFailing = true;

        cut.Find("#address").Change("Storgata 1A");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Storgata 1A", cut.Find("#address").GetAttribute("value"));
        Assert.DoesNotContain("status=opprettet", CurrentPath);
    }

    [Fact]
    public void Render_HasLinkBackToList()
    {
        var cut = Render<HousingUnitCreate>();

        Assert.Equal("/boenheter", cut.Find("a.back-link").GetAttribute("href"));
    }
}
