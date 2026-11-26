using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitListTests : UiTestBase
{
    [Fact]
    public void Render_NoUnits_ShowsEmptyState()
    {
        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Contains("Ingen boenheter registrert ennå", cut.Markup));
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public async Task Render_StoredUnits_ShowsAddressOwnersAndStatusSortedNumerically()
    {
        await Service.RegisterAsync(new HousingUnitDetails
        {
            Address = "Storgata 10B",
            OwnerName = "Test Eier",
            SecondOwnerName = "Test Medeier",
            SectionNumber = 12
        });
        await Service.RegisterAsync("Storgata 2A");

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));
        var rows = cut.FindAll("tbody tr");
        Assert.Contains("Storgata 2A", rows[0].TextContent);
        Assert.Contains("Storgata 10B", rows[1].TextContent);
        Assert.Contains("Test Eier og Test Medeier", rows[1].TextContent);
        Assert.Contains("Aktiv", rows[1].TextContent);
    }

    [Fact]
    public async Task Render_OneOwner_ShowsOnlyThatName()
    {
        await Service.RegisterAsync(new HousingUnitDetails { Address = "Storgata 1A", OwnerName = "Test Eier" });

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Equal("Test Eier", cut.Find(".owner-cell").TextContent));
    }

    [Fact]
    public async Task Render_StoredUnits_ShowsCountsForRegisteredActiveAndInactive()
    {
        await Service.RegisterAsync("Storgata 1A");
        await Service.RegisterAsync("Storgata 1B");
        var inactive = await Service.RegisterAsync("Storgata 1C");
        await Service.DeactivateAsync(inactive.Id);

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() =>
        {
            var values = cut.FindAll(".stat-value").Select(element => element.TextContent).ToList();
            Assert.Equal(["3", "2", "1"], values);
        });
    }

    [Fact]
    public async Task Render_Default_ShowsOnlyActiveUnits()
    {
        await Service.RegisterAsync("Storgata 1A");
        var inactive = await Service.RegisterAsync("Storgata 1B");
        await Service.DeactivateAsync(inactive.Id);

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr")));
        Assert.Contains("Storgata 1A", cut.Find("tbody").TextContent);
    }

    [Fact]
    public async Task StatusFilter_AllAndInactive_ChangeWhichUnitsAreShown()
    {
        await Service.RegisterAsync("Storgata 1A");
        var inactive = await Service.RegisterAsync("Storgata 1B");
        await Service.DeactivateAsync(inactive.Id);
        var cut = Render<HousingUnitList>();
        cut.WaitForElement("tbody tr");

        ClickFilter(cut, "Alle");
        Assert.Equal(2, cut.FindAll("tbody tr").Count);

        ClickFilter(cut, "Inaktive");
        var row = Assert.Single(cut.FindAll("tbody tr"));
        Assert.Contains("Storgata 1B", row.TextContent);
        Assert.Contains("Inaktiv", row.TextContent);
    }

    [Theory]
    [InlineData("kari")]
    [InlineData("OLA")]
    [InlineData("h0101")]
    public async Task Search_FindsUnitByFirstOwnerSecondOwnerOrUnitNumber(string searchText)
    {
        await Service.RegisterAsync(new HousingUnitDetails
        {
            Address = "Storgata 1A",
            UnitNumber = "H0101",
            OwnerName = "Kari Testesen",
            SecondOwnerName = "Ola Prøvesen"
        });
        await Service.RegisterAsync("Parkveien 5");
        var cut = Render<HousingUnitList>();
        cut.WaitForElement("tbody tr");

        cut.Find("input[type=search]").Input(searchText);

        var row = Assert.Single(cut.FindAll("tbody tr"));
        Assert.Contains("Storgata 1A", row.TextContent);
    }

    [Fact]
    public async Task Search_NoMatch_ShowsMessageAndResetShowsUnitsAgain()
    {
        await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitList>();
        cut.WaitForElement("tbody tr");

        cut.Find("input[type=search]").Input("finnes-ikke");

        Assert.Contains("Ingen boenheter funnet", cut.Markup);
        cut.Find(".empty-state button").Click();
        Assert.Single(cut.FindAll("tbody tr"));
    }

    [Fact]
    public async Task Render_StoredUnits_TheAddressLinksToTheDetailPage_AndThereIsNoEditButtonPerRow()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitList>();

        cut.WaitForElement("tbody tr");
        var link = Assert.Single(cut.FindAll("tbody a"));
        Assert.Equal($"/boenheter/{unit.Id}", link.GetAttribute("href"));
        Assert.Equal("Storgata 1A", link.TextContent.Trim());
        Assert.Empty(cut.FindAll("tbody .btn"));
    }

    [Fact]
    public async Task Render_UnitWithoutOwner_SaysSo_AndTheUnitNumberIsShownOnlyWhenItIsNotInTheAddress()
    {
        await Service.RegisterAsync(new HousingUnitDetails { Address = "Storgata 1A", UnitNumber = "1A" });
        await Service.RegisterAsync(new HousingUnitDetails { Address = "Storgata 2", UnitNumber = "H0201" });

        var cut = Render<HousingUnitList>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));
        var rows = cut.FindAll("tbody tr");
        Assert.Empty(rows[0].QuerySelectorAll(".unit-sub"));
        Assert.Contains("H0201", rows[1].QuerySelector(".unit-sub")!.TextContent);
        Assert.Contains("Ikke registrert", rows[0].QuerySelector(".owner-cell")!.TextContent);
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

    private static void ClickFilter(IRenderedComponent<HousingUnitList> cut, string text)
    {
        cut.FindAll(".btn-group button").First(button => button.TextContent.Trim() == text).Click();
    }
}
