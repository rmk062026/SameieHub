using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.BoardCases;

namespace SameieHub.Tests.Web;

public class BoardCaseListTests : UiTestBase
{
    private async Task<BoardCase> RegisterAsync(string title, BoardCaseDetails? details = null)
    {
        return await BoardCases.RegisterAsync((details ?? new BoardCaseDetails { Title = title }) with { Title = title });
    }

    private async Task<BoardCase> RegisterWithStatusAsync(string title, BoardCaseStatus status)
    {
        var boardCase = await RegisterAsync(title);

        if (status == BoardCaseStatus.Decided)
        {
            await BoardCases.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        }

        if (status != BoardCaseStatus.New)
        {
            await BoardCases.ChangeStatusAsync(boardCase.Id, status);
        }

        return boardCase;
    }

    private IRenderedComponent<BoardCaseList> RenderList()
    {
        var cut = Render<BoardCaseList>();
        cut.WaitForElement("[data-testid=quick-tab-open], [data-testid=no-cases], [role=alert]");

        return cut;
    }

    private static IReadOnlyList<string> Titles(IRenderedComponent<BoardCaseList> cut)
    {
        return cut.FindAll("[data-testid=case-title]").Select(link => link.TextContent).ToList();
    }

    private static void Select(IRenderedComponent<BoardCaseList> cut, string label, string value)
    {
        cut.FindFieldByLabel(label).Change(value);
    }

    private static void Tab(IRenderedComponent<BoardCaseList> cut, string key)
    {
        cut.FindByTestId($"quick-tab-{key}").Click();
    }

    private static string TabText(IRenderedComponent<BoardCaseList> cut, string key)
    {
        return cut.FindByTestId($"quick-tab-{key}").TextContent.Trim();
    }

    // Empty, loading and errors

    [Fact]
    public void Render_NoCases_ShowsEmptyState()
    {
        var cut = RenderList();

        Assert.Contains("Ingen styresaker registrert ennå", cut.Markup);
        Assert.Empty(cut.FindAll("table"));
        Assert.Empty(cut.FindAll("[data-testid^=quick-tab]"));
    }

