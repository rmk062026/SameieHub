using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.BoardCases;

namespace SameieHub.Tests.Web;

public class BoardCaseCreateTests : UiTestBase
{
    private IRenderedComponent<BoardCaseCreate> RenderLoaded()
    {
        var cut = Render<BoardCaseCreate>();
        cut.WaitForElement("#title");

        return cut;
    }

    [Fact]
    public async Task Submit_OnlyTitle_StoresCaseWithDefaultsAndGoesToDetailPage()
    {
        var cut = RenderLoaded();

        cut.Find("#title").Change("  Maling av fellesarealer  ");
        cut.Find("form").Submit();

        var boardCase = Assert.Single(await BoardCases.GetAllAsync());
        cut.WaitForAssertion(() => Assert.Equal($"/styresaker/{boardCase.Id}?status=opprettet", CurrentPath));
        Assert.Equal("Maling av fellesarealer", boardCase.Title);
        Assert.Equal(BoardCaseStatus.New, boardCase.Status);
        Assert.Equal(BoardCasePriority.Normal, boardCase.Priority);
        Assert.Null(boardCase.HousingUnitId);
    }

    [Fact]
    public async Task Submit_AllFields_StoresEverything()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = RenderLoaded();

        cut.Find("#title").Change("Maling av fellesarealer");
        cut.Find("#description").Change("Fiktiv beskrivelse.");
        cut.Find("#priority").Change("High");
        cut.Find("#meetingDate").Change("2026-12-03");
        cut.ChooseHousingUnit(unit.Id);
        cut.Find("#notes").Change("Fiktive merknader.");
        cut.Find("form").Submit();

