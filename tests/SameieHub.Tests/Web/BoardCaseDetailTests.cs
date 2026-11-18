using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.BoardCases;

namespace SameieHub.Tests.Web;

public class BoardCaseDetailTests : UiTestBase
{
    private IRenderedComponent<BoardCaseDetail> RenderDetail(int id)
    {
        var cut = Render<BoardCaseDetail>(parameters => parameters.Add(page => page.Id, id));
        cut.WaitForElement(".detail-grid");

        return cut;
    }

    private async Task<BoardCase> RegisterAsync(BoardCaseDetails? details = null)
    {
        return await BoardCases.RegisterAsync(details ?? new BoardCaseDetails { Title = "Maling av fellesarealer" });
    }

    private static void ClickButton(IRenderedComponent<BoardCaseDetail> cut, string text)
    {
        cut.FindAll("button").First(button => button.TextContent.Trim() == text).Click();
    }

    // Information

    [Fact]
    public async Task Render_FullyRegisteredCase_ShowsAllInformationInNorwegian()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterAsync(new BoardCaseDetails
        {
            Title = "Maling av fellesarealer",
            Description = "Fiktiv beskrivelse.",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(2026, 12, 3),
            HousingUnitId = unit.Id,
            Notes = "Fiktive merknader."
        });
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);

        var cut = RenderDetail(boardCase.Id);

        var text = cut.Markup;
        Assert.Contains("Maling av fellesarealer", cut.Find("h1").TextContent);
        Assert.Contains($"Sak #{boardCase.Id}", text);
        Assert.Contains("Ny", cut.Find(".detail-meta .status-new").TextContent);
        Assert.Contains("Høy", cut.Find(".detail-meta .priority-high").TextContent);
        Assert.Contains("Fiktiv beskrivelse.", text);
        Assert.Contains("03.12.2026", text);
        Assert.Contains("18.11.2026 11:30", text);
        Assert.Contains("Fiktive merknader.", text);
        Assert.Equal("Fiktivt vedtak.", cut.Find("#decision").GetAttribute("value"));
    }

    [Fact]
    public async Task Render_CaseLinkedToUnit_ShowsClickableLinkToTheUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterAsync(new BoardCaseDetails { Title = "Maling", HousingUnitId = unit.Id });

        var cut = RenderDetail(boardCase.Id);

        var link = cut.Find($"a[href='/boenheter/{unit.Id}']");
        Assert.Equal("Storgata 1A", link.TextContent);
    }

    [Fact]
    public async Task Render_CaseLinkedToDeactivatedUnit_StillShowsTheUnitMarkedInactive()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterAsync(new BoardCaseDetails { Title = "Maling", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal("Storgata 1A", cut.Find($"a[href='/boenheter/{unit.Id}']").TextContent);
        Assert.Contains("(inaktiv)", cut.Markup);
    }

    [Fact]
    public async Task Render_CaseWithoutUnitAndOptionalInformation_ShowsWholeCooperativeAndMissingValuesTidily()
    {
        var boardCase = await RegisterAsync(new BoardCaseDetails { Title = "Vedtekter" });

        var cut = RenderDetail(boardCase.Id);

        Assert.Contains("Hele sameiet", cut.Markup);
        Assert.Contains("Ikke registrert", cut.Markup);
        Assert.Contains("Ingen vedtak er registrert.", cut.Markup);
        Assert.Empty(cut.FindAll("a[href^='/boenheter/']"));
    }

    [Fact]
    public async Task Render_Case_HasEditAndBackLinks()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal($"/styresaker/{boardCase.Id}/rediger", cut.Find(".page-actions a").GetAttribute("href"));
        Assert.Equal("/styresaker", cut.Find("a.back-link").GetAttribute("href"));
    }

    [Fact]
    public void Render_UnknownCase_ShowsNotFoundMessage()
    {
        var cut = Render<BoardCaseDetail>(parameters => parameters.Add(page => page.Id, 999));

        cut.WaitForAssertion(() => Assert.Contains("Fant ikke styresaken", cut.Markup));
    }

    [Fact]
    public async Task Render_DatabaseUnavailable_ShowsErrorAndRetryLoadsTheCase()
    {
        var boardCase = await RegisterAsync();
        BoardCaseRepository.IsFailing = true;
        var cut = Render<BoardCaseDetail>(parameters => parameters.Add(page => page.Id, boardCase.Id));
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente styresaken", cut.Find("[role=alert]").TextContent));

        BoardCaseRepository.IsFailing = false;
        cut.Find("[role=alert] button").Click();

        cut.WaitForElement(".detail-grid");
    }

    // Status

    [Fact]
    public async Task ChangeStatus_ToInProgress_ShowsNewStatusAndStoresIt()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#newStatus").Change("InProgress");
        ClickButton(cut, "Endre status");

        cut.WaitForAssertion(() => Assert.Contains("Under behandling", cut.Find(".detail-meta .status-badge").TextContent));
        Assert.Contains("Statusen er endret.", cut.Find("[role=status]").TextContent);
        Assert.Equal(BoardCaseStatus.InProgress, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task ChangeStatus_ToDecidedWithoutDecision_IsRejectedWithNorwegianMessageAndNothingChanges()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#newStatus").Change("Decided");
        ClickButton(cut, "Endre status");

        cut.WaitForAssertion(() =>
            Assert.Contains("før et vedtak er registrert", cut.Find("[role=alert]").TextContent));
        Assert.Equal(BoardCaseStatus.New, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task ChangeStatus_ToDecidedWithDecision_IsAllowedAndCaseIsNotClosed()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#newStatus").Change("Decided");
        ClickButton(cut, "Endre status");

        cut.WaitForAssertion(() => Assert.Contains("Vedtatt", cut.Find(".detail-meta .status-badge").TextContent));
        Assert.Equal(BoardCaseStatus.Decided, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task Reopen_ClosedCase_BecomesInProgress()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);
        var cut = RenderDetail(boardCase.Id);

        ClickButton(cut, "Gjenåpne saken");

        cut.WaitForAssertion(() => Assert.Contains("Under behandling", cut.Find(".detail-meta .status-badge").TextContent));
        Assert.Equal(BoardCaseStatus.InProgress, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task Render_CaseNotClosed_HasNoReopenButton()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() == "Gjenåpne saken");
    }

    // Decision

    [Fact]
    public async Task Decision_Register_IsStoredWithoutChangingStatus()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket er lagret.", cut.Find("[role=status]").TextContent));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(BoardCaseStatus.New, stored.Status);
    }

    [Fact]
    public async Task Decision_Correct_ReplacesTheDecision()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Feil vedtak.", null);
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decision").Input("Riktig vedtak.");
        ClickButton(cut, "Lagre vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket er lagret.", cut.Find("[role=status]").TextContent));
        Assert.Equal("Riktig vedtak.", (await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task Decision_Blank_ShowsMessageAndKeepsTheStoredDecision()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decision").Input("   ");
        ClickButton(cut, "Lagre vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket må fylles ut", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Fiktivt vedtak.", (await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task Decision_Clear_WhenCaseIsNotDecided_RemovesIt()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Registrert ved en feil.", null);
        var cut = RenderDetail(boardCase.Id);

        ClickButton(cut, "Fjern vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket og vedtaksdatoen er fjernet.", cut.Find("[role=status]").TextContent));
        Assert.Null((await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
        Assert.Contains("Ingen vedtak er registrert.", cut.Markup);
    }

    [Fact]
    public async Task Decision_Clear_WhenCaseIsDecided_IsRejectedWithNorwegianMessageAndKeepsTheDecision()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);
        var cut = RenderDetail(boardCase.Id);

        ClickButton(cut, "Fjern vedtak");

        cut.WaitForAssertion(() =>
            Assert.Contains("kan ikke fjernes mens saken har status «Vedtatt»", cut.Find("[role=alert]").TextContent));
        Assert.Equal("Fiktivt vedtak.", (await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task Actions_DatabaseUnavailable_ShowsErrorAndKeepsTheCase()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        BoardCaseRepository.IsFailing = true;

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke lagre endringen", cut.Find("[role=alert]").TextContent));
    }

    [Fact]
    public async Task Actions_CaseDeletedMeanwhile_ShowsNotFoundMessage()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        await BoardCases.DeleteAsync(boardCase.Id);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        cut.WaitForAssertion(() => Assert.Contains("finnes ikke lenger", cut.Find("[role=alert]").TextContent));
    }

    // Deletion

    [Fact]
    public async Task Render_Case_ShowsDeleteButtonButNoConfirmationAndDeletesNothing()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.NotNull(await BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task Delete_ClickingDelete_AsksForConfirmationWithTitleAndWarningAndDeletesNothing()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        ClickButton(cut, "Slett styresaken …");

        var dialog = cut.Find("[role=alertdialog]");
        Assert.Contains("Maling av fellesarealer", dialog.TextContent);
        Assert.Contains("Dette kan ikke angres", dialog.TextContent);
        Assert.NotNull(await BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task Delete_Cancel_ClosesConfirmationAndDeletesNothing()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        ClickButton(cut, "Slett styresaken …");

        ClickButton(cut, "Avbryt");

        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.NotNull(await BoardCases.GetByIdAsync(boardCase.Id));
        Assert.DoesNotContain("status=slettet", CurrentPath);
    }

    [Fact]
    public async Task Delete_Confirm_DeletesTheCaseAndGoesToListWithMessage()
    {
        var boardCase = await RegisterAsync();
        var other = await RegisterAsync(new BoardCaseDetails { Title = "Annen sak" });
        var cut = RenderDetail(boardCase.Id);
        ClickButton(cut, "Slett styresaken …");

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Equal("/styresaker?status=slettet", CurrentPath));
        Assert.Null(await BoardCases.GetByIdAsync(boardCase.Id));
        Assert.NotNull(await BoardCases.GetByIdAsync(other.Id));
    }

    [Fact]
    public async Task Delete_CaseInUse_ShowsUnderstandableMessageAndKeepsTheCase()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        BoardCaseRepository.IsDeletionBlocked = true;
        ClickButton(cut, "Slett styresaken …");

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("knyttet til oppgaver", cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.DoesNotContain("status=slettet", CurrentPath);
        BoardCaseRepository.IsDeletionBlocked = false;
        Assert.NotNull(await BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task Delete_DatabaseUnavailable_ShowsErrorAndKeepsTheCase()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        ClickButton(cut, "Slett styresaken …");
        BoardCaseRepository.IsFailing = true;

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke slette styresaken", cut.Find("[role=alert]").TextContent));
        BoardCaseRepository.IsFailing = false;
        Assert.NotNull(await BoardCases.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task Delete_CaseAlreadyDeleted_ShowsNotFoundMessage()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        ClickButton(cut, "Slett styresaken …");
        await BoardCases.DeleteAsync(boardCase.Id);

        ClickButton(cut, "Ja, slett permanent");

        cut.WaitForAssertion(() => Assert.Contains("finnes ikke lenger", cut.Find("[role=alert]").TextContent));
    }

    // Decision date

    private async Task<BoardCase> RegisterCaseWithDecisionAsync(DateOnly? decisionDate)
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", decisionDate);

        return boardCase;
    }

    [Fact]
    public async Task DecisionDate_Registered_IsShownInNorwegianFormatTogetherWithTheDecisionText()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));

        var cut = RenderDetail(boardCase.Id);

        var box = cut.Find(".decision-box");
        Assert.Contains("Fiktivt vedtak.", box.TextContent);
        Assert.Contains("Vedtaksdato:", box.TextContent);
        Assert.Contains("03.12.2026", box.TextContent);
        Assert.Equal("2026-12-03", cut.Find("#decisionDate").GetAttribute("value"));
    }

    [Fact]
    public async Task DecisionDate_DecisionWithoutDate_ShowsThatTheDateIsNotRegistered()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(null);

        var cut = RenderDetail(boardCase.Id);

        Assert.Contains("Ikke registrert", cut.Find(".decision-box").TextContent);
    }

    [Fact]
    public async Task DecisionDate_NoDecision_ShowsNoDecisionBox()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.Empty(cut.FindAll(".decision-box"));
        Assert.Contains("Ingen vedtak er registrert.", cut.Markup);
    }

    [Fact]
    public async Task DecisionDate_RegisterDecisionWithDate_IsStoredAndShown()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        cut.Find("#decisionDate").Change("2026-12-03");
        ClickButton(cut, "Registrer vedtak");

        cut.WaitForAssertion(() => Assert.Contains("03.12.2026", cut.Find(".decision-box").TextContent));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.DecisionDate);
        Assert.Equal(BoardCaseStatus.New, stored.Status);
    }

    [Fact]
    public async Task DecisionDate_RegisterDecisionWithoutDate_IsStoredWithNoDate()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket er lagret.", cut.Find("[role=status]").TextContent));
        Assert.Null((await BoardCases.GetByIdAsync(boardCase.Id))!.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_ChangeOnlyTheDate_IsStoredAndUpdatedAtChanges()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);
        Clock.Advance(TimeSpan.FromHours(3));

        cut.Find("#decisionDate").Change("2026-12-10");
        ClickButton(cut, "Lagre vedtak");

        cut.WaitForAssertion(() => Assert.Contains("10.12.2026", cut.Find(".decision-box").TextContent));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.True(stored.UpdatedAt > stored.CreatedAt);
    }

    [Fact]
    public async Task DecisionDate_SaveWithoutChanges_KeepsUpdatedAt()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var before = (await BoardCases.GetByIdAsync(boardCase.Id))!.UpdatedAt;
        var cut = RenderDetail(boardCase.Id);
        Clock.Advance(TimeSpan.FromHours(3));

        ClickButton(cut, "Lagre vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket er lagret.", cut.Find("[role=status]").TextContent));
        Assert.Equal(before, (await BoardCases.GetByIdAsync(boardCase.Id))!.UpdatedAt);
    }

    [Fact]
    public async Task DecisionDate_RemoveTheDate_KeepsTheDecisionText()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decisionDate").Change("");
        ClickButton(cut, "Lagre vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Ikke registrert", cut.Find(".decision-box").TextContent));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Null(stored.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_InvalidDate_ShowsNorwegianMessageAndKeepsTheStoredDecision()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decision").Input("Nytt vedtak.");
        cut.Find("#decisionDate").Change("1999-01-01");
        ClickButton(cut, "Lagre vedtak");

        cut.WaitForAssertion(() =>
            Assert.Equal("Vedtaksdatoen må være mellom 01.01.2000 og 31.12.2100.", cut.Find("[role=alert]").TextContent));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_DateWithoutDecisionText_IsRejected()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#decisionDate").Change("2026-12-03");
        ClickButton(cut, "Registrer vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket må fylles ut", cut.Find("[role=alert]").TextContent));
        Assert.Null((await BoardCases.GetByIdAsync(boardCase.Id))!.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_RemovingTheDecision_AlsoRemovesTheDate()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);

        ClickButton(cut, "Fjern vedtak");

        cut.WaitForAssertion(() => Assert.Contains("Vedtaket og vedtaksdatoen er fjernet.", cut.Find("[role=status]").TextContent));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Null(stored!.Decision);
        Assert.Null(stored.DecisionDate);
        Assert.Equal("", cut.Find("#decisionDate").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task DecisionDate_DecidedStatusWithoutDate_IsAllowed()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(null);
        var cut = RenderDetail(boardCase.Id);

        cut.Find("#newStatus").Change("Decided");
        ClickButton(cut, "Endre status");

        cut.WaitForAssertion(() => Assert.Contains("Vedtatt", cut.Find(".detail-meta .status-badge").TextContent));
    }

    // Messages after saving, passed in the address

    [Theory]
    [InlineData("opprettet", "Styresaken er registrert.")]
    [InlineData("endret", "Endringene er lagret.")]
    public async Task Render_StatusInTheAddress_ShowsTheSuccessMessage(string status, string expectedMessage)
    {
        var boardCase = await RegisterAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/styresaker/{boardCase.Id}?status={status}");

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal(expectedMessage, cut.Find("[role=status]").TextContent);
    }
}
