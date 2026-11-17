using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitDetailTests : HousingUnitUiTestBase
{
    [Fact]
    public async Task Render_FullyRegisteredUnit_ShowsAllInformationAndBothOwners()
    {
        var unit = await Service.RegisterAsync(new HousingUnitDetails
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
            SecondOwnerEmail = "medeier@example.test",
            Notes = "Fiktive testdata."
        });

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForAssertion(() => Assert.Contains("H0101", cut.Markup));
        var text = cut.Markup;
        Assert.Contains("Storgata 1A", text);
        Assert.Contains("Tomannsbolig", text);
        Assert.Contains("85,5 m²", text.Replace("85.5", "85,5"));
        Assert.Contains("Test Eier", text);
        Assert.Contains("Test Medeier", text);
        Assert.Contains("Eier 1", text);
        Assert.Contains("Eier 2", text);
        Assert.Contains("Fiktive testdata.", text);
        Assert.Equal("mailto:eier@example.test", cut.Find("a[href^='mailto:eier']").GetAttribute("href"));
        Assert.Equal("tel:400 00 000", cut.Find("a[href^='tel:']").GetAttribute("href"));
    }

    [Fact]
    public async Task Render_OnlyAddress_ShowsMissingValuesTidilyAndHidesSecondOwner()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForAssertion(() => Assert.Contains("Storgata 1A", cut.Markup));
        Assert.Contains("Ikke registrert", cut.Markup);
        Assert.DoesNotContain("Eier 2", cut.Markup);
        Assert.DoesNotContain("Eier 1", cut.Markup);
    }

    [Fact]
    public async Task Render_Unit_HasEditLinkAndBackLink()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForElement(".page-actions");
        Assert.Equal($"/boenheter/{unit.Id}/rediger", cut.Find(".page-actions a").GetAttribute("href"));
        Assert.Equal("/boenheter", cut.Find("a.back-link").GetAttribute("href"));
    }

    [Fact]
    public async Task Deactivate_ActiveUnit_ShowsInactiveStatusAndStoresIt()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".status-badge");
        Assert.Equal("Aktiv", cut.Find(".status-badge").TextContent);

        ClickButton(cut, "Deaktiver");

        cut.WaitForAssertion(() => Assert.Equal("Inaktiv", cut.Find(".status-badge").TextContent));
        Assert.Contains("Boenheten er deaktivert.", cut.Find("[role=status]").TextContent);
        Assert.False((await Service.GetByIdAsync(unit.Id))!.IsActive);
    }

    [Fact]
    public async Task Reactivate_InactiveUnit_ShowsActiveStatusAndStoresIt()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await Service.DeactivateAsync(unit.Id);
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".status-badge");

        ClickButton(cut, "Reaktiver");

        cut.WaitForAssertion(() => Assert.Equal("Aktiv", cut.Find(".status-badge").TextContent));
        Assert.True((await Service.GetByIdAsync(unit.Id))!.IsActive);
    }

    [Fact]
    public async Task Deactivate_DatabaseUnavailable_ShowsError()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".status-badge");
        Repository.IsFailing = true;

        ClickButton(cut, "Deaktiver");

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke endre status", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public void Render_UnknownId_ShowsNotFoundMessage()
    {
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, 999));

        cut.WaitForAssertion(() => Assert.Contains("Fant ikke boenheten", cut.Markup));
    }

    private static void ClickButton(IRenderedComponent<HousingUnitDetail> cut, string text)
    {
        cut.FindAll("button").First(button => button.TextContent.Trim() == text).Click();
    }
}