    [Fact]
    public void Render_DatabaseUnavailable_ShowsErrorAndRetryLoadsData()
    {
        BoardCaseRepository.IsFailing = true;
        var cut = Render<BoardCaseList>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente styresakene", cut.Find("[role=alert]").TextContent));

        BoardCaseRepository.IsFailing = false;
        cut.Find("[role=alert] button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Ingen styresaker registrert ennå", cut.Markup));
    }

    [Fact]
    public async Task Render_StatusDeletedInTheAddress_ShowsTheDeletedMessage()
    {
        await RegisterAsync("Sak A");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/styresaker?status=slettet");

        var cut = RenderList();

        Assert.Equal("Styresaken er slettet permanent.", cut.Find(".alert-success").TextContent);
    }

    [Fact]
    public void Render_HasNewCaseAsThePrimaryActionInTheHeader()
    {
        var cut = RenderList();

        Assert.Equal("Styresaker", cut.FindByTestId("page-title").TextContent);
        Assert.Equal("/styresaker/ny", cut.Find("a.btn-primary").GetAttribute("href"));
    }

    // Quick views

    [Fact]
    public async Task Render_ShowsCountsOnTheQuickViews_AndOpenMeansNotClosed()
    {
        await RegisterWithStatusAsync("Ny", BoardCaseStatus.New);
        await RegisterWithStatusAsync("Under behandling", BoardCaseStatus.InProgress);
        await RegisterWithStatusAsync("Vedtatt", BoardCaseStatus.Decided);
        await RegisterWithStatusAsync("Avsluttet", BoardCaseStatus.Closed);

        var cut = RenderList();

        Assert.Equal("Åpne 3", TabText(cut, "open"));
        Assert.Equal("Vedtatt 1", TabText(cut, "decided"));
        Assert.Equal("Avsluttet 1", TabText(cut, "closed"));
        Assert.Equal("true", cut.FindByTestId("quick-tab-open").GetAttribute("aria-pressed"));
        Assert.Equal(3, Titles(cut).Count);
        Assert.DoesNotContain("Avsluttet", Titles(cut));
        Assert.Contains("ikke er avsluttet", cut.FindByTestId("tab-explanation").TextContent);
    }

    [Fact]
    public async Task QuickView_DecidedAndClosed_ShowOnlyCasesWithThatStatus()
    {
        await RegisterWithStatusAsync("Åpen", BoardCaseStatus.InProgress);
        await RegisterWithStatusAsync("Vedtatt sak", BoardCaseStatus.Decided);
        await RegisterWithStatusAsync("Avsluttet sak", BoardCaseStatus.Closed);
        var cut = RenderList();

        Tab(cut, "decided");
        Assert.Equal(["Vedtatt sak"], Titles(cut));
        Assert.Equal("true", cut.FindByTestId("quick-tab-decided").GetAttribute("aria-pressed"));

        Tab(cut, "closed");
        Assert.Equal(["Avsluttet sak"], Titles(cut));
    }

    [Fact]
    public async Task Count_OpenCasesMatchesTheOversiktNumber()
    {
        await RegisterWithStatusAsync("Ny", BoardCaseStatus.New);
        await RegisterWithStatusAsync("Vedtatt", BoardCaseStatus.Decided);
        await RegisterWithStatusAsync("Avsluttet", BoardCaseStatus.Closed);
        var list = RenderList();

        var dashboard = Render<SameieHub.Web.Components.Pages.Dashboard>();
        dashboard.WaitForElement("[data-testid=status-summary]");
        var card = dashboard.FindAll(".stat-card").First(element => element.QuerySelector(".stat-label")!.TextContent == "Åpne styresaker");

        Assert.Equal($"Åpne {card.QuerySelector(".stat-value")!.TextContent}", TabText(list, "open"));
    }

    // Rows

    [Fact]
    public async Task Render_Case_ShowsTitleStatusMeetingDateScopeAndHighPriority()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterAsync("Maling av porter", new BoardCaseDetails
        {
            Title = "Maling av porter",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(2026, 12, 3),
            HousingUnitId = unit.Id
        });
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.InProgress);

        var cut = RenderList();

        var row = cut.FindByTestId("case-row");
        var title = row.QuerySelector("[data-testid=case-title]")!;
        Assert.Equal("Maling av porter", title.TextContent);
        Assert.Equal($"/styresaker/{boardCase.Id}", title.GetAttribute("href"));
        Assert.Equal("Høy prioritet", row.QuerySelector("[data-testid=high-priority]")!.TextContent);
        Assert.Equal("03.12.2026", row.QuerySelector("[data-testid=case-meeting-date]")!.TextContent.Trim());
        Assert.Contains("Under behandling", row.TextContent);
        var scope = row.QuerySelector("[data-testid=case-scope] a")!;
        Assert.Equal("Storgata 1A", scope.TextContent);
        Assert.Equal($"/boenheter/{unit.Id}", scope.GetAttribute("href"));
    }

    [Fact]
    public async Task Render_Case_DoesNotShowTimestampsNumberNormalPriorityOrActionButtons()
    {
        var boardCase = await RegisterAsync("Vanlig sak");

        var cut = RenderList();

        var row = cut.FindByTestId("case-row");
        Assert.DoesNotContain("18.11.2026", row.TextContent);
        Assert.DoesNotContain($"#{boardCase.Id}", row.TextContent);
        Assert.DoesNotContain("Normal", row.TextContent);
        Assert.False(cut.HasTestId("high-priority"));
        Assert.Empty(row.QuerySelectorAll("button"));
        Assert.Equal([$"/styresaker/{boardCase.Id}"], row.QuerySelectorAll("a").Select(link => link.GetAttribute("href")).ToList());
        Assert.Equal(["Sak", "Møtedato", "Status"], cut.FindAll("thead th").Select(header => header.TextContent).ToList());
    }

    [Fact]
    public async Task Render_CaseWithoutMeetingDateOrUnit_SaysSo()
    {
        await RegisterAsync("Vedtekter");

        var cut = RenderList();

        var row = cut.FindByTestId("case-row");
        Assert.Equal("Ingen møtedato", row.QuerySelector("[data-testid=case-meeting-date]")!.TextContent.Trim());
        Assert.Equal("Hele sameiet", row.QuerySelector("[data-testid=case-scope]")!.TextContent.Trim());
    }

    [Fact]
    public async Task Render_CaseLinkedToDeactivatedUnit_StillShowsTheUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await RegisterAsync("Maling", new BoardCaseDetails { Title = "Maling", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = RenderList();

        Assert.Equal("Storgata 1A", cut.FindByTestId("case-scope").TextContent.Trim());
    }

    [Fact]
    public async Task Render_TableCells_HaveLabelsSoTheRowCanBecomeACardOnASmallScreen()
    {
        await RegisterAsync("Sak");

        var cut = RenderList();

        var labels = cut.FindByTestId("case-row").QuerySelectorAll("td").Select(cell => cell.GetAttribute("data-label")).ToList();
        Assert.Equal(["Sak", "Møtedato", "Status"], labels);
        Assert.Contains("table-cards", cut.FindByTestId("case-table").ClassName);
    }

    // Sorting

    [Fact]
    public async Task Sort_OpenCasesByDefault_NearestMeetingDateFirstAndCasesWithoutDateLast()
    {
        await RegisterAsync("Uten dato");
        await RegisterAsync("Senere", new BoardCaseDetails { Title = "Senere", MeetingDate = new DateOnly(2026, 12, 20) });
        await RegisterAsync("Nærmest", new BoardCaseDetails { Title = "Nærmest", MeetingDate = new DateOnly(2026, 11, 20) });

        var cut = RenderList();

        Assert.Equal(["Nærmest", "Senere", "Uten dato"], Titles(cut));
        Assert.Equal("MeetingDate", cut.FindFieldByLabel("Sortering").GetAttribute("value"));
    }

    [Fact]
    public async Task Sort_OpenCasesByDefault_PassedMeetingDatesComeLast_ButNothingIsHidden()
    {
        await RegisterAsync("Gammel", new BoardCaseDetails { Title = "Gammel", MeetingDate = new DateOnly(2026, 9, 1) });
        await RegisterAsync("Nylig passert", new BoardCaseDetails { Title = "Nylig passert", MeetingDate = new DateOnly(2026, 11, 10) });
        await RegisterAsync("Uten dato");
        await RegisterAsync("I dag", new BoardCaseDetails { Title = "I dag", MeetingDate = new DateOnly(2026, 11, 18) });
        await RegisterAsync("Senere", new BoardCaseDetails { Title = "Senere", MeetingDate = new DateOnly(2026, 12, 20) });

        var cut = RenderList();

        Assert.Equal(["I dag", "Senere", "Uten dato", "Nylig passert", "Gammel"], Titles(cut));
    }

    [Fact]
    public async Task Sort_ChoosingMeetingDateYourself_KeepsTheDomainOrderWithTheOldestFirst()
    {
        await RegisterAsync("Gammel", new BoardCaseDetails { Title = "Gammel", MeetingDate = new DateOnly(2026, 9, 1) });
        await RegisterAsync("Senere", new BoardCaseDetails { Title = "Senere", MeetingDate = new DateOnly(2026, 12, 20) });
        var cut = RenderList();

        Select(cut, "Sortering", "MeetingDate");

        Assert.Equal(["Gammel", "Senere"], Titles(cut));
    }

    [Fact]
    public async Task Sort_OtherViewsByDefault_MostRecentlyUpdatedFirst()
    {
        var first = await RegisterWithStatusAsync("Eldst oppdatert", BoardCaseStatus.Closed);
        Clock.Advance(TimeSpan.FromHours(1));
        await RegisterWithStatusAsync("Sist oppdatert", BoardCaseStatus.Closed);
        var cut = RenderList();

        Tab(cut, "closed");

        Assert.Equal(["Sist oppdatert", "Eldst oppdatert"], Titles(cut));
        Assert.Equal("RecentlyUpdated", cut.FindFieldByLabel("Sortering").GetAttribute("value"));
        Assert.NotNull(first);
    }

    [Fact]
    public async Task Sort_ByTitleOrNewest_ChangesTheOrder_AndTheChoiceIsKeptWhenTheViewChanges()
    {
        await RegisterAsync("Vedtekter");
        Clock.Advance(TimeSpan.FromHours(1));
        await RegisterAsync("Brannvern");
        var cut = RenderList();

        Select(cut, "Sortering", "Title");
        Assert.Equal(["Brannvern", "Vedtekter"], Titles(cut));

        Select(cut, "Sortering", "Oldest");
        Assert.Equal(["Vedtekter", "Brannvern"], Titles(cut));

        Tab(cut, "decided");
        Tab(cut, "open");
        Assert.Equal(["Vedtekter", "Brannvern"], Titles(cut));
    }

    // Search and filters

    [Theory]
    [InlineData("porter")]
    [InlineData("FASADEN")]
    [InlineData("#2")]
    public async Task Search_FindsCaseByTitleDescriptionOrCaseNumber(string searchText)
    {
        await RegisterAsync("Vedtekter");
        await RegisterAsync("Maling av porter", new BoardCaseDetails { Title = "Maling av porter", Description = "Gjelder fasaden" });
        var cut = RenderList();

        cut.FindByTestId("case-search").Input(searchText);

        Assert.Equal(["Maling av porter"], Titles(cut));
        Assert.Contains("Søk:", cut.FindByTestId("active-filters").TextContent);
    }

    [Fact]
    public async Task Search_IsCombinedWithTheSelectedQuickView()
    {
        await RegisterWithStatusAsync("Maling åpen", BoardCaseStatus.InProgress);
        await RegisterWithStatusAsync("Maling avsluttet", BoardCaseStatus.Closed);
        await RegisterAsync("Noe annet");
        var cut = RenderList();

        cut.FindByTestId("case-search").Input("maling");
        Assert.Equal(["Maling åpen"], Titles(cut));

        Tab(cut, "closed");
        Assert.Equal(["Maling avsluttet"], Titles(cut));
    }

    [Fact]
    public async Task Filter_AnyStatusCanBeChosen_ReplacesTheQuickViewAndShowsAnActiveFilter()
    {
        await RegisterWithStatusAsync("Ny", BoardCaseStatus.New);
        await RegisterWithStatusAsync("Under arbeid", BoardCaseStatus.InProgress);
        await RegisterWithStatusAsync("Avsluttet", BoardCaseStatus.Closed);
        var cut = RenderList();

        Select(cut, "Status", "InProgress");
        Assert.Equal(["Under arbeid"], Titles(cut));
        Assert.Contains("Status: Under behandling", cut.FindByTestId("active-filters").TextContent);
        Assert.Equal("false", cut.FindByTestId("quick-tab-open").GetAttribute("aria-pressed"));

        Select(cut, "Status", "all");
        Assert.Equal(3, Titles(cut).Count);

        Select(cut, "Status", "");
        Assert.Equal(2, Titles(cut).Count);
        Assert.Equal("true", cut.FindByTestId("quick-tab-open").GetAttribute("aria-pressed"));
    }

    [Fact]
    public async Task Filter_Priority_ShowsOnlyCasesWithThatPriority_AndCanBeRemoved()
    {
        await RegisterAsync("Viktig", new BoardCaseDetails { Title = "Viktig", Priority = BoardCasePriority.High });
        await RegisterAsync("Vanlig");
        var cut = RenderList();

        Select(cut, "Prioritet", "High");
        Assert.Equal(["Viktig"], Titles(cut));

        cut.Find("[aria-label='Fjern filteret Prioritet: Høy']").Click();
        Assert.Equal(2, Titles(cut).Count);
        Assert.False(cut.HasTestId("active-filters"));
    }

    [Fact]
    public async Task Filter_HousingUnit_ShowsCasesForThatUnitOrOnlyTheWholeCooperative()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await RegisterAsync("For boenhet", new BoardCaseDetails { Title = "For boenhet", HousingUnitId = unit.Id });
        await RegisterAsync("For hele sameiet");
        var cut = RenderList();

        Select(cut, "Boenhet", unit.Id.ToString());
        Assert.Equal(["For boenhet"], Titles(cut));
        Assert.Contains("Boenhet: Storgata 1A", cut.FindByTestId("active-filters").TextContent);

        Select(cut, "Boenhet", "none");
        Assert.Equal(["For hele sameiet"], Titles(cut));
        Assert.Contains("Boenhet: Hele sameiet", cut.FindByTestId("active-filters").TextContent);
    }

    [Fact]
    public async Task Filter_MoreFiltersStartClosedButTellWhatIsInside()
    {
        await RegisterAsync("Sak");

        var cut = RenderList();

        var details = cut.Find("details.disclosure");
        Assert.False(details.HasAttribute("open"));
        Assert.Contains("Flere filtre", details.QuerySelector("summary")!.TextContent);
        Assert.Contains("Sortering", details.QuerySelector(".disclosure-hint")!.TextContent);
    }

    // Empty results

    [Fact]
    public async Task Search_NoMatch_ExplainsThatCasesExistAndResetShowsCasesAgain()
    {
        await RegisterAsync("Vedtekter");
        var cut = RenderList();

        cut.FindByTestId("case-search").Input("finnes-ikke");

        var empty = cut.FindByTestId("empty-list").TextContent;
        Assert.Contains("Ingen styresaker funnet", empty);
        Assert.Contains("Sameiet har 1 styresak i alt", empty);
        Assert.Contains("Søk: «finnes-ikke»", cut.FindByTestId("active-filters").TextContent);

        cut.ClickButton("Nullstill søk og filtre");

        Assert.Single(Titles(cut));
        Assert.False(cut.HasTestId("active-filters"));
    }

    [Fact]
    public async Task Render_NoOpenCases_ExplainsAndOffersToShowClosedOnes()
    {
        await RegisterWithStatusAsync("Avsluttet", BoardCaseStatus.Closed);

        var cut = RenderList();

        Assert.Contains("Ingen styresaker funnet", cut.Markup);
        cut.ClickButton("Vis også avsluttede");

        Assert.Equal(["Avsluttet"], Titles(cut));
        Assert.Contains("Status: Alle", cut.FindByTestId("active-filters").TextContent);
    }

    [Fact]
    public async Task Render_AlwaysTellsHowManyOfAllCasesAreShown()
    {
        await RegisterWithStatusAsync("Åpen", BoardCaseStatus.New);
        await RegisterWithStatusAsync("Avsluttet", BoardCaseStatus.Closed);

        var cut = RenderList();

        Assert.Contains("Viser 1 av 2 styresaker", cut.FindByTestId("list-summary").TextContent);
    }
}
