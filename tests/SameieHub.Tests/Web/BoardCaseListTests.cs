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

    private static IReadOnlyList<string> RowTexts(IRenderedComponent<BoardCaseList> cut)
    {
        return cut.FindAll("tbody tr").Select(row => row.TextContent).ToList();
    }

    private static void Select(IRenderedComponent<BoardCaseList> cut, string label, string value)
    {
        cut.Find($"select[aria-label='{label}']").Change(value);
    }

    [Fact]
    public void Render_NoCases_ShowsEmptyState()
    {
        var cut = Render<BoardCaseList>();

        cut.WaitForAssertion(() => Assert.Contains("Ingen styresaker registrert ennå", cut.Markup));
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public async Task Render_Cases_ShowsNewestFirstByDefault()
    {
        await RegisterAsync("Eldst");
        Clock.Advance(TimeSpan.FromHours(1));
        await RegisterAsync("Nyest");

        var cut = Render<BoardCaseList>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr").Count));
        var rows = RowTexts(cut);
        Assert.Contains("Nyest", rows[0]);
        Assert.Contains("Eldst", rows[1]);
    }

    [Fact]
    public async Task Render_Case_ShowsIdTitleStatusPriorityNorwegianTimesAndHousingUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterAsync("Maling av porter", new BoardCaseDetails
        {
            Title = "Maling av porter",
            Priority = BoardCasePriority.High,
            HousingUnitId = unit.Id
        });
        Clock.Advance(TimeSpan.FromHours(2));
        await BoardCases.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.InProgress);

        var cut = Render<BoardCaseList>();

        cut.WaitForElement("tbody tr");
        var row = RowTexts(cut).Single();
        Assert.Contains($"#{boardCase.Id}", row);
        Assert.Contains("Maling av porter", row);
        Assert.Contains("Under behandling", row);
        Assert.Contains("Høy", row);
        Assert.Contains("18.11.2026 11:30", row);
        Assert.Contains("18.11.2026 13:30", row);
        Assert.Contains("Storgata 1A", row);
    }

    [Fact]
    public async Task Render_CaseWithoutHousingUnit_SaysWholeCooperative()
    {
        await RegisterAsync("Vedtekter");

        var cut = Render<BoardCaseList>();

        cut.WaitForElement("tbody tr");
        Assert.Contains("Hele sameiet", RowTexts(cut).Single());
    }

    [Fact]
    public async Task Render_CaseLinkedToDeactivatedUnit_StillShowsTheUnit()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await RegisterAsync("Maling", new BoardCaseDetails { Title = "Maling", HousingUnitId = unit.Id });
        await Service.DeactivateAsync(unit.Id);

        var cut = Render<BoardCaseList>();

        cut.WaitForElement("tbody tr");
        Assert.Contains("Storgata 1A", RowTexts(cut).Single());
    }

    [Fact]
    public async Task Render_Cases_ShowsTotalAndStatusCounts()
    {
        var first = await RegisterAsync("Sak A");
        await RegisterAsync("Sak B");
        await RegisterAsync("Sak C");
        await BoardCases.ChangeStatusAsync(first.Id, BoardCaseStatus.InProgress);

        var cut = Render<BoardCaseList>();

        cut.WaitForAssertion(() =>
        {
            var values = cut.FindAll(".stat-value").Select(element => element.TextContent).ToList();
            Assert.Equal(["3", "2", "1", "0", "0"], values);
        });
    }

    [Fact]
    public async Task Render_Cases_LinksToDetailAndEditPages()
    {
        var boardCase = await RegisterAsync("Sak A");

        var cut = Render<BoardCaseList>();

        cut.WaitForElement("tbody tr");
        var hrefs = cut.FindAll("tbody a").Select(link => link.GetAttribute("href")).ToList();
        Assert.Contains($"/styresaker/{boardCase.Id}", hrefs);
        Assert.Contains($"/styresaker/{boardCase.Id}/rediger", hrefs);
    }

    [Theory]
    [InlineData("porter")]
    [InlineData("FASADEN")]
    [InlineData("#2")]
    public async Task Search_FindsCaseByTitleDescriptionOrCaseNumber(string searchText)
    {
        await RegisterAsync("Vedtekter");
        await RegisterAsync("Maling av porter", new BoardCaseDetails { Title = "Maling av porter", Description = "Gjelder fasaden" });
        var cut = Render<BoardCaseList>();
        cut.WaitForElement("tbody tr");

        cut.Find("input[type=search]").Input(searchText);

        var row = Assert.Single(RowTexts(cut));
        Assert.Contains("Maling av porter", row);
    }

    [Fact]
    public async Task StatusFilter_ShowsOnlyCasesWithThatStatus()
    {
        var inProgress = await RegisterAsync("Under arbeid");
        await RegisterAsync("Ny sak");
        await BoardCases.ChangeStatusAsync(inProgress.Id, BoardCaseStatus.InProgress);
        var cut = Render<BoardCaseList>();
        cut.WaitForElement("tbody tr");

        Select(cut, "Filtrer på status", "InProgress");

        Assert.Contains("Under arbeid", Assert.Single(RowTexts(cut)));
    }

    [Fact]
    public async Task PriorityFilter_ShowsOnlyCasesWithThatPriority()
    {
        await RegisterAsync("Viktig", new BoardCaseDetails { Title = "Viktig", Priority = BoardCasePriority.High });
        await RegisterAsync("Vanlig");
        var cut = Render<BoardCaseList>();
        cut.WaitForElement("tbody tr");

        Select(cut, "Filtrer på prioritet", "High");

        Assert.Contains("Viktig", Assert.Single(RowTexts(cut)));
    }

    [Fact]
    public async Task HousingUnitFilter_ShowsCasesForThatUnitOrOnlyTheWholeCooperative()
    {
        var unit = await Service.RegisterAsync("Storgata 1A");
        await RegisterAsync("For boenhet", new BoardCaseDetails { Title = "For boenhet", HousingUnitId = unit.Id });
        await RegisterAsync("For hele sameiet");
        var cut = Render<BoardCaseList>();
        cut.WaitForElement("tbody tr");

        Select(cut, "Filtrer på boenhet", unit.Id.ToString());
        Assert.Contains("For boenhet", Assert.Single(RowTexts(cut)));

        Select(cut, "Filtrer på boenhet", "none");
        Assert.Contains("For hele sameiet", Assert.Single(RowTexts(cut)));
    }

    [Fact]
    public async Task Sort_ByTitle_ChangesTheOrder()
    {
        await RegisterAsync("Vedtekter");
        Clock.Advance(TimeSpan.FromHours(1));
        await RegisterAsync("Brannvern");
        var cut = Render<BoardCaseList>();
        cut.WaitForElement("tbody tr");

        Select(cut, "Sorter", "Title");

        var rows = RowTexts(cut);
        Assert.Contains("Brannvern", rows[0]);
        Assert.Contains("Vedtekter", rows[1]);
    }

    [Fact]
    public async Task Search_NoMatch_ShowsMessageAndResetShowsCasesAgain()
    {
        await RegisterAsync("Vedtekter");
        var cut = Render<BoardCaseList>();
        cut.WaitForElement("tbody tr");

        cut.Find("input[type=search]").Input("finnes-ikke");

        Assert.Contains("Ingen styresaker funnet", cut.Markup);
        cut.Find(".empty-state button").Click();
        Assert.Single(cut.FindAll("tbody tr"));
    }

    [Fact]
    public void Render_DatabaseUnavailable_ShowsErrorAndRetryLoadsData()
    {
        BoardCaseRepository.IsFailing = true;
        var cut = Render<BoardCaseList>();
        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke hente styresakene", cut.Find("[role=alert]").TextContent));

        BoardCaseRepository.IsFailing = false;
        cut.Find("button").Click();

        cut.WaitForAssertion(() => Assert.Contains("Ingen styresaker registrert ennå", cut.Markup));
    }

    [Fact]
    public async Task Render_StatusDeletedInTheAddress_ShowsTheDeletedMessage()
    {
        await RegisterAsync("Sak A");
        Services.GetRequiredService<NavigationManager>().NavigateTo("/styresaker?status=slettet");

        var cut = Render<BoardCaseList>();

        cut.WaitForAssertion(() => Assert.Equal("Styresaken er slettet permanent.", cut.Find("[role=status]").TextContent));
    }
}
