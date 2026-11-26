using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Web.Components.Layout;
using SameieHub.Web.Components.Pages;
using SameieHub.Web.Components.Pages.BoardCases;
using SameieHub.Web.Components.Pages.Charging;
using SameieHub.Web.Components.Pages.HousingUnits;
using SameieHub.Web.Components.Pages.TaskItems;
using SameieHub.Web.Components.Shared;

namespace SameieHub.Tests.Web;

/// <summary>
/// Navigation, the «Flere handlinger» menu, the words used for the same things everywhere, and the rules in the stylesheet that keep the
/// pages usable on a small screen (R6). What the keyboard and the small screen really do is checked in the browser.
/// </summary>
public class NavigationConsistencyTests : ChargingUiTestBase
{
    private const string MoreMenuModule = "./Components/Shared/MoreMenu.razor.js";

    // Main navigation

    [Fact]
    public void NavMenu_HasTheAgreedOrder_WithTheBackupLinkSeparatedLast_AndNoDashboardOrMeetingEntry()
    {
        var cut = Render<NavMenu>();

        var links = cut.FindAll("#main-nav a");
        Assert.Equal(
            ["Oversikt", "Styresaker", "Oppgaver", "Boenheter", "Elbillading", "Sikkerhetskopi"],
            links.Select(link => link.TextContent.Trim()).ToList());
        Assert.Equal(
            ["/", "/styresaker", "/oppgaver", "/boenheter", "/elbillading", "/innstillinger/sikkerhetskopi"],
            links.Select(link => link.GetAttribute("href")).ToList());
        Assert.Contains("nav-separated", links[^1].ClassName);
        Assert.DoesNotContain("nav-separated", string.Join(" ", links.Take(5).Select(link => link.ClassName)));
        Assert.DoesNotContain("Dashboard", cut.Markup);
        Assert.DoesNotContain("møte", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/", "Oversikt")]
    [InlineData("/styresaker", "Styresaker")]
    [InlineData("/styresaker/4", "Styresaker")]
    [InlineData("/styresaker/ny", "Styresaker")]
    [InlineData("/oppgaver/4/rediger", "Oppgaver")]
    [InlineData("/boenheter/2", "Boenheter")]
    [InlineData("/boenheter/ny", "Boenheter")]
    [InlineData("/elbillading/maler/1", "Elbillading")]
    [InlineData("/elbillading/avstemming", "Elbillading")]
    [InlineData("/innstillinger/sikkerhetskopi", "Sikkerhetskopi")]
    public void NavMenu_MarksTheSectionOfTheCurrentPageAsActive_AlsoOnDetailAndRegistrationPages(string path, string expected)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(path);

        var cut = Render<NavMenu>();

        var active = cut.FindAll("#main-nav a.active").Select(link => link.TextContent.Trim()).ToList();
        Assert.Equal([expected], active);
    }

    [Fact]
    public void NavMenu_TheMenuButtonOnASmallScreen_TellsWhetherTheMenuIsOpen_AndOpensAndClosesIt()
    {
        var cut = Render<NavMenu>();
        var button = cut.Find("button.menu-toggle");
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Equal("main-nav", button.GetAttribute("aria-controls"));

        button.Click();
        Assert.Equal("true", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));
        Assert.Contains("open", cut.Find("#main-nav").ClassName);

        cut.FindAll("#main-nav a")[1].Click();
        Assert.Equal("false", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void NavMenu_EscapeClosesTheOpenMenu_AndMovesTheFocusBackToTheMenuButton()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NavMenu>();
        cut.Find("button.menu-toggle").Click();
        Assert.Equal("true", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));

        cut.Find("aside").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.Equal("false", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("open", cut.Find("#main-nav").ClassName);
        JSInterop.VerifyInvoke("Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public void NavMenu_OtherKeysAndEscapeWithAClosedMenu_ChangeNothing()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<NavMenu>();

        cut.Find("aside").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("false", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));
        Assert.Empty(JSInterop.Invocations);

        cut.Find("button.menu-toggle").Click();
        cut.Find("aside").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("true", cut.Find("button.menu-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void NavMenu_TheMenuClosesWhenThePageChanges_AlsoWhenALinkOutsideTheMenuWasUsed()
    {
        var cut = Render<NavMenu>();
        cut.Find("button.menu-toggle").Click();

        Services.GetRequiredService<NavigationManager>().NavigateTo("/oppgaver");

        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find("button.menu-toggle").GetAttribute("aria-expanded")));
    }

    [Fact]
    public void Css_TheClosedMenuIsNotShownOnASmallScreen_SoItsLinksCannotGetTheKeyboardFocus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "src", "SameieHub.Web", "Components", "Layout", "NavMenu.razor.css")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var css = File.ReadAllText(Path.Combine(directory.FullName, "src", "SameieHub.Web", "Components", "Layout", "NavMenu.razor.css")).Replace("\r\n", "\n");

        Assert.Contains(".main-nav {\n    flex: 1;\n    display: none;", css);
        Assert.Contains(".main-nav.open {\n        display: flex;", css);
    }

