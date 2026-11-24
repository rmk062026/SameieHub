using Bunit;
using Microsoft.AspNetCore.Components;
using SameieHub.Web.Components.Shared;

namespace SameieHub.Tests.Web;

/// <summary>
/// The shared building blocks for the redesign (R0). They are not used on any page yet, so these tests show how they behave: what they
/// render, that they can be used with the keyboard, and that an error can never be hidden inside something that is closed.
/// </summary>
public class UiBuildingBlocksTests : BunitContext
{
    // PageHeader

    [Fact]
    public void PageHeader_WithOnlyTheOldParameters_RendersAsBefore()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(header => header.Title, "Boenheter")
            .Add(header => header.BackHref, "/")
            .AddChildContent("<a class=\"btn\" href=\"/ny\">Registrer</a>"));

        Assert.Equal("Boenheter", cut.Find("h1").TextContent);
        Assert.Equal("Registrer", cut.Find(".page-actions a").TextContent);
        Assert.Empty(cut.FindAll("[data-testid=primary-action]"));
        Assert.Empty(cut.FindAll("[data-testid=more-menu]"));
    }

    [Fact]
    public void PageHeader_PrimaryActionAndMoreActions_ArePlacedInTheActionArea()
    {
        var cut = Render<PageHeader>(parameters => parameters
            .Add(header => header.Title, "Oppgave")
            .Add(header => header.PrimaryAction, "<button class=\"btn btn-primary\">Marker som fullført</button>")
            .Add(header => header.MoreActions, "<button class=\"dropdown-item\">Rediger</button>"));

        Assert.Equal("Marker som fullført", cut.FindByTestId("primary-action").TextContent);
        Assert.Contains("Rediger", cut.FindByTestId("more-menu").TextContent);
        Assert.Equal("page-title", cut.Find("h1").GetAttribute("data-testid"));
    }

    // MoreMenu

    [Fact]
    public void MoreMenu_IsClosedByDefault_HasAFocusableSummaryAndShowsItsItems()
    {
        var cut = Render<MoreMenu>(parameters => parameters.AddChildContent("<button type=\"button\">Slett oppgaven …</button>"));

        var details = cut.Find("details");
        Assert.False(details.HasAttribute("open"));
        Assert.Equal("Flere handlinger", cut.Find("summary").TextContent.Trim());
        Assert.Equal("Slett oppgaven …", cut.Find("details button").TextContent);
        Assert.Equal("group", cut.Find("[role=group]").GetAttribute("role"));
    }

    [Fact]
    public void MoreMenu_CanHaveAnotherLabel()
    {
        var cut = Render<MoreMenu>(parameters => parameters.Add(menu => menu.Label, "Mer").AddChildContent("<span>x</span>"));

        Assert.Equal("Mer", cut.Find("summary").TextContent.Trim());
        Assert.Equal("Mer", cut.Find("[role=group]").GetAttribute("aria-label"));
    }

    // Disclosure

    [Fact]
    public void Disclosure_IsClosedByDefault_AndTheSummaryTellsWhatIsInside()
    {
        var cut = Render<Disclosure>(parameters => parameters
            .Add(disclosure => disclosure.Label, "Flere valg")
            .Add(disclosure => disclosure.Hint, "Kategori · Prioritet · Merknader")
            .AddChildContent("<input id=\"x\" />"));

        Assert.False(cut.Find("details").HasAttribute("open"));
        Assert.Equal("Flere valg", cut.Find("summary .disclosure-label").TextContent);
        Assert.Equal("Kategori · Prioritet · Merknader", cut.Find("summary .disclosure-hint").TextContent);
        Assert.NotEmpty(cut.FindAll("#x"));
    }

    [Fact]
    public void Disclosure_OpenParameter_StartsItOpen()
    {
        var cut = Render<Disclosure>(parameters => parameters.Add(disclosure => disclosure.Label, "Detaljer").Add(disclosure => disclosure.Open, true));

        Assert.True(cut.Find("details").HasAttribute("open"));
    }

    [Fact]
    public void Disclosure_OpensByItself_WhenSomethingInsideNeedsAttention_AndStaysOpenAfterwards()
    {
        var cut = Render<Disclosure>(parameters => parameters.Add(disclosure => disclosure.Label, "Flere valg"));
        Assert.False(cut.Find("details").HasAttribute("open"));

        cut.Render(parameters => parameters.Add(disclosure => disclosure.Label, "Flere valg").Add(disclosure => disclosure.ForceOpen, true));
        Assert.True(cut.Find("details").HasAttribute("open"));

        cut.Render(parameters => parameters.Add(disclosure => disclosure.Label, "Flere valg").Add(disclosure => disclosure.ForceOpen, false));
        Assert.True(cut.Find("details").HasAttribute("open"));
    }

    // ActiveFilters

    [Fact]
    public void ActiveFilters_WithoutFilters_RendersNothing()
    {
        var cut = Render<ActiveFilters>(parameters => parameters.Add(filters => filters.Filters, []));

        Assert.Empty(cut.FindAll("[data-testid=active-filters]"));
    }

    [Fact]
    public async Task ActiveFilters_ShowsEveryFilter_CanRemoveOneAndClearAll()
    {
        var removed = new List<string>();
        var cleared = false;
        var cut = Render<ActiveFilters>(parameters => parameters
            .Add(filters => filters.Filters, [new ActiveFilter("status", "Status: Pågår"), new ActiveFilter("prioritet", "Prioritet: Høy")])
            .Add(filters => filters.OnRemove, EventCallback.Factory.Create<string>(this, key => removed.Add(key)))
            .Add(filters => filters.OnClearAll, EventCallback.Factory.Create(this, () => cleared = true)));

        Assert.Contains("Status: Pågår", cut.FindByTestId("active-filters").TextContent);
        Assert.Equal("Fjern filteret Prioritet: Høy", cut.FindAll(".filter-chip-remove")[1].GetAttribute("aria-label"));

        cut.FindAll(".filter-chip-remove")[0].Click();
        cut.ClickButton("Fjern alle filtre");

        Assert.Equal(["status"], removed);
        Assert.True(cleared);
        await Task.CompletedTask;
    }

    [Fact]
    public void ActiveFilters_ClearAllIsOnlyOfferedWhenThereIsMoreThanOne()
    {
        var cut = Render<ActiveFilters>(parameters => parameters.Add(filters => filters.Filters, [new ActiveFilter("status", "Status: Pågår")]));

        Assert.DoesNotContain("Fjern alle filtre", cut.Markup);
    }

    // QuickTabs

    [Fact]
    public void QuickTabs_ShowsTheCountAndMarksTheSelectedOneForScreenReaders()
    {
        var cut = Render<QuickTabs>(parameters => parameters
            .Add(tabs => tabs.AriaLabel, "Vis oppgaver")
            .Add(tabs => tabs.Tabs, [new QuickTab("open", "Åpne", 15), new QuickTab("overdue", "Forfalte", 7)])
            .Add(tabs => tabs.Selected, "overdue"));

        Assert.Equal("Vis oppgaver", cut.Find("nav").GetAttribute("aria-label"));
        var buttons = cut.FindAll("button");
        Assert.Equal("Åpne 15", buttons[0].TextContent.Trim());
        Assert.Equal("false", buttons[0].GetAttribute("aria-pressed"));
        Assert.Equal("true", buttons[1].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void QuickTabs_ClickingATab_ReportsItsKey()
    {
        string? chosen = null;
        var cut = Render<QuickTabs>(parameters => parameters
            .Add(tabs => tabs.Tabs, [new QuickTab("open", "Åpne"), new QuickTab("done", "Fullførte")])
            .Add(tabs => tabs.Selected, "open")
            .Add(tabs => tabs.SelectedChanged, EventCallback.Factory.Create<string>(this, key => chosen = key)));

        cut.ClickButton("Fullførte");

        Assert.Equal("done", chosen);
    }

    [Fact]
    public void QuickTabs_WithAddresses_AreLinksAndTheSelectedOneIsTheCurrentPage()
    {
        var cut = Render<QuickTabs>(parameters => parameters
            .Add(tabs => tabs.Tabs, [new QuickTab("oversikt", "Oversikt", Href: "/elbillading"), new QuickTab("avstemming", "Avstemming", Href: "/elbillading/avstemming")])
            .Add(tabs => tabs.Selected, "avstemming"));

        var links = cut.FindAll("a");
        Assert.Empty(cut.FindAll("button"));
        Assert.Equal("/elbillading", links[0].GetAttribute("href"));
        Assert.Null(links[0].GetAttribute("aria-current"));
        Assert.Equal("page", links[1].GetAttribute("aria-current"));
    }

    // ActionFeedback

    [Fact]
    public void ActionFeedback_WithoutMessage_RendersNothing()
    {
        var cut = Render<ActionFeedback>();

        Assert.Empty(cut.FindAll("[data-testid=action-feedback]"));
    }

    [Fact]
    public void ActionFeedback_Success_IsAStatusWithAnUndoAction()
    {
        var undone = false;
        var cut = Render<ActionFeedback>(parameters => parameters
            .Add(feedback => feedback.Message, "Oppgaven er fullført.")
            .Add(feedback => feedback.ActionText, "Angre")
            .Add(feedback => feedback.OnAction, EventCallback.Factory.Create(this, () => undone = true)));

        var element = cut.FindByTestId("action-feedback");
        Assert.Equal("status", element.GetAttribute("role"));
        Assert.Contains("Oppgaven er fullført.", element.TextContent);

        cut.FindByTestId("feedback-action").Click();

        Assert.True(undone);
    }

    [Fact]
    public void ActionFeedback_Error_IsAnAlertAndCanBeDismissed()
    {
        var dismissed = false;
        var cut = Render<ActionFeedback>(parameters => parameters
            .Add(feedback => feedback.Message, "Kunne ikke lagre.")
            .Add(feedback => feedback.Kind, FeedbackKind.Error)
            .Add(feedback => feedback.OnDismiss, EventCallback.Factory.Create(this, () => dismissed = true)));

        Assert.Equal("alert", cut.FindByTestId("action-feedback").GetAttribute("role"));
        Assert.Empty(cut.FindAll("[data-testid=feedback-action]"));

        cut.Find("[data-testid=feedback-dismiss]").Click();

        Assert.True(dismissed);
    }

    // Test helpers

    [Fact]
    public void TestHelpers_FindRowsFieldsAndButtonsByWhatTheyShow()
    {
        var cut = Render<TestHelperTarget>();

        Assert.Contains("Bestille", cut.FindRowContaining("Bestille").TextContent);
        Assert.Equal("title", cut.FindFieldByLabel("Tittel").Id);
        Assert.Equal("Lagre", cut.FindButton("Lagre").TextContent);
        Assert.True(cut.HasTestId("x"));
        Assert.Throws<InvalidOperationException>(() => cut.FindRowContaining("finnes ikke"));
    }

    /// <summary>A tiny component with a table, a labelled field and a button, to try the helpers on.</summary>
    private sealed class TestHelperTarget : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.AddMarkupContent(0,
                "<table><tbody><tr><td>Bestille servicebesøk</td></tr></tbody></table>"
                + "<label for=\"title\">Tittel *</label><input id=\"title\" data-testid=\"x\" /><button>Lagre</button>");
        }
    }
}
