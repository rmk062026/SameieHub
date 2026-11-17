using Bunit;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitListTests : HousingUnitUiTestBase
{
    [Fact]
    public void Render_NoUnits_ShowsEmptyState()
    {
        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Contains("Ingen boenheter registrert ennå", cut.Markup));
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public async Task Render_StoredUnits_ShowsIdAndAddressSortedByAddress()
    {
        await Service.RegisterAsync("Storgata 2B");
        await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));
        var rows = cut.FindAll("tbody tr");
        Assert.Contains("Storgata 1A", rows[0].TextContent);
        Assert.Contains("Storgata 2B", rows[1].TextContent);
        Assert.Contains("2", rows[0].QuerySelector(".col-id")!.TextContent);
        Assert.Contains("1", rows[1].QuerySelector(".col-id")!.TextContent);
    }

    [Fact]
    public async Task Render_NumbersInAddresses_AreSortedNumerically()
    {
        await Service.RegisterAsync("Storgata 10B");
        await Service.RegisterAsync("Storgata 2A");

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));
        var rows = cut.FindAll("tbody tr");
        Assert.Contains("Storgata 2A", rows[0].TextContent);
        Assert.Contains("Storgata 10B", rows[1].TextContent);
    }

    [Fact]
    public async Task Render_StoredUnits_ShowsCount()
    {
        await Service.RegisterAsync("Storgata 1A");
        await Service.RegisterAsync("Storgata 1B");

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Equal("2 boenheter registrert", cut.Find(".summary").TextContent));
    }

    [Fact]
    public async Task Render_StoredUnits_LinksToEditPageOfEachUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() =>
            Assert.Equal($"/boenheter/{unit.Id}/rediger", cut.Find("tbody a").GetAttribute("href")));
    }

    [Fact]
    public void Render_DatabaseUnavailable_ShowsErrorAndRetryLoadsData()
    {
        Repository.IsFailing = true;
        var cut = Render<HousingUnitList>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente boenhetene", cut.Find("[role=alert]").TextContent));

        Repository.IsFailing = false;
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Ingen boenheter registrert ennå", cut.Markup));
    }
}
