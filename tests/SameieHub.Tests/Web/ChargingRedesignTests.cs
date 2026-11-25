using Bunit;
using SameieHub.Domain;
using SameieHub.Web.Components.Pages.Charging;

namespace SameieHub.Tests.Web;

/// <summary>
/// The redesigned Elbillading pages (R5): status lines instead of number cards, tabs, chargers grouped by cabinet, the words for meter
/// reading and consumption, and the explanation of the reconciliation. The calculations are tested in the domain tests.
/// </summary>
public class ChargingRedesignTests : ChargingUiTestBase
{
    private static readonly DateTime Start = new(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 11, 8, 7, 0, 0, DateTimeKind.Utc);

    private async Task<IRenderedComponent<ChargingIndex>> RenderIndexAsync()
    {
        await Task.CompletedTask;
        var cut = Render<ChargingIndex>();
        cut.WaitForElement("[data-testid=charging-status]");

        return cut;
    }

    private IRenderedComponent<ReconciliationPage> RenderReconciliation()
    {
        var cut = Render<ReconciliationPage>();
        cut.WaitForElement("[data-testid=reconciliation]");

        return cut;
    }

    // Overview

    [Fact]
    public async Task Index_HasNoNumberCards_ButStatusLinesWithTheMainMeterReading()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 110);

        var cut = await RenderIndexAsync();

        Assert.Empty(cut.FindAll(".stat-card"));
        var main = cut.FindByTestId("main-meter-status").TextContent;
        Assert.Contains("Målerstand 1100 kWh", main);
        Assert.Contains("avlest 08.11.2026 08:00", main);
        Assert.Contains("8 registrert", cut.FindByTestId("charger-counts").TextContent);
        Assert.Contains("8 i bruk", cut.FindByTestId("charger-counts").TextContent);
    }

    [Fact]
    public async Task Index_ShowsTheLatestReconciliationWithStatusSignedDifferenceAndPercent()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 110);

        var cut = await RenderIndexAsync();

        var status = cut.FindByTestId("reconciliation-status");
        Assert.Contains("Fullstendig", status.TextContent);
        Assert.Equal("+20 kWh", status.QuerySelector("[data-testid=difference]")!.TextContent);
        Assert.Equal("+20,0 %", status.QuerySelector("[data-testid=difference-percent]")!.TextContent);
        Assert.Equal("/elbillading/avstemming", status.QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public async Task Index_IncompleteReconciliation_DoesNotPresentAnyDifference()
    {
        await SeedFacilityAsync(7);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 110);

        var cut = await RenderIndexAsync();

        var status = cut.FindByTestId("reconciliation-status");
        Assert.Contains("Ufullstendig", status.TextContent);
        Assert.Contains("ikke beregnet", status.QuerySelector("[data-testid=difference]")!.TextContent);
        Assert.DoesNotContain("+", status.TextContent);
        Assert.Empty(status.QuerySelectorAll("[data-testid=difference-percent]"));
        Assert.Contains("mangler", status.TextContent);
    }

    [Fact]
    public async Task Index_GroupsTheEightChargersByCabinetWithAChargerLinkAndAHistoryLinkForEach()
    {
        await SeedFacilityAsync(8);

        var cut = await RenderIndexAsync();

        Assert.Equal(["Sikringsskap 1", "Sikringsskap 2"], cut.FindAll("[data-testid=cabinet-group]").Select(heading => heading.TextContent).ToList());
        var rows = cut.FindAll("[data-testid=charger-row]");
        Assert.Equal(8, rows.Count);
        for (var index = 0; index < 8; index++)
        {
            var row = rows.First(candidate => candidate.TextContent.Contains($"Testveien {index + 1}"));
            var subMeter = await SubMeterAsync(index);
            Assert.Equal($"/elbillading/{Chargers[index].Id}", row.QuerySelector("a.unit-link")!.GetAttribute("href"));
            Assert.Equal($"/elbillading/maler/{subMeter.Id}", row.QuerySelector("a[href^='/elbillading/maler/']")!.GetAttribute("href"));
        }

        Assert.Equal(4, cut.FindAll("table")[0].QuerySelectorAll("tbody tr").Length);
        Assert.Equal(4, cut.FindAll("table")[1].QuerySelectorAll("tbody tr").Length);
    }

    [Fact]
    public async Task Index_CallsTheLatestSubMeterValueAMeterReading_NotConsumption()
    {
        await SeedFacilityAsync(1);
        await RegisterFullSetAsync(Start, 1000, 100);

        var cut = await RenderIndexAsync();

        Assert.Contains("Siste målerstand", cut.Find("thead").TextContent);
        Assert.DoesNotContain("Forbruk", cut.Find("thead").TextContent);
        Assert.Contains("100 kWh", cut.FindByTestId("charger-row").TextContent);
    }

    [Fact]
    public async Task Index_OffersTabsWithOverviewSelected_AndAShortLinkToFindAndCorrectAReading()
    {
        await SeedFacilityAsync(8);

        var cut = await RenderIndexAsync();

        var tabs = cut.FindAll("nav[aria-label=Elbillading] a");
        Assert.Equal(["Oversikt", "Registrer avlesning", "Avstemming"], tabs.Select(tab => tab.TextContent.Trim()).ToList());
        Assert.Equal(["/elbillading", "/elbillading/avlesning", "/elbillading/avstemming"], tabs.Select(tab => tab.GetAttribute("href")).ToList());
        Assert.Equal("page", tabs[0].GetAttribute("aria-current"));
        Assert.Null(tabs[1].GetAttribute("aria-current"));
        var link = cut.FindByTestId("correct-reading-link");
        Assert.Equal("Feil avlesning? Se historikk og rett", link.TextContent);
        Assert.Equal($"/elbillading/maler/{MainMeter.Id}", link.GetAttribute("href"));
    }

    [Fact]
    public async Task Index_HeaderHasOnlyTheRarelyUsedRegisterChargerAction_AndTheFirstReadingIsOfferedWhenThereIsNone()
    {
        await SeedFacilityAsync(8);

        var cut = await RenderIndexAsync();

        Assert.Equal(["Registrer lader"], cut.FindByTestId("page-actions").QuerySelectorAll("a").Select(link => link.TextContent).ToList());
        Assert.Contains("Ingen avlesninger er registrert", cut.FindByTestId("main-meter-status").TextContent);
        Assert.Equal("/elbillading/avlesning", cut.FindByTestId("main-meter-status").QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact]
    public void Index_WithoutFacility_ShowsNoTabs()
    {
        var cut = Render<ChargingIndex>();
        cut.WaitForElement("#facilityName");

        Assert.Empty(cut.FindAll("nav[aria-label=Elbillading]"));
    }

    // Registering readings

    [Fact]
    public async Task ReadingSetCreate_ExplainsMeterReadingAndConsumption_AndShowsTheTabs()
    {
        await SeedFacilityAsync(8);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");

        var text = cut.FindByTestId("reading-explanation").TextContent;
        Assert.Contains("Målerstand er tallet måleren viser", text);
        Assert.Contains("Forbruk er forskjellen mellom to målerstander", text);
        Assert.Equal("page", cut.Find("nav[aria-label=Elbillading] a[href='/elbillading/avlesning']").GetAttribute("aria-current"));
    }

    [Fact]
    public async Task ReadingSetCreate_EachFieldNamesTheMeterAndShowsThePreviousMeterReadingAndTheConsumptionSincePrevious()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        var subMeter = await SubMeterAsync(0);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        var field = cut.Find($"#kwh-{subMeter.Id}");
        field.Input("112,5");

        var container = field.ParentElement!;
        Assert.Contains("Testveien 1", container.QuerySelector("label")!.TextContent);
        Assert.Contains("målerstand (kWh)", container.QuerySelector("label")!.TextContent);
        Assert.Contains("Forrige målerstand: 100 kWh", container.TextContent);
        Assert.Contains("Forbruk siden forrige avlesning: +12,5 kWh", container.TextContent);
    }

    [Fact]
    public async Task ReadingSetCreate_ALowerMeterReadingIsCalledNotPossible_NotNegativeConsumption()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        var field = cut.Find($"#kwh-{MainMeter.Id}");
        field.Input("900");

        Assert.Contains("Målerstanden kan ikke gå ned", field.ParentElement!.TextContent);
    }

    [Fact]
    public async Task ReadingSetCreate_AfterSave_LinksToTheHistoryWhereAReadingCanBeCorrected()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-10T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("1100");
        cut.Find("form").Submit();
        cut.WaitForElement("[role=status] h2");

        var link = cut.FindByTestId("correct-reading-link");
        Assert.Equal($"/elbillading/maler/{MainMeter.Id}", link.GetAttribute("href"));
        Assert.Contains("Forbruk siden forrige avlesning", cut.Find("thead").TextContent);
        Assert.Contains("Ny målerstand", cut.Find("thead").TextContent);
    }

    // History

    [Fact]
    public async Task MeterHistory_ExplainsTheDifferenceBetweenMeterReadingAndConsumption_AndStillOffersCorrectionAndDeletion()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 110);

        var cut = Render<MeterHistoryPage>(parameters => parameters.Add(page => page.MeterId, MainMeter.Id));
        cut.WaitForElement("[data-testid=reading-explanation]");

        Assert.Contains("Målerstand er tallet måleren viste", cut.FindByTestId("reading-explanation").TextContent);
        Assert.Contains("Forbruk siden forrige avlesning", cut.Find("thead").TextContent);
        Assert.Contains(cut.FindAll("tbody button"), button => button.TextContent.Trim() == "Korriger");
        Assert.Contains(cut.FindAll("tbody button"), button => button.TextContent.Trim() == "Slett");
        Assert.Contains("+100 kWh", cut.Find("tbody").TextContent);
    }

    // Reconciliation page

    [Fact]
    public async Task Reconciliation_ShowsTheTabsAndTheThreeNumbersWithClearNames()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 110);

        var cut = RenderReconciliation();

        Assert.Equal("page", cut.Find("nav[aria-label=Elbillading] a[href='/elbillading/avstemming']").GetAttribute("aria-current"));
        var labels = cut.FindAll(".reconciliation-summary dt").Select(term => term.TextContent).ToList();
        Assert.Equal(
            ["Forbruk i perioden målt av hovedmåleren", "Sum av forbruk i perioden på undermålerne", "Differanse (hovedmåler − undermålere)"],
            labels);
        var values = cut.FindAll(".reconciliation-summary dd").Select(value => value.TextContent.Trim()).ToList();
        Assert.Equal("100 kWh", values[0]);
        Assert.Equal("80 kWh", values[1]);
        Assert.StartsWith("+20 kWh", values[2]);
        Assert.Contains("+20,0 %", values[2]);
    }

    [Fact]
    public async Task Reconciliation_PositiveDifference_IsExplainedNeutrallyAndNotAsALoss()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 110);

        var cut = RenderReconciliation();

        var text = cut.FindByTestId("difference-explanation").TextContent;
        Assert.Contains("En positiv differanse betyr at hovedmåleren viser mer forbruk enn summen av undermålerne", text);
        Assert.Contains("ikke i seg selv et tap eller bevis på feil", text);
        Assert.Contains("Forbruket er forskjellen mellom to målerstander", text);
    }

    [Fact]
    public async Task Reconciliation_NegativeDifference_ShowsTheMinusSignAndItsMeaning()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 115);

        var cut = RenderReconciliation();

        Assert.Equal("−20 kWh", cut.Find("[data-testid=difference]").TextContent);
        Assert.Equal("−20,0 %", cut.Find("[data-testid=difference-percent]").TextContent);
        Assert.Contains("En negativ differanse betyr at undermålerne til sammen viser mer forbruk", cut.FindByTestId("difference-explanation").TextContent);
    }

    [Fact]
    public async Task Reconciliation_ZeroDifference_HasNoSignAndSaysTheyAreEqual()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1080, 110);

        var cut = RenderReconciliation();

        Assert.Equal("0 kWh", cut.Find("[data-testid=difference]").TextContent);
        Assert.Contains("like mye forbruk", cut.FindByTestId("difference-explanation").TextContent);
    }

    [Fact]
    public async Task Reconciliation_MainMeterWithoutConsumption_ExplainsWhyThereIsNoPercent()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1000, 100);

        var cut = RenderReconciliation();

        Assert.Empty(cut.FindAll("[data-testid=difference-percent]"));
        Assert.Contains("Prosent kan ikke beregnes fordi hovedmåleren viste 0 kWh", cut.FindByTestId("difference-explanation").TextContent);
    }

    [Fact]
    public async Task Reconciliation_Incomplete_SaysWhatIsMissingAndShowsNoDifferenceOrDirection()
    {
        await SeedFacilityAsync(7);
        await RegisterFullSetAsync(Start, 1000, 100);
        await RegisterFullSetAsync(End, 1100, 110);

        var cut = RenderReconciliation();

        Assert.Equal("Ikke beregnet", cut.Find("[data-testid=difference]").TextContent);
        Assert.Contains("Bare 7 av 8 undermålere", cut.Find(".alert-warning").TextContent);
        var explanation = cut.FindByTestId("difference-explanation").TextContent;
        Assert.Contains("Differansen vises ikke", explanation);
        Assert.DoesNotContain("positiv", explanation);
        Assert.DoesNotContain("negativ", explanation);
        Assert.Contains("bare dokumenterte", cut.Find(".reconciliation-summary").TextContent);
    }
}
