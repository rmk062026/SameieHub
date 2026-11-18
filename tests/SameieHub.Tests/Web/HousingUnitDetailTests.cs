using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class HousingUnitDetailTests : UiTestBase
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

    [Fact]
    public async Task Render_Unit_ShowsDeleteButtonButNoConfirmationAndDeletesNothing()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForElement(".danger-zone");
        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.NotNull(await Service.GetByIdAsync(unit.Id));
    }

    [Fact]
    public async Task Delete_ClickingDelete_AsksForConfirmationWithAddressAndWarningAndDeletesNothing()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".danger-zone");

        ClickButton(cut, "Slett boenheten …");

        var dialog = cut.Find("[role=alertdialog]");
        Assert.Contains("Storgata 1A", dialog.TextContent);
        Assert.Contains("Dette kan ikke angres", dialog.TextContent);
        Assert.NotNull(await Service.GetByIdAsync(unit.Id));
    }

    [Fact]
    public async Task Delete_Cancel_ClosesConfirmationAndDeletesNothing()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".danger-zone");
        ClickButton(cut, "Slett boenheten …");

        ClickButton(cut, "Avbryt");

        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.NotNull(await Service.GetByIdAsync(unit.Id));
        Assert.DoesNotContain("status=slettet", CurrentPath);
    }

    [Fact]
    public async Task Delete_Confirm_DeletesUnitAndGoesToListWithMessage()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var other = await Service.RegisterAsync("Storgata 1B");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".danger-zone");
        ClickButton(cut, "Slett boenheten …");

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Equal("/boenheter?status=slettet", CurrentPath));
        Assert.Null(await Service.GetByIdAsync(unit.Id));
        Assert.NotNull(await Service.GetByIdAsync(other.Id));
    }

    [Fact]
    public async Task Delete_UnitInUse_ShowsUnderstandableMessageAndKeepsUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".danger-zone");
        Repository.IsDeletionBlocked = true;
        ClickButton(cut, "Slett boenheten …");

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("knyttet til styresaker eller oppgaver", cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.DoesNotContain("status=slettet", CurrentPath);
        Repository.IsDeletionBlocked = false;
        Assert.NotNull(await Service.GetByIdAsync(unit.Id));
    }

    [Fact]
    public async Task Delete_DatabaseUnavailable_ShowsErrorAndKeepsUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".danger-zone");
        ClickButton(cut, "Slett boenheten …");
        Repository.IsFailing = true;

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke slette boenheten", cut.Find("[role=alert]").TextContent));
        Repository.IsFailing = false;
        Assert.NotNull(await Service.GetByIdAsync(unit.Id));
    }

    [Fact]
    public async Task Delete_UnitAlreadyDeleted_ShowsNotFoundMessage()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement(".danger-zone");
        ClickButton(cut, "Slett boenheten …");
        await Service.DeleteAsync(unit.Id);

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("finnes ikke lenger", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public async Task Render_UnitWithBoardCases_ListsThemWithStatusAndLinkAndOmitsOtherUnitsCases()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var otherUnit = await Service.RegisterAsync("Storgata 1B");
        var linked = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter", HousingUnitId = unit.Id });
        await BoardCases.ChangeStatusAsync(linked.Id, BoardCaseStatus.InProgress);
        await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak for annen boenhet", HousingUnitId = otherUnit.Id });
        await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak for hele sameiet" });

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForElement("tbody tr");
        var row = Assert.Single(cut.FindAll("tbody tr"));
        Assert.Contains($"#{linked.Id}", row.TextContent);
        Assert.Contains("Maling av porter", row.TextContent);
        Assert.Contains("Under behandling", row.TextContent);
        Assert.Equal($"/styresaker/{linked.Id}", row.QuerySelector("a")!.GetAttribute("href"));
        Assert.DoesNotContain("Sak for annen boenhet", cut.Markup);
    }

    [Fact]
    public async Task Render_UnitWithoutBoardCases_ShowsShortEmptyState()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForAssertion(() => Assert.Contains("Ingen styresaker er knyttet til denne boenheten.", cut.Markup));
    }

    private static void ClickButton(IRenderedComponent<HousingUnitDetail> cut, string text)
    {
        cut.FindAll("button").First(button => button.TextContent.Trim() == text).Click();
    }

    [Fact]
    public async Task Render_ActiveUnit_ShowsRegisterBoardCaseShortcutWithTheUnitInTheLink()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForElement(".section-heading");
        var link = cut.Find(".section-heading a");
        Assert.Equal("Registrer styresak", link.TextContent);
        Assert.Equal($"/styresaker/ny?boenhet={unit.Id}", link.GetAttribute("href"));
    }

    [Fact]
    public async Task Render_InactiveUnit_HasNoRegisterBoardCaseShortcut()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await Service.DeactivateAsync(unit.Id);

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForElement(".section-heading");
        Assert.Empty(cut.FindAll(".section-heading a"));
    }

    [Fact]
    public async Task Render_InactiveUnitWithExistingBoardCase_StillListsTheCase()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));

        cut.WaitForElement("tbody tr");
        Assert.Contains("Maling av porter", cut.Find("tbody").TextContent);
    }
}
