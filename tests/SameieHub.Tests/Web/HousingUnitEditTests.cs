using Bunit;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitEditTests : HousingUnitUiTestBase
{
    [Fact]
    public async Task Render_ExistingUnit_ShowsCurrentAddress()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForAssertion(() => Assert.Equal("Storgata 1A", cut.Find("#address").GetAttribute("value")));
    }

    [Fact]
    public async Task Submit_NewAddress_IsStoredAndGoesToList()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");

        cut.Find("#address").Change("Storgata 2A");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("/boenheter?status=endret", CurrentPath));
        var stored = await Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 2A", stored!.Address);
    }

    [Fact]
    public async Task Submit_EmptyAddress_ShowsMessageAndKeepsStoredAddress()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");

        cut.Find("#address").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Adressen må fylles ut.", cut.Find(".validation-message").TextContent));
        var stored = await Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 1A", stored!.Address);
    }

    [Fact]
    public void Render_UnknownId_ShowsNotFoundMessage()
    {
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, 999));

        cut.WaitForAssertion(() => Assert.Contains("Fant ikke boenheten", cut.Markup));
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task Submit_DatabaseUnavailable_ShowsErrorAndKeepsInput()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");
        Repository.IsFailing = true;

        cut.Find("#address").Change("Storgata 2A");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Storgata 2A", cut.Find("#address").GetAttribute("value"));
    }
}
