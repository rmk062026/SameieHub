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
        cut.WaitForElement("[data-testid=case-summary]");

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

    private static void OpenDecisionForm(IRenderedComponent<BoardCaseDetail> cut)
    {
        cut.FindByTestId("open-decision-form").Click();
    }

    private static string DetailValue(IRenderedComponent<BoardCaseDetail> cut, string label)
    {
        var item = cut.FindAll(".detail-item").First(element => element.QuerySelector("dt")!.TextContent == label);

        return item.QuerySelector("dd")!.TextContent.Trim();
    }

    private static string Feedback(IRenderedComponent<BoardCaseDetail> cut)
    {
        return cut.FindByTestId("action-feedback").TextContent;
    }

    private async Task<BoardCase> RegisterInProgressAsync()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.InProgress);

        return boardCase;
    }

    // Information

    [Fact]
    public async Task Render_FullyRegisteredCase_ShowsTheMostImportantInformationFirst()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterAsync(new BoardCaseDetails
        {
            Title = "Maling av fellesarealer",
            Description = "Fiktiv beskrivelse.",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(2026, 12, 3),
            HousingUnitId = unit.Id
        });
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal("Maling av fellesarealer", cut.FindByTestId("page-title").TextContent);
        var summary = cut.FindByTestId("case-summary");
        Assert.Contains("Ny", summary.QuerySelector(".status-new")!.TextContent);
        Assert.Equal("Høy prioritet", summary.QuerySelector("[data-testid=high-priority]")!.TextContent);
        Assert.Equal("03.12.2026", DetailValue(cut, "Registrert møtedato"));
        Assert.Equal("Storgata 1A", DetailValue(cut, "Gjelder"));
        Assert.Contains("Fiktiv beskrivelse.", cut.FindByTestId("case-description").TextContent);
        Assert.Contains("Fiktivt vedtak.", cut.FindByTestId("decision-box").TextContent);
    }

    [Fact]
    public async Task Render_Case_KeepsRarelyUsedInformationInAClosedDetailsSection()
    {
        var boardCase = await RegisterAsync(new BoardCaseDetails { Title = "Sak", Priority = BoardCasePriority.Low, Notes = "Fiktive merknader." });

        var cut = RenderDetail(boardCase.Id);

        var details = cut.Find("details.disclosure");
        Assert.False(details.HasAttribute("open"));
        Assert.Equal("Detaljer", details.QuerySelector(".disclosure-label")!.TextContent);
        Assert.Equal($"#{boardCase.Id}", DetailValue(cut, "Saksnummer"));
        Assert.Equal("Lav", DetailValue(cut, "Prioritet"));
        Assert.Equal("Fiktive merknader.", DetailValue(cut, "Merknader"));
        Assert.Equal("18.11.2026 11:30", DetailValue(cut, "Opprettet"));
        Assert.Equal("18.11.2026 11:30", DetailValue(cut, "Sist oppdatert"));
        Assert.False(cut.HasTestId("high-priority"));
    }

    [Fact]
    public async Task Render_MeetingDate_IsCalledARegisteredDateAndNeverAConfirmedMeeting()
    {
        var boardCase = await RegisterAsync(new BoardCaseDetails { Title = "Sak", MeetingDate = new DateOnly(2026, 12, 3) });

        var cut = RenderDetail(boardCase.Id);

        Assert.Contains(cut.FindAll(".detail-item dt"), term => term.TextContent == "Registrert møtedato");
        Assert.DoesNotContain("Neste styremøte", cut.Markup);
        Assert.DoesNotContain("bekreftet", cut.Markup);
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

        Assert.Equal("Hele sameiet", DetailValue(cut, "Gjelder"));
        Assert.Equal("Ikke registrert", DetailValue(cut, "Registrert møtedato"));
        Assert.Contains("Ikke registrert", cut.FindByTestId("case-description").TextContent);
        Assert.Contains("Ingen vedtak er registrert ennå", cut.FindByTestId("no-decision").TextContent);
        Assert.Empty(cut.FindAll("a[href^='/boenheter/']"));
    }

    [Fact]
    public async Task Render_Case_HasEditAndBackLinks()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal($"/styresaker/{boardCase.Id}/rediger", cut.FindByTestId("page-actions").QuerySelector("a")!.GetAttribute("href"));
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

        cut.WaitForElement("[data-testid=case-summary]");
    }

    [Theory]
    [InlineData("opprettet", "Styresaken er registrert.")]
    [InlineData("endret", "Endringene er lagret.")]
    public async Task Render_StatusInTheAddress_ShowsTheSuccessMessage(string status, string expectedMessage)
    {
        var boardCase = await RegisterAsync();
        Services.GetRequiredService<NavigationManager>().NavigateTo($"/styresaker/{boardCase.Id}?status={status}");

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal(expectedMessage, cut.FindByTestId("action-feedback").TextContent.Replace("Lukk", ""));
        Assert.Equal("status", cut.FindByTestId("action-feedback").GetAttribute("role"));
    }

    // The main action follows the natural next step

    [Fact]
    public async Task MainAction_NewCase_StartsTheTreatment()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        Assert.Equal("Start behandling", cut.FindByTestId("main-action").TextContent.Trim());

        cut.FindByTestId("main-action").Click();

        Assert.Equal(BoardCaseStatus.InProgress, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
        Assert.Contains("Saken er satt til «Under behandling».", Feedback(cut));
        Assert.Contains("Under behandling", cut.FindByTestId("case-summary").TextContent);
    }

    [Fact]
    public async Task MainAction_InProgressWithoutDecision_OpensTheDecisionForm_AndDoesNotChangeTheStatus()
    {
        var boardCase = await RegisterInProgressAsync();
        var cut = RenderDetail(boardCase.Id);
        Assert.Equal("Registrer vedtak", cut.FindByTestId("main-action").TextContent.Trim());
        Assert.False(cut.HasTestId("decision-form"));

        cut.FindByTestId("main-action").Click();

        Assert.True(cut.HasTestId("decision-form"));
        Assert.Equal(BoardCaseStatus.InProgress, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task MainAction_RegisterDecision_IsDisabledWhileTheFormIsOpen_AndTheSectionButtonIsNotRepeated()
    {
        var boardCase = await RegisterInProgressAsync();
        var cut = RenderDetail(boardCase.Id);
        Assert.False(cut.HasTestId("open-decision-form"));

        cut.FindByTestId("main-action").Click();

        Assert.True(cut.FindByTestId("main-action").HasAttribute("disabled"));
        Assert.True(cut.HasTestId("decision-form"));
    }

    [Fact]
    public async Task MainAction_InProgressWithDecision_MarksTheCaseAsDecided()
    {
        var boardCase = await RegisterInProgressAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        var cut = RenderDetail(boardCase.Id);
        Assert.Equal("Marker som vedtatt", cut.FindByTestId("main-action").TextContent.Trim());

        cut.FindByTestId("main-action").Click();

        Assert.Equal(BoardCaseStatus.Decided, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
        Assert.Contains("Saken er markert som vedtatt.", Feedback(cut));
    }

    [Fact]
    public async Task MainAction_DecidedCase_OffersToCloseItAndNothingClosesItAutomatically()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);
        var cut = RenderDetail(boardCase.Id);
        Assert.Equal("Avslutt saken", cut.FindByTestId("main-action").TextContent.Trim());
        Assert.Equal(BoardCaseStatus.Decided, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);

        cut.FindByTestId("main-action").Click();

        Assert.Equal(BoardCaseStatus.Closed, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
        Assert.Contains("Saken er avsluttet.", Feedback(cut));
    }

    [Fact]
    public async Task MainAction_ClosedCase_ReopensItAsInProgressAndSaysSo()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);
        var cut = RenderDetail(boardCase.Id);
        Assert.Equal("Gjenåpne saken", cut.FindByTestId("main-action").TextContent.Trim());

        cut.FindByTestId("main-action").Click();

        Assert.Equal(BoardCaseStatus.InProgress, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
        Assert.Contains("gjenåpnet og står nå som «Under behandling»", Feedback(cut));
    }

    // All statuses stay reachable, and the rules are kept

    [Fact]
    public async Task MoreActions_OfferEveryOtherStatus_AndCloseAfterUse()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        Assert.False(cut.HasTestId("set-status-New"));
        Assert.True(cut.HasTestId("set-status-InProgress"));
        Assert.True(cut.HasTestId("set-status-Decided"));
        Assert.True(cut.HasTestId("set-status-Closed"));

        cut.FindByTestId("set-status-InProgress").Click();

        Assert.Equal(BoardCaseStatus.InProgress, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
        Assert.Contains("Statusen er endret til «Under behandling».", Feedback(cut));
        Assert.False(cut.FindByTestId("more-menu").HasAttribute("open"));
        Assert.True(cut.HasTestId("set-status-New"));
    }

    [Fact]
    public async Task ChangeStatus_ToDecidedWithoutDecision_IsRejectedWithNorwegianMessageAtTheActionAndNothingChanges()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);

        cut.FindByTestId("set-status-Decided").Click();

        var alert = cut.Find("[role=alert]");
        Assert.Contains("før et vedtak er registrert", alert.TextContent);
        Assert.Equal(BoardCaseStatus.New, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task ChangeStatus_ToDecidedWithDecision_IsAllowedAndCaseIsNotClosed()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        var cut = RenderDetail(boardCase.Id);

        cut.FindByTestId("set-status-Decided").Click();

        Assert.Contains("Vedtatt", cut.FindByTestId("case-summary").TextContent);
        Assert.Equal(BoardCaseStatus.Decided, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task ChangeStatus_DecidedStatusWithoutDecisionDate_IsAllowed()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        var cut = RenderDetail(boardCase.Id);

        cut.FindByTestId("set-status-Decided").Click();

        Assert.Contains("Vedtatt", cut.FindByTestId("case-summary").TextContent);
    }

    [Fact]
    public async Task ChangeStatus_DatabaseUnavailable_ShowsErrorAndKeepsTheStatus()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        BoardCaseRepository.IsFailing = true;

        cut.FindByTestId("main-action").Click();

        Assert.Contains("Kunne ikke lagre endringen", cut.Find("[role=alert]").TextContent);
        BoardCaseRepository.IsFailing = false;
        Assert.Equal(BoardCaseStatus.New, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    // Tasks and the closing hint

    [Fact]
    public async Task Tasks_ShowProgressAndEachTaskWithAssigneeDueDateStatusAndLink()
    {
        var boardCase = await RegisterAsync();
        var done = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Ferdig", BoardCaseId = boardCase.Id, AssignedTo = "Lise Prøve" });
        await Tasks.ChangeStatusAsync(done.Id, TaskItemStatus.Completed);
        var open = await Tasks.RegisterAsync(new TaskItemDetails
        {
            Title = "Åpen",
            BoardCaseId = boardCase.Id,
            DueDate = new DateOnly(2026, 11, 10)
        });
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Annen sak sin oppgave" });

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal("(1 av 2 fullført)", cut.FindByTestId("task-progress").TextContent);
        var rows = cut.FindAll("[data-testid=case-task]");
        Assert.Equal(2, rows.Count);
        var openRow = rows.First(row => row.TextContent.Contains("Åpen"));
        Assert.Equal($"/oppgaver/{open.Id}", openRow.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("Ikke tildelt", openRow.QuerySelector("[data-testid=case-task-assignee]")!.TextContent);
        Assert.Contains("10.11.2026", openRow.TextContent);
        Assert.Equal("Forfalt", openRow.QuerySelector(".is-overdue")!.TextContent);
        var doneRow = rows.First(row => row.TextContent.Contains("Ferdig"));
        Assert.Equal("Lise Prøve", doneRow.QuerySelector("[data-testid=case-task-assignee]")!.TextContent);
        Assert.Contains("Fullført", doneRow.QuerySelector(".status-badge")!.TextContent);
        Assert.DoesNotContain("Annen sak sin oppgave", cut.Markup);
    }

    [Fact]
    public async Task Tasks_NewTaskLinkSuggestsTheCase_AndNoTasksIsExplained()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.Equal($"/oppgaver/ny?styresak={boardCase.Id}", cut.FindByTestId("new-task").GetAttribute("href"));
        Assert.Equal("Ny oppgave", cut.FindByTestId("new-task").TextContent);
        Assert.Contains("Ingen oppgaver er knyttet til denne saken", cut.FindByTestId("case-tasks").TextContent);
        Assert.False(cut.HasTestId("task-progress"));
        Assert.False(cut.HasTestId("close-hint"));
    }

    [Fact]
    public async Task Tasks_AllCompleted_ShowsADiscreetHintAndDoesNotChangeTheStatus()
    {
        var boardCase = await RegisterInProgressAsync();
        var task = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Ferdig", BoardCaseId = boardCase.Id });
        await Tasks.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var cut = RenderDetail(boardCase.Id);

        Assert.Contains("Vurder om saken kan avsluttes", cut.FindByTestId("close-hint").TextContent);
        Assert.Equal(BoardCaseStatus.InProgress, (await BoardCases.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task Tasks_NotAllCompletedOrCaseClosed_ShowsNoHint()
    {
        var boardCase = await RegisterAsync();
        var done = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Ferdig", BoardCaseId = boardCase.Id });
        await Tasks.ChangeStatusAsync(done.Id, TaskItemStatus.Completed);
        await Tasks.RegisterAsync(new TaskItemDetails { Title = "Åpen", BoardCaseId = boardCase.Id });

        Assert.False(RenderDetail(boardCase.Id).HasTestId("close-hint"));

        var open = (await Tasks.GetAllAsync()).First(task => task.Title == "Åpen");
        await Tasks.ChangeStatusAsync(open.Id, TaskItemStatus.Completed);
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);

        Assert.False(RenderDetail(boardCase.Id).HasTestId("close-hint"));
    }

    // Decision

    [Fact]
    public async Task Decision_NoDecision_ExplainsItAndOffersToRegisterOne()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.False(cut.HasTestId("decision-box"));
        Assert.False(cut.HasTestId("decision-form"));
        Assert.Contains("kan bare settes til «Vedtatt» når et vedtak er registrert", cut.FindByTestId("no-decision").TextContent);
        Assert.Equal("Registrer vedtak", cut.FindByTestId("open-decision-form").TextContent.Trim());
    }

    [Fact]
    public async Task Decision_Register_IsStoredWithoutChangingStatusAndShownAtTheDecision()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        var section = cut.FindByTestId("decision-section");
        Assert.Contains("Vedtaket er lagret.", section.QuerySelector("[data-testid=action-feedback]")!.TextContent);
        Assert.Contains("Fiktivt vedtak.", cut.FindByTestId("decision-box").TextContent);
        Assert.False(cut.HasTestId("decision-form"));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(BoardCaseStatus.New, stored.Status);
    }

    [Fact]
    public async Task Decision_EditForm_StartsWithTheRegisteredDecisionAndCanBeCancelledWithoutChanges()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);
        Assert.Equal("Endre vedtak", cut.FindByTestId("open-decision-form").TextContent.Trim());

        OpenDecisionForm(cut);

        Assert.Equal("Fiktivt vedtak.", cut.Find("#decision").GetAttribute("value"));
        Assert.Equal("2026-12-03", cut.Find("#decisionDate").GetAttribute("value"));

        cut.Find("#decision").Input("Ikke lagret.");
        ClickButton(cut, "Avbryt");

        Assert.False(cut.HasTestId("decision-form"));
        Assert.Equal("Fiktivt vedtak.", (await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
        Assert.Contains("Fiktivt vedtak.", cut.FindByTestId("decision-box").TextContent);
    }

    [Fact]
    public async Task Decision_Correct_ReplacesTheDecision()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Feil vedtak.", null);
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        cut.Find("#decision").Input("Riktig vedtak.");
        ClickButton(cut, "Lagre vedtak");

        Assert.Contains("Vedtaket er lagret.", Feedback(cut));
        Assert.Equal("Riktig vedtak.", (await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task Decision_Blank_ShowsMessageAtTheFormAndKeepsTheStoredDecision()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        cut.Find("#decision").Input("   ");
        ClickButton(cut, "Lagre vedtak");

        var alert = cut.FindByTestId("decision-section").QuerySelector("[role=alert]")!;
        Assert.Contains("Vedtaket må fylles ut", alert.TextContent);
        Assert.True(cut.HasTestId("decision-form"));
        Assert.Equal("Fiktivt vedtak.", (await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task Decision_Clear_WhenCaseIsNotDecided_RemovesIt()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Registrert ved en feil.", null);
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        ClickButton(cut, "Fjern vedtak");

        Assert.Contains("Vedtaket og vedtaksdatoen er fjernet.", Feedback(cut));
        Assert.Null((await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
        Assert.True(cut.HasTestId("no-decision"));
    }

    [Fact]
    public async Task Decision_Clear_WhenCaseIsDecided_IsRejectedWithNorwegianMessageAndKeepsTheDecision()
    {
        var boardCase = await RegisterAsync();
        await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        ClickButton(cut, "Fjern vedtak");

        Assert.Contains("kan ikke fjernes mens saken har status «Vedtatt»", cut.Find("[role=alert]").TextContent);
        Assert.Equal("Fiktivt vedtak.", (await BoardCases.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task Decision_DatabaseUnavailable_ShowsErrorAndKeepsTheFormOpen()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);
        BoardCaseRepository.IsFailing = true;

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        Assert.Contains("Kunne ikke lagre endringen", cut.Find("[role=alert]").TextContent);
        Assert.True(cut.HasTestId("decision-form"));
    }

    [Fact]
    public async Task Decision_CaseDeletedMeanwhile_ShowsNotFoundMessage()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);
        await BoardCases.DeleteAsync(boardCase.Id);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        Assert.Contains("finnes ikke lenger", cut.Find("[role=alert]").TextContent);
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

        var box = cut.FindByTestId("decision-box");
        Assert.Contains("Fiktivt vedtak.", box.TextContent);
        Assert.Contains("Vedtaksdato:", box.TextContent);
        Assert.Contains("03.12.2026", box.TextContent);
    }

    [Fact]
    public async Task DecisionDate_DecisionWithoutDate_ShowsThatTheDateIsNotRegistered()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(null);

        var cut = RenderDetail(boardCase.Id);

        Assert.Contains("Ikke registrert", cut.FindByTestId("decision-box").TextContent);
    }

    [Fact]
    public async Task DecisionDate_RegisterDecisionWithDate_IsStoredAndShown()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        cut.Find("#decisionDate").Change("2026-12-03");
        ClickButton(cut, "Registrer vedtak");

        Assert.Contains("03.12.2026", cut.FindByTestId("decision-box").TextContent);
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
        OpenDecisionForm(cut);

        cut.Find("#decision").Input("Fiktivt vedtak.");
        ClickButton(cut, "Registrer vedtak");

        Assert.Contains("Vedtaket er lagret.", Feedback(cut));
        Assert.Null((await BoardCases.GetByIdAsync(boardCase.Id))!.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_ChangeOnlyTheDate_IsStoredAndUpdatedAtChanges()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);
        Clock.Advance(TimeSpan.FromHours(3));

        cut.Find("#decisionDate").Change("2026-12-10");
        ClickButton(cut, "Lagre vedtak");

        Assert.Contains("10.12.2026", cut.FindByTestId("decision-box").TextContent);
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
        OpenDecisionForm(cut);
        Clock.Advance(TimeSpan.FromHours(3));

        ClickButton(cut, "Lagre vedtak");

        Assert.Contains("Vedtaket er lagret.", Feedback(cut));
        Assert.Equal(before, (await BoardCases.GetByIdAsync(boardCase.Id))!.UpdatedAt);
    }

    [Fact]
    public async Task DecisionDate_RemoveTheDate_KeepsTheDecisionText()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        cut.Find("#decisionDate").Change("");
        ClickButton(cut, "Lagre vedtak");

        Assert.Contains("Ikke registrert", cut.FindByTestId("decision-box").TextContent);
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Null(stored.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_InvalidDate_ShowsNorwegianMessageAndKeepsTheStoredDecision()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        cut.Find("#decision").Input("Nytt vedtak.");
        cut.Find("#decisionDate").Change("1999-01-01");
        ClickButton(cut, "Lagre vedtak");

        Assert.Equal("Vedtaksdatoen må være mellom 01.01.2000 og 31.12.2100.", cut.Find("[role=alert]").TextContent.Replace("Lukk", ""));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_DateWithoutDecisionText_IsRejected()
    {
        var boardCase = await RegisterAsync();
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        cut.Find("#decisionDate").Change("2026-12-03");
        ClickButton(cut, "Registrer vedtak");

        Assert.Contains("Vedtaket må fylles ut", cut.Find("[role=alert]").TextContent);
        Assert.Null((await BoardCases.GetByIdAsync(boardCase.Id))!.DecisionDate);
    }

    [Fact]
    public async Task DecisionDate_RemovingTheDecision_AlsoRemovesTheDate()
    {
        var boardCase = await RegisterCaseWithDecisionAsync(new DateOnly(2026, 12, 3));
        var cut = RenderDetail(boardCase.Id);
        OpenDecisionForm(cut);

        ClickButton(cut, "Fjern vedtak");

        Assert.Contains("Vedtaket og vedtaksdatoen er fjernet.", Feedback(cut));
        var stored = await BoardCases.GetByIdAsync(boardCase.Id);
        Assert.Null(stored!.Decision);
        Assert.Null(stored.DecisionDate);
    }

    // Deletion

    [Fact]
    public async Task Render_Case_ShowsDeleteButtonSeparatelyButNoConfirmationAndDeletesNothing()
    {
        var boardCase = await RegisterAsync();

        var cut = RenderDetail(boardCase.Id);

        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.Contains(cut.FindAll(".danger-zone button"), button => button.TextContent.Trim() == "Slett styresaken …");
        Assert.DoesNotContain("Slett", cut.FindByTestId("more-menu").TextContent);
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
}