        var boardCase = Assert.Single(await BoardCases.GetAllAsync());
        Assert.Equal("Fiktiv beskrivelse.", boardCase.Description);
        Assert.Equal(BoardCasePriority.High, boardCase.Priority);
        Assert.Equal(new DateOnly(2026, 12, 3), boardCase.MeetingDate);
        Assert.Equal(unit.Id, boardCase.HousingUnitId);
        Assert.Equal("Fiktive merknader.", boardCase.Notes);
    }

    [Fact]
    public async Task Render_HousingUnitChoices_OnlyContainActiveUnitsAndWholeCooperative()
    {
        await Service.RegisterAsync("Storgata 1A");
        var inactive = await Service.RegisterAsync("Storgata 1B");
        await Service.DeactivateAsync(inactive.Id);

        var cut = RenderLoaded();

        Assert.True(cut.IsWholeCooperativeChosen());
        cut.Find("#scope-unit").Change(true);
        var options = cut.FindAll("#housingUnitId option").Select(option => option.TextContent).ToList();
        Assert.Equal(["Velg boenhet", "Storgata 1A"], options);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Submit_EmptyTitle_ShowsMessageKeepsInputAndStoresNothing(string title)
    {
        var cut = RenderLoaded();

        cut.Find("#title").Change(title);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Tittelen må fylles ut.", cut.Find(".validation-message").TextContent));
        Assert.Equal(title, cut.Find("#title").GetAttribute("value"));
        Assert.Empty(await BoardCases.GetAllAsync());
    }

    [Fact]
    public async Task Submit_MeetingDateOutOfRange_ShowsNorwegianMessageAtTheFieldAndStoresNothing()
    {
        var cut = RenderLoaded();
        cut.Find("#title").Change("Maling");

        cut.Find("#meetingDate").Change("1999-01-01");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Equal("Møtedatoen må være mellom 01.01.2000 og 31.12.2100.", cut.Find(".validation-message").TextContent));
        Assert.Equal("Maling", cut.Find("#title").GetAttribute("value"));
        Assert.Empty(await BoardCases.GetAllAsync());
    }

    [Fact]
    public async Task Submit_AfterCorrectingTheDate_Saves()
    {
        var cut = RenderLoaded();
        cut.Find("#title").Change("Maling");
        cut.Find("#meetingDate").Change("1999-01-01");
        cut.Find("form").Submit();
        cut.WaitForElement(".validation-message");

        cut.Find("#meetingDate").Change("2026-12-03");
        cut.Find("form").Submit();

        Assert.Single(await BoardCases.GetAllAsync());
    }

    [Fact]
    public void Submit_DatabaseUnavailable_ShowsErrorAndKeepsInput()
    {
        var cut = RenderLoaded();
        BoardCaseRepository.IsFailing = true;

        cut.Find("#title").Change("Maling");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Maling", cut.Find("#title").GetAttribute("value"));
        Assert.DoesNotContain("status=opprettet", CurrentPath);
    }

    [Fact]
    public void Render_HousingUnitsUnavailable_ShowsErrorAndRetryShowsTheForm()
    {
        Repository.IsFailing = true;
        var cut = Render<BoardCaseCreate>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente boenhetene", cut.Find("[role=alert]").TextContent));

        Repository.IsFailing = false;
        cut.Find("button").Click();

        cut.WaitForElement("#title");
    }

    [Fact]
    public void Render_HasLinkBackToList()
    {
        var cut = Render<BoardCaseCreate>();

        Assert.Equal("/styresaker", cut.Find("a.back-link").GetAttribute("href"));
    }

    // Shortcut from the housing unit page

    private IRenderedComponent<BoardCaseCreate> RenderWithQuery(string query)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/styresaker/ny" + query);
        var cut = Render<BoardCaseCreate>();
        cut.WaitForElement("#title");

        return cut;
    }

    [Fact]
    public async Task Shortcut_ActiveHousingUnit_IsPreselectedAndSavedWithTheCase()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await Service.RegisterAsync("Storgata 1B");

        var cut = RenderWithQuery($"?boenhet={unit.Id}");

        Assert.Equal(unit.Id.ToString(), cut.SelectedHousingUnit());
        Assert.Empty(cut.FindAll(".alert-warning"));
        cut.Find("#title").Change("Maling");
        cut.Find("form").Submit();
        Assert.Equal(unit.Id, Assert.Single(await BoardCases.GetAllAsync()).HousingUnitId);
    }

    [Fact]
    public async Task Shortcut_InactiveHousingUnit_IsNotPreselectedNotOfferedAndShowsNotice()
    {
        var inactive = await Service.RegisterAsync("Storgata 1A");
        await Service.DeactivateAsync(inactive.Id);

        var cut = RenderWithQuery($"?boenhet={inactive.Id}");

        Assert.True(cut.IsWholeCooperativeChosen());
        Assert.Empty(cut.FindAll("#housingUnitId"));
        Assert.DoesNotContain(cut.FindAll("#housingUnitId option"), option => option.TextContent.Contains("Storgata 1A"));
        Assert.Contains("finnes ikke eller er inaktiv", cut.Find(".alert-warning").TextContent);
        cut.Find("#title").Change("Maling");
        cut.Find("form").Submit();
        Assert.Null(Assert.Single(await BoardCases.GetAllAsync()).HousingUnitId);
    }

    [Fact]
    public void Shortcut_UnknownHousingUnit_IsNotPreselectedAndShowsNotice()
    {
        var cut = RenderWithQuery("?boenhet=999");

        Assert.True(cut.IsWholeCooperativeChosen());
        Assert.Empty(cut.FindAll("#housingUnitId"));
        Assert.Contains("finnes ikke eller er inaktiv", cut.Find(".alert-warning").TextContent);
    }

    [Fact]
    public async Task Shortcut_NoHousingUnitInTheLink_ShowsNoNoticeAndWholeCooperative()
    {
        await Service.RegisterAsync("Storgata 1A");

        var cut = RenderWithQuery("");

        Assert.Empty(cut.FindAll(".alert-warning"));
        Assert.True(cut.IsWholeCooperativeChosen());
        Assert.Empty(cut.FindAll("#housingUnitId"));
    }
}
