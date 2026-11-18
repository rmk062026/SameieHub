using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.BoardCases;

namespace SameieHub.Tests.Web;

public class BoardCaseEditTests : UiTestBase
{
    private async Task<IRenderedComponent<BoardCaseEdit>> RenderEditAsync(BoardCase boardCase)
    {
        await Task.CompletedTask;
        var cut = Render<BoardCaseEdit>(parameters => parameters.Add(page => page.Id, boardCase.Id));
        cut.WaitForElement("#title");

        return cut;
    }

    private async Task<BoardCase> RegisterFullCaseAsync(int? housingUnitId = null)
    {
        return await BoardCases.RegisterAsync(new BoardCaseDetails
        {
            Title = "Maling av fellesarealer",
            Description = "Fiktiv beskrivelse.",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(2026, 12, 3),
            HousingUnitId = housingUnitId,
            Notes = "Fiktive merknader."
        });
    }

    [Fact]
    public async Task Render_ExistingCase_ShowsCurrentValues()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var cut = await RenderEditAsync(await RegisterFullCaseAsync(unit.Id));

        Assert.Equal("Maling av fellesarealer", cut.Find("#title").GetAttribute("value"));
        Assert.Equal("Fiktiv beskrivelse.", cut.Find("#description").GetAttribute("value"));
        Assert.Equal("High", cut.Find("#priority").GetAttribute("value"));
        Assert.Equal("2026-12-03", cut.Find("#meetingDate").GetAttribute("value"));
        Assert.Equal(unit.Id.ToString(), cut.Find("#housingUnitId").GetAttribute("value"));
        Assert.Equal("Fiktive merknader.", cut.Find("#notes").GetAttribute("value"));
    }

    [Fact]
    public async Task Render_LinkedUnitDeactivatedLater_StaysSelectableAndOtherInactiveUnitsAreNotOffered()
    {
        var linked = await Service.RegisterAsync("Storgata 1A");
        var otherInactive = await Service.RegisterAsync("Storgata 1B");
        await Service.RegisterAsync("Storgata 1C");
        var boardCase = await RegisterFullCaseAsync(linked.Id);
        await Service.DeactivateAsync(linked.Id);
        await Service.DeactivateAsync(otherInactive.Id);

        var cut = await RenderEditAsync(boardCase);

        var options = cut.FindAll("#housingUnitId option").Select(option => option.TextContent).ToList();
        Assert.Equal(["Hele sameiet (ingen boenhet)", "Storgata 1A (inaktiv)", "Storgata 1C"], options);
        Assert.Equal(linked.Id.ToString(), cut.Find("#housingUnitId").GetAttribute("value"));
    }

    [Fact]
    public async Task Submit_ChangedTitle_IsStoredKeepsOtherValuesAndGoesToDetailPage()
    {
        var boardCase = await RegisterFullCaseAsync();
        var cut = await RenderEditAsync(boardCase);

        cut.Find("#title").Change("Ny tittel");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal($"/styresaker/{boardCase.Id}?status=endret", CurrentPath));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Ny tittel", stored!.Title);
        Assert.Equal(BoardCasePriority.High, stored.Priority);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.MeetingDate);
        Assert.Equal("Fiktive merknader.", stored.Notes);
    }

    [Fact]
    public async Task Submit_EditingOtherFieldsOfCaseLinkedToDeactivatedUnit_KeepsTheLink()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterFullCaseAsync(unit.Id);
        await Service.DeactivateAsync(unit.Id);
        var cut = await RenderEditAsync(boardCase);

        cut.Find("#title").Change("Ny tittel");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("status=endret", CurrentPath));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Ny tittel", stored!.Title);
        Assert.Equal(unit.Id, stored.HousingUnitId);
    }

    [Fact]
    public async Task Submit_RemovingTheHousingUnit_MakesTheCaseConcernTheWholeCooperative()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterFullCaseAsync(unit.Id);
        var cut = await RenderEditAsync(boardCase);

        cut.Find("#housingUnitId").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("status=endret", CurrentPath));
        Assert.Null((await BoardCases.GetByIdAsync(boardCase.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task Submit_InvalidMeetingDate_ShowsMessageAndKeepsTheStoredCaseUnchanged()
    {
        var boardCase = await RegisterFullCaseAsync();
        var cut = await RenderEditAsync(boardCase);

        cut.Find("#title").Change("Ny tittel");
        cut.Find("#meetingDate").Change("2200-01-01");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Møtedatoen må være mellom", cut.Find(".validation-message").TextContent));
        Assert.Equal("Ny tittel", cut.Find("#title").GetAttribute("value"));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Maling av fellesarealer", stored!.Title);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.MeetingDate);
    }

    [Fact]
    public async Task Submit_EmptyTitle_ShowsMessageAndKeepsStoredTitle()
    {
        var boardCase = await RegisterFullCaseAsync();
        var cut = await RenderEditAsync(boardCase);

        cut.Find("#title").Change("");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Tittelen må fylles ut.", cut.Find(".validation-message").TextContent));
        Assert.Equal("Maling av fellesarealer", (await BoardCases.GetByIdAsync(boardCase.Id))!.Title);
    }

    [Fact]
    public void Render_UnknownCase_ShowsNotFoundMessage()
    {
        var cut = Render<BoardCaseEdit>(parameters => parameters.Add(page => page.Id, 999));

        cut.WaitForAssertion(() => Assert.Contains("Fant ikke styresaken", cut.Markup));
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task Submit_DatabaseUnavailable_ShowsErrorAndKeepsInput()
    {
        var boardCase = await RegisterFullCaseAsync();
        var cut = await RenderEditAsync(boardCase);
        BoardCaseRepository.IsFailing = true;

        cut.Find("#title").Change("Ny tittel");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Ny tittel", cut.Find("#title").GetAttribute("value"));
    }

    [Fact]
    public void Render_DatabaseUnavailable_ShowsLoadError()
    {
        BoardCaseRepository.IsFailing = true;

        var cut = Render<BoardCaseEdit>(parameters => parameters.Add(page => page.Id, 1));

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente styresaken", cut.Find("[role=alert]").TextContent));
    }
}
