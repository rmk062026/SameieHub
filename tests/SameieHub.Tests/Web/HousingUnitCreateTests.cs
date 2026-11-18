using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitCreateTests : UiTestBase
{
    [Fact]
    public async Task Submit_OnlyAddress_StoresTrimmedUnitAndGoesToDetailPage()
    {
        var cut = Render<HousingUnitCreate>();

        cut.Find("#address").Change("  Storgata 1A  ");
        cut.Find("form").Submit();

        var unit = Assert.Single(await Service.GetAllAsync());
        cut.WaitForAssertion(() => Assert.Equal($"/boenheter/{unit.Id}?status=opprettet", CurrentPath));
        Assert.Equal("Storgata 1A", unit.Address);
        Assert.True(unit.IsActive);
        Assert.Null(unit.OwnerName);
    }

    [Fact]
    public async Task Submit_AllFieldsIncludingSecondOwner_StoresEverything()
    {
        var cut = Render<HousingUnitCreate>();

        cut.Find("#address").Change("Storgata 1A");
        cut.Find("#unitNumber").Change("H0101");
        cut.Find("#sectionNumber").Change("12");
        cut.Find("#housingType").Change("Duplex");
        cut.Find("#areaSquareMeters").Change("85.5");
        cut.Find("#ownerName").Change("Test Eier");
        cut.Find("#ownerPhoneNumber").Change("+47 400 00 000");
        cut.Find("#ownerEmail").Change("eier@example.test");
        cut.Find("#notes").Change("Fiktive testdata.");
        ClickButton(cut, "+ Legg til eier 2");
        cut.Find("#secondOwnerName").Change("Test Medeier");
        cut.Find("#secondOwnerPhoneNumber").Change("400 00 001");
        cut.Find("#secondOwnerEmail").Change("medeier@example.test");
        cut.Find("form").Submit();

        var unit = Assert.Single(await Service.GetAllAsync());
        Assert.Equal("H0101", unit.UnitNumber);
        Assert.Equal(12, unit.SectionNumber);
        Assert.Equal(HousingType.Duplex, unit.HousingType);
        Assert.Equal(85.5m, unit.AreaSquareMeters);
        Assert.Equal("Test Eier", unit.OwnerName);
        Assert.Equal("+47 400 00 000", unit.OwnerPhoneNumber);
        Assert.Equal("eier@example.test", unit.OwnerEmail);
        Assert.Equal("Test Medeier", unit.SecondOwnerName);
        Assert.Equal("400 00 001", unit.SecondOwnerPhoneNumber);
        Assert.Equal("medeier@example.test", unit.SecondOwnerEmail);
        Assert.Equal("Fiktive testdata.", unit.Notes);
    }

    [Fact]
    public void Render_SecondOwnerFieldsAreHiddenUntilAdded_AndCanBeRemovedAgain()
    {
        var cut = Render<HousingUnitCreate>();
        Assert.Empty(cut.FindAll("#secondOwnerName"));

        ClickButton(cut, "+ Legg til eier 2");
        Assert.Single(cut.FindAll("#secondOwnerName"));

        ClickButton(cut, "Fjern eier 2");
        Assert.Empty(cut.FindAll("#secondOwnerName"));
    }

    [Fact]
    public async Task Submit_SecondOwnerRemovedBeforeSaving_StoresNoSecondOwner()
    {
        var cut = Render<HousingUnitCreate>();
        cut.Find("#address").Change("Storgata 1A");
        ClickButton(cut, "+ Legg til eier 2");
        cut.Find("#secondOwnerName").Change("Skal fjernes");
        ClickButton(cut, "Fjern eier 2");

        cut.Find("form").Submit();

        var unit = Assert.Single(await Service.GetAllAsync());
        Assert.Null(unit.SecondOwnerName);
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

    [Theory]
    [InlineData("#ownerEmail", "ikke-en-epost", "Skriv inn en gyldig e-postadresse")]
    [InlineData("#ownerPhoneNumber", "abc", "Telefonnummer kan bare inneholde")]
    [InlineData("#sectionNumber", "0", "Seksjonsnummer må være et helt tall")]
    [InlineData("#areaSquareMeters", "0", "Arealet må være større enn 0")]
    public async Task Submit_InvalidValue_ShowsMessageAtTheFieldKeepsInputAndStoresNothing(
        string fieldSelector, string value, string expectedMessage)
    {
        var cut = Render<HousingUnitCreate>();
        cut.Find("#address").Change("Storgata 1A");

        cut.Find(fieldSelector).Change(value);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains(expectedMessage, cut.Find(".validation-message").TextContent));
        Assert.Equal(value, cut.Find(fieldSelector).GetAttribute("value"));
        Assert.Equal("Storgata 1A", cut.Find("#address").GetAttribute("value"));
        Assert.Empty(await Service.GetAllAsync());
    }

    [Fact]
    public async Task Submit_InvalidSecondOwnerEmail_ShowsMessageAndStoresNothing()
    {
        var cut = Render<HousingUnitCreate>();
        cut.Find("#address").Change("Storgata 1A");
        ClickButton(cut, "+ Legg til eier 2");

        cut.Find("#secondOwnerEmail").Change("feil");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("gyldig e-postadresse", cut.Find(".validation-message").TextContent));
        Assert.Empty(await Service.GetAllAsync());
    }

    [Fact]
    public async Task Submit_AfterCorrectingInvalidValue_Saves()
    {
        var cut = Render<HousingUnitCreate>();
        cut.Find("#address").Change("Storgata 1A");
        cut.Find("#ownerEmail").Change("feil");
        cut.Find("form").Submit();
        cut.WaitForElement(".validation-message");

        cut.Find("#ownerEmail").Change("eier@example.test");
        cut.Find("form").Submit();

        var unit = Assert.Single(await Service.GetAllAsync());
        Assert.Equal("eier@example.test", unit.OwnerEmail);
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

    private static void ClickButton(IRenderedComponent<HousingUnitCreate> cut, string text)
    {
        cut.FindAll("button").First(button => button.TextContent.Trim() == text).Click();
    }
}