    [Fact]
    public void MainLayout_StartsWithASkipLinkToTheContent_ThatMovesTheFocusThere()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<MainLayout>(parameters => parameters.Add(layout => layout.Body, builder => builder.AddMarkupContent(0, "<p>Innhold</p>")));

        var skip = cut.Find("a.skip-link");
        Assert.EndsWith("#main-content", skip.GetAttribute("href"));
        Assert.StartsWith(Services.GetRequiredService<NavigationManager>().Uri.Split('#')[0], skip.GetAttribute("href"));
        Assert.Equal("Hopp til innhold", skip.TextContent);
        Assert.Equal("main-content", cut.Find("main").Id);
        Assert.Equal("-1", cut.Find("main").GetAttribute("tabindex"));
        skip.Click();
        JSInterop.VerifyInvoke("Blazor._internal.domWrapper.focus");
    }

    // The «Flere handlinger» menu

    [Fact]
    public void MoreMenu_ConnectsTheScriptThatClosesItWithEscapeAndOutsideClicks()
    {
        var module = JSInterop.SetupModule(MoreMenuModule);
        module.SetupVoid("attach", _ => true);
        module.SetupVoid("detach", _ => true);

        var cut = Render<MoreMenu>(parameters => parameters.AddChildContent("<button type=\"button\">Handling</button>"));

        module.VerifyInvoke("attach");
        Assert.False(cut.Find("details").HasAttribute("open"));
        Assert.Equal("SUMMARY", cut.Find("details > summary").TagName);
    }

    [Fact]
    public async Task MoreMenu_DisconnectsTheScriptWhenItIsRemoved()
    {
        var module = JSInterop.SetupModule(MoreMenuModule);
        module.SetupVoid("attach", _ => true);
        module.SetupVoid("detach", _ => true);
        var cut = Render<MoreMenu>(parameters => parameters.AddChildContent("<button type=\"button\">Handling</button>"));

        await DisposeComponentsAsync();

        module.VerifyInvoke("detach");
        Assert.NotNull(cut);
    }

    [Fact]
    public void MoreMenu_TheItemsAreRealButtonsAndLinks_ReachedInTheNaturalTabOrderAfterTheSummary()
    {
        var cut = Render<MoreMenu>(parameters => parameters.AddChildContent("<a href=\"/x\">Rediger</a><button type=\"button\">Slett</button>"));

        var children = cut.Find("details").Children.Select(child => child.TagName).ToList();
        Assert.Equal(["SUMMARY", "DIV"], children);
        Assert.Equal(["A", "BUTTON"], cut.Find(".more-menu-items").Children.Select(child => child.TagName).ToList());
        Assert.Empty(cut.FindAll("[tabindex]"));
    }

    // Same words for the same things

    [Fact]
    public async Task NoPageUsesTheOldWordsForCreatingThings()
    {
        await SeedFacilityAsync(2);
        await Service.RegisterAsync("Uten Lader 9");
        var unit = Units[0];
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak", HousingUnitId = unit.Id });
        var task = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave", BoardCaseId = boardCase.Id, HousingUnitId = unit.Id });
        await RegisterFullSetAsync(new DateTime(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc), 1000, 100);
        await RegisterFullSetAsync(new DateTime(2026, 11, 8, 7, 0, 0, DateTimeKind.Utc), 1100, 110);

        var pages = new List<(string Name, string Markup)>
        {
            ("Oversikt", Render<Dashboard>().WaitForMarkup("[data-testid=status-summary]")),
            ("Styresaker", Render<BoardCaseList>().WaitForMarkup("[data-testid=quick-tab-open]")),
            ("Oppgaver", Render<TaskList>().WaitForMarkup("[data-testid=quick-tab-open]")),
            ("Boenheter", Render<HousingUnitList>().WaitForMarkup("table")),
            ("Elbillading", Render<ChargingIndex>().WaitForMarkup("[data-testid=charging-status]")),
            ("Ny avlesning", Render<ReadingSetCreate>().WaitForMarkup("#readAt")),
            ("Styresak", Render<BoardCaseDetail>(p => p.Add(page => page.Id, boardCase.Id)).WaitForMarkup("[data-testid=case-summary]")),
            ("Oppgave", Render<TaskDetail>(p => p.Add(page => page.Id, task.Id)).WaitForMarkup("[data-testid=task-summary]")),
            ("Boenhet", Render<HousingUnitDetail>(p => p.Add(page => page.Id, unit.Id)).WaitForMarkup(".detail-grid")),
            ("Ny boenhet", Render<HousingUnitCreate>().WaitForMarkup("#address")),
            ("Ny styresak", Render<BoardCaseCreate>().WaitForMarkup("#title")),
            ("Ny oppgave", Render<TaskCreate>().WaitForMarkup("#title")),
            ("Ny lader", Render<ChargerCreate>().WaitForMarkup("h1")),
        };

        string[] oldWords = ["Dashboard", "Registrer boenhet", "Registrer styresak", "Registrer oppgave", "Registrer lader", "Registrer avlesning", "Rediger elbillader"];
        foreach (var (name, markup) in pages)
        {
            foreach (var word in oldWords)
            {
                Assert.False(markup.Contains(word, StringComparison.Ordinal), $"{name} still says «{word}».");
            }
        }
    }

    [Fact]
    public void TheFormsSaveWith_Lagre_AndCancelWith_Avbryt()
    {
        var task = Render<TaskCreate>();
        task.WaitForElement("#title");
        var boardCase = Render<BoardCaseCreate>();
        boardCase.WaitForElement("#title");
        var unit = Render<HousingUnitCreate>();
        unit.WaitForElement("#address");

        Assert.Equal("Lagre", task.Find("button[type=submit]").TextContent.Trim());
        Assert.Equal("Avbryt", task.Find(".form-actions a").TextContent.Trim());
        Assert.Equal("Lagre", boardCase.Find("button[type=submit]").TextContent.Trim());
        Assert.Equal("Avbryt", boardCase.Find(".form-actions a").TextContent.Trim());
        Assert.Equal("Lagre", unit.Find("button[type=submit]").TextContent.Trim());
        Assert.Equal("Avbryt", unit.Find(".form-actions a").TextContent.Trim());
    }

    [Fact]
    public void TheMainActionOfEachListIsCalledNew_AndTheEditButtonsAreNotFilled()
    {
        var list = Render<HousingUnitList>();
        list.WaitForElement(".empty-state, table");
        Assert.Contains(list.FindAll("a.btn-primary"), link => link.TextContent.Trim() == "Ny boenhet");

        var cases = Render<BoardCaseList>();
        cases.WaitForElement(".empty-state, table");
        Assert.Contains(cases.FindAll("a.btn-primary"), link => link.TextContent.Trim() == "Ny styresak");

        var tasks = Render<TaskList>();
        tasks.WaitForElement(".empty-state, table");
        Assert.Contains(tasks.FindAll("a.btn-primary"), link => link.TextContent.Trim() == "Ny oppgave");
    }

    [Fact]
    public async Task EditIsAnOutlinedButtonOnEveryDetailPage_NeverTheFilledMainButton()
    {
        await SeedFacilityAsync(1);
        var boardCase = await BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak" });
        var task = await Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave" });

        var caseDetail = Render<BoardCaseDetail>(p => p.Add(page => page.Id, boardCase.Id));
        caseDetail.WaitForElement("[data-testid=case-summary]");
        var taskDetail = Render<TaskDetail>(p => p.Add(page => page.Id, task.Id));
        taskDetail.WaitForElement("[data-testid=task-summary]");
        var unitDetail = Render<HousingUnitDetail>(p => p.Add(page => page.Id, Units[0].Id));
        unitDetail.WaitForElement(".detail-grid");
        var chargerDetail = Render<ChargerDetail>(p => p.Add(page => page.Id, Chargers[0].Id));
        chargerDetail.WaitForElement(".detail-grid");

        AssertOutlinedEdit(caseDetail);
        AssertOutlinedEdit(taskDetail);
        AssertOutlinedEdit(unitDetail);
        AssertOutlinedEdit(chargerDetail);
    }

    private static void AssertOutlinedEdit<T>(IRenderedComponent<T> page) where T : IComponent
    {
        var edit = page.FindAll(".page-actions a").First(link => link.TextContent.Trim() == "Rediger");

        Assert.Contains("btn-outline-secondary", edit.ClassName);
        Assert.DoesNotContain("btn-primary", edit.ClassName);
    }

    // Stylesheet rules for small screens

    private static string Css()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "src", "SameieHub.Web", "wwwroot", "app.css")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return File.ReadAllText(Path.Combine(directory.FullName, "src", "SameieHub.Web", "wwwroot", "app.css"));
    }

    [Fact]
    public async Task TheReadingHistoryAndTheHousingUnitList_BecomeCardsOnASmallScreen_WithALabelOnEveryCell_AndButtonsInTheirOwnCell()
    {
        await SeedFacilityAsync(1);
        await RegisterFullSetAsync(new DateTime(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc), 1000, 100);
        var history = Render<MeterHistoryPage>(p => p.Add(page => page.MeterId, MainMeter.Id));
        history.WaitForElement("[data-testid=reading-table]");
        var units = Render<HousingUnitList>();
        units.WaitForElement("[data-testid=unit-table]");

        foreach (var table in new[] { history.Find("[data-testid=reading-table]"), units.Find("[data-testid=unit-table]") })
        {
            Assert.Contains("table-cards", table.ClassName);
            foreach (var cell in table.QuerySelectorAll("tbody td"))
            {
                Assert.True(cell.HasAttribute("data-label") || cell.ClassList.Contains("cell-actions"), $"A cell has no label: {cell.TextContent}");
            }
        }

        Assert.Contains(history.FindAll("tbody td.cell-actions button"), button => button.TextContent.Trim() == "Korriger");
    }

    [Fact]
    public void Css_CardListsBeginBelow992Pixels_SoNoButtonIsClippedOnATabletOrAPhone()
    {
        var css = Css();

        Assert.Contains("@media (max-width: 991.98px) {\n    .table-cards thead", css.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Css_ButtonsAreAtLeast44PixelsHighOnTouchScreens_AndAnOpenMenuIsPartOfThePageFlow()
    {
        var css = Css().Replace("\r\n", "\n");

        Assert.Contains("@media (max-width: 991.98px) {\n    .btn {\n        display: inline-flex;\n        align-items: center;\n        justify-content: center;\n        min-height: 2.75rem;", css);
        Assert.Contains(".more-menu .more-menu-items {\n        position: static;", css);
        Assert.Contains(".data-table-card {\n    overflow-x: auto;", css);
        Assert.Contains(".table-responsive {\n    background:", css);
    }
}

internal static class NavigationTestExtensions
{
    public static string WaitForMarkup<T>(this IRenderedComponent<T> cut, string selector) where T : IComponent
    {
        try
        {
            cut.WaitForElement(selector);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{typeof(T).Name} never showed {selector}. Markup: {cut.Markup[..Math.Min(300, cut.Markup.Length)]}", exception);
        }

        return cut.Markup;
    }
}
