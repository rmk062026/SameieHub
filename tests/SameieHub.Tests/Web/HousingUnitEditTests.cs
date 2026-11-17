using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitEditTests : HousingUnitUiTestBase
{
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
        SecondOwnerEmail = "medeier@example.test",
        Notes = "Fiktive testdata."
    };

    [Fact]
    public async Task Render_ExistingUnit_ShowsCurrentValuesIncludingSecondOwner()
    {
        var unit = await Service.RegisterAsync(FullDetails());

        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForAssertion(() => Assert.Equal("Storgata 1A", cut.Find("#address").GetAttribute("value")));
        Assert.Equal("H0101", cut.Find("#unitNumber").GetAttribute("value"));
        Assert.Equal("12", cut.Find("#sectionNumber").GetAttribute("value"));
        Assert.Equal("Duplex", cut.Find("#housingType").GetAttribute("value"));
        Assert.Equal("Test Eier", cut.Find("#ownerName").GetAttribute("value"));
        Assert.Equal("Test Medeier", cut.Find("#secondOwnerName").GetAttribute("value"));
        Assert.Equal("Fiktive testdata.", cut.Find("#notes").GetAttribute("value"));
    }

    [Fact]
    public async Task Render_UnitWithoutSecondOwner_HidesSecondOwnerFields()
    {
        var unit = await Service.RegisterAsync(FullDetails() with
        {
            SecondOwnerName = null,
            SecondOwnerEmail = null
        });

        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForElement("#address");
        Assert.Empty(cut.FindAll("#secondOwnerName"));
    }

    [Fact]
    public async Task Submit_ChangedValues_AreStoredAndGoToDetailPage()
    {
        var unit = await Service.RegisterAsync(FullDetails());
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");

        cut.Find("#address").Change("Storgata 2A");
        cut.Find("#ownerPhoneNumber").Change("400 00 009");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal($"/boenheter/{unit.Id}?status=endret", CurrentPath));
        var stored = await Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 2A", stored!.Address);
        Assert.Equal("400 00 009", stored.OwnerPhoneNumber);
        Assert.Equal("Test Eier", stored.OwnerName);
        Assert.Equal("Test Medeier", stored.SecondOwnerName);
    }

    [Fact]
    public async Task Submit_RemovingSecondOwner_ClearsOnlySecondOwner()
    {
        var unit = await Service.RegisterAsync(FullDetails());
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");

        cut.FindAll("button").First(button => button.TextContent.Trim() == "Fjern eier 2").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal($"/boenheter/{unit.Id}?status=endret", CurrentPath));
        var stored = await Service.GetByIdAsync(unit.Id);
        Assert.Null(stored!.SecondOwnerName);
        Assert.Null(stored.SecondOwnerEmail);
        Assert.Equal("Test Eier", stored.OwnerName);
        Assert.Equal("400 00 000", stored.OwnerPhoneNumber);
        Assert.Equal("eier@example.test", stored.OwnerEmail);
    }

    [Fact]
    public async Task Submit_ClearingOptionalFields_StoresNulls()
    {
        var unit = await Service.RegisterAsync(FullDetails());
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");

        cut.Find("#unitNumber").Change("");
        cut.Find("#sectionNumber").Change("");
        cut.Find("#housingType").Change("");
        cut.Find("#areaSquareMeters").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("status=endret", CurrentPath));
        var stored = await Service.GetByIdAsync(unit.Id);
        Assert.Null(stored!.UnitNumber);
        Assert.Null(stored.SectionNumber);
        Assert.Null(stored.HousingType);
        Assert.Null(stored.AreaSquareMeters);
    }

    [Fact]
    public async Task Submit_EmptyAddress_ShowsMessageAndKeepsStoredValues()
    {
        var unit = await Service.RegisterAsync(FullDetails());
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");

        cut.Find("#address").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Adressen må fylles ut.", cut.Find(".validation-message").TextContent));
        var stored = await Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 1A", stored!.Address);
    }

    [Fact]
    public async Task Submit_InvalidEmail_ShowsMessageAndDoesNotChangeAnyStoredValue()
    {
        var unit = await Service.RegisterAsync(FullDetails());
        var cut = Render<HousingUnitEdit>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#address");

        cut.Find("#address").Change("Storgata 2A");
        cut.Find("#ownerEmail").Change("feil");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("gyldig e-postadresse", cut.Find(".validation-message").TextContent));
        var stored = await Service.GetByIdAsync(unit.Id);
        Assert.Equal("Storgata 1A", stored!.Address);
        Assert.Equal("eier@example.test", stored.OwnerEmail);
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
