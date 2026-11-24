using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SameieHub.Domain;
using SameieHub.Web.Components.Layout;
using SameieHub.Web.Components.Pages;
using SameieHub.Web.Components.Pages.Charging;
using SameieHub.Web.Components.Pages.HousingUnits;

namespace SameieHub.Tests.Web;

public class ChargingPagesTests : ChargingUiTestBase
{
    private static readonly DateTime Jan1 = new(2026, 11, 1, 7, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Jan2 = new(2026, 11, 8, 7, 0, 0, DateTimeKind.Utc);

    // Navigation

    [Fact]
    public void NavMenu_HasLinkToCharging()
    {
        var cut = Render<NavMenu>();

        var link = cut.FindAll("a").First(element => element.TextContent.Contains("Elbillading"));
        Assert.Equal("elbillading", link.GetAttribute("href")!.TrimStart('/'));
    }

    [Theory]
    [InlineData("2026-11-10T08:00")]
    [InlineData("2026-11-10T08:00:00")]
    public void ChargingInput_AcceptsTimeWithAndWithoutSeconds(string text)
    {
        var error = ChargingInput.TryConvertToUtc(text, null, out var utc);

        Assert.Null(error);
        Assert.Equal(new DateTime(2026, 11, 10, 7, 0, 0, DateTimeKind.Utc), utc);
    }

    [Theory]
    [InlineData("2027-10-31T02:30")]
    [InlineData("2027-10-31T02:30:00")]
    public void ChargingInput_RepeatedHourIsAmbiguousWithAndWithoutSeconds(string text)
    {
        Assert.True(ChargingInput.IsAmbiguous(text));
    }

    // Overview

    [Fact]
    public async Task Index_WithoutFacility_OffersSetupAndCreatesFacility()
    {
        var cut = Render<ChargingIndex>();
        cut.WaitForElement("#facilityName");

        cut.Find("#facilityName").Input("Ladeanlegg");
        cut.Find("form").Submit();

        cut.WaitForElement(".stat-row");
        Assert.NotNull(await Charging.GetFacilityAsync());
        Assert.NotNull(await Charging.GetMainMeterAsync());
    }

    [Fact]
    public async Task Index_EmptyFacilityName_ShowsMessageAndStoresNothing()
    {
        var cut = Render<ChargingIndex>();
        cut.WaitForElement("#facilityName");

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Oppgi et navn", cut.Find("[role=alert]").TextContent));
        Assert.Null(await Charging.GetFacilityAsync());
    }

    [Fact]
    public async Task Index_ShowsChargersCountsAndUnitsWithoutCharger()
    {
        await SeedFacilityAsync(2);
        await Service.RegisterAsync("Uten Lader 9");
        await Charging.ChangeChargerStatusAsync(Chargers[1].Id, ChargerStatus.TemporarilyDisabled);

        var cut = Render<ChargingIndex>();
        cut.WaitForElement(".stat-row");

        Assert.Contains("Testveien 1", cut.Markup);
        Assert.Contains("Testveien 2", cut.Markup);
        Assert.Contains("Midlertidig deaktivert", cut.Markup);
        Assert.Contains("Uten Lader 9", cut.Find("#without-title").ParentElement!.TextContent);
        Assert.Contains("Ingen avlesning", cut.Markup);
    }

    [Fact]
    public async Task Index_WithOnlyOneMainReading_SaysReconciliationNeedsTwoReadings()
    {
        await SeedFacilityAsync(8);
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 1000 });

        var cut = Render<ChargingIndex>();
        cut.WaitForElement(".stat-row");

        Assert.Contains("minst to avlesninger", cut.Markup);
        Assert.Contains("1000 kWh", cut.Markup);
    }

    [Fact]
    public async Task Index_ReadFailure_ShowsErrorWithRetry()
    {
        await SeedFacilityAsync(1);
        Readings.FailOnRead = true;

        var cut = Render<ChargingIndex>();

        cut.WaitForElement("[role=alert]");
        Assert.Contains("Kunne ikke hente elbillading", cut.Markup);
        Assert.Contains("Prøv igjen", cut.Markup);
    }

    // Charger registration and editing

    [Fact]
    public async Task ChargerCreate_ValidForm_RegistersChargerWithSubMeterInChosenCabinet()
    {
        await Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await Service.RegisterAsync("Testveien 1");

        var cut = Render<ChargerCreate>();
        cut.WaitForElement("#housingUnitId");
        cut.Find("#housingUnitId").Change(unit.Id.ToString());
        cut.Find("#cabinetNumber").Change("2");
        cut.Find("#chargerNumber").Change("P7");
        cut.Find("form").Submit();

        var charger = Assert.Single(await Charging.GetChargersAsync());
        cut.WaitForAssertion(() => Assert.Equal($"/elbillading/{charger.Id}?status=opprettet", CurrentPath));
        Assert.Equal("P7", charger.ChargerNumber);
        Assert.Equal(2, (await Charging.GetSubMeterAsync(charger.Id))!.CabinetNumber);
    }

    [Fact]
    public async Task ChargerCreate_MissingChoices_ShowsFieldMessagesAndStoresNothing()
    {
        await Charging.SetUpFacilityAsync("Ladeanlegg");
        await Service.RegisterAsync("Testveien 1");

        var cut = Render<ChargerCreate>();
        cut.WaitForElement("#housingUnitId");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var messages = string.Join(" ", cut.FindAll(".validation-message").Select(element => element.TextContent));
            Assert.Contains("Velg boenheten", messages);
            Assert.Contains("Velg sikringsskap", messages);
        });
        Assert.Empty(await Charging.GetChargersAsync());
    }

    [Fact]
    public async Task ChargerCreate_OnlyOffersUnitsWithoutCharger()
    {
        await SeedFacilityAsync(1);
        await Service.RegisterAsync("Ledig Vei 5");

        var cut = Render<ChargerCreate>();
        cut.WaitForElement("#housingUnitId");

        var options = cut.FindAll("#housingUnitId option").Select(option => option.TextContent).ToList();
        Assert.Contains("Ledig Vei 5", options);
        Assert.DoesNotContain("Testveien 1", options);
    }

    [Fact]
    public async Task ChargerCreate_UnitFromQuery_IsPreselected()
    {
        await Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await Service.RegisterAsync("Testveien 1");

        Services.GetRequiredService<NavigationManager>().NavigateTo($"/elbillading/ny?boenhet={unit.Id}");
        var cut = Render<ChargerCreate>();
        cut.WaitForElement("#housingUnitId");

        Assert.Equal(unit.Id.ToString(), cut.Find("#housingUnitId").GetAttribute("value"));
    }

    [Fact]
    public async Task ChargerEdit_ChangeStatusToNotInUse_IsSavedAndReadingsStillAccepted()
    {
        await SeedFacilityAsync(1);

        var cut = Render<ChargerEdit>(parameters => parameters.Add(page => page.Id, Chargers[0].Id));
        cut.WaitForElement("#status");
        cut.Find("#status").Change(ChargerStatus.NotInUse.ToString());
        cut.Find("#cabinetNumber").Change("2");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal($"/elbillading/{Chargers[0].Id}?status=endret", CurrentPath));
        var saved = (await Charging.GetChargerAsync(Chargers[0].Id))!;
        Assert.Equal(ChargerStatus.NotInUse, saved.Status);
        Assert.Equal(2, (await SubMeterAsync(0)).CabinetNumber);

        var sub = await SubMeterAsync(0);
        await Charging.RegisterReadingAsync(sub.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 5 });
        Assert.Single(await Charging.GetReadingsAsync(sub.Id));
    }

    [Fact]
    public async Task ChargerEdit_UnknownCharger_ShowsNotFound()
    {
        await SeedFacilityAsync(1);

        var cut = Render<ChargerEdit>(parameters => parameters.Add(page => page.Id, 999));

        cut.WaitForAssertion(() => Assert.Contains("Fant ikke laderen", cut.Markup));
    }

    [Fact]
    public async Task ChargerDetail_ShowsChargerMeterAndReadings()
    {
        await SeedFacilityAsync(1);
        var sub = await SubMeterAsync(0);
        await Charging.RegisterReadingAsync(sub.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 10 });
        await Charging.RegisterReadingAsync(sub.Id, new MeterReadingDetails { ReadAt = Jan2, Kwh = 25.5m });

        var cut = Render<ChargerDetail>(parameters => parameters.Add(page => page.Id, Chargers[0].Id));
        cut.WaitForElement(".data-table");

        Assert.Contains("Testveien 1", cut.Markup);
        Assert.Contains("Sikringsskap", cut.Markup);
        Assert.Contains("25,5 kWh", cut.Markup);
        Assert.Contains("+15,5 kWh", cut.Markup);
        Assert.Contains("Startverdi", cut.Markup);
    }

    // Reading set form

    [Fact]
    public async Task ReadingSetCreate_ShowsMainMeterAndEightSubMetersInTwoCabinets()
    {
        await SeedFacilityAsync(8);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");

        Assert.Equal(9, cut.FindAll("input[id^=kwh-]").Count);
        Assert.Contains("Sikringsskap 1", cut.Markup);
        Assert.Contains("Sikringsskap 2", cut.Markup);
    }

    [Fact]
    public async Task ReadingSetCreate_ShowsPreviousValueAndDifferenceBesideTheField()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find($"#kwh-{MainMeter.Id}").Input("1050,5");

        Assert.Contains("Forrige: 1000 kWh", cut.Find($"#kwh-{MainMeter.Id}").ParentElement!.TextContent);
        Assert.Contains("+50,5 kWh", cut.Find($"#kwh-{MainMeter.Id}").ParentElement!.TextContent);
    }

    [Fact]
    public async Task ReadingSetCreate_PartialSet_StoresOnlyFilledFieldsAndTreatsZeroAsRealValue()
    {
        await SeedFacilityAsync(8);
        var first = await SubMeterAsync(0);
        var second = await SubMeterAsync(1);
        var third = await SubMeterAsync(2);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-10T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find($"#kwh-{first.Id}").Input("0");
        cut.Find($"#kwh-{second.Id}").Input("12,5");
        cut.Find("form").Submit();

        cut.WaitForElement("[role=status] h2");
        Assert.Contains("Avlesningen er registrert", cut.Markup);
        Assert.Contains("ble ikke avlest", cut.Markup);

        Assert.Equal(0m, Assert.Single(await Charging.GetReadingsAsync(first.Id)).Kwh);
        Assert.Equal(12.5m, Assert.Single(await Charging.GetReadingsAsync(second.Id)).Kwh);
        Assert.Empty(await Charging.GetReadingsAsync(third.Id));
        Assert.Equal(500m, Assert.Single(await Charging.GetReadingsAsync(MainMeter.Id)).Kwh);
    }

    [Fact]
    public async Task ReadingSetCreate_NorwegianLocalTimeIsStoredAsUtc()
    {
        await SeedFacilityAsync(1);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-10T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find("form").Submit();

        cut.WaitForElement("[role=status] h2");
        var reading = Assert.Single(await Charging.GetReadingsAsync(MainMeter.Id));
        Assert.Equal(new DateTime(2026, 11, 10, 7, 0, 0, DateTimeKind.Utc), reading.ReadAt);
    }

    [Fact]
    public async Task ReadingSetCreate_TimeWithSecondsFromTheBrowser_IsAccepted()
    {
        await SeedFacilityAsync(1);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-10T08:00:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find("form").Submit();

        cut.WaitForElement("[role=status] h2");
        var reading = Assert.Single(await Charging.GetReadingsAsync(MainMeter.Id));
        Assert.Equal(new DateTime(2026, 11, 10, 7, 0, 0, DateTimeKind.Utc), reading.ReadAt);
    }

    [Fact]
    public async Task ReadingSetCreate_SummerTimeIsConvertedWithTwoHoursOffset()
    {
        await SeedFacilityAsync(1);
        Clock.Advance(TimeSpan.FromDays(300));

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2027-07-01T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find("form").Submit();

        cut.WaitForElement("[role=status] h2");
        var reading = Assert.Single(await Charging.GetReadingsAsync(MainMeter.Id));
        Assert.Equal(new DateTime(2027, 7, 1, 6, 0, 0, DateTimeKind.Utc), reading.ReadAt);
    }

    [Fact]
    public async Task ReadingSetCreate_RepeatedAutumnHour_AsksForChoiceAndStoresChosenTime()
    {
        await SeedFacilityAsync(1);
        Clock.Advance(TimeSpan.FromDays(400));

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2027-10-31T02:30");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");

        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Contains("forekommer to ganger", cut.Markup));
        Assert.Empty(await Charging.GetReadingsAsync(MainMeter.Id));

        cut.Find("#readAt-winter").Change(true);
        cut.Find("form").Submit();

        cut.WaitForElement("[role=status] h2");
        var reading = Assert.Single(await Charging.GetReadingsAsync(MainMeter.Id));
        Assert.Equal(new DateTime(2027, 10, 31, 1, 30, 0, DateTimeKind.Utc), reading.ReadAt);
    }

    [Fact]
    public async Task ReadingSetCreate_TimeSkippedInSpring_IsRefusedWithExplanation()
    {
        await SeedFacilityAsync(1);
        Clock.Advance(TimeSpan.FromDays(200));

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2027-03-28T02:30");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("fantes ikke", cut.Markup));
        Assert.Empty(await Charging.GetReadingsAsync(MainMeter.Id));
    }

    [Fact]
    public async Task ReadingSetCreate_AllFieldsEmpty_ShowsMessageAndStoresNothing()
    {
        await SeedFacilityAsync(8);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("minst én målerstand", cut.Markup));
        Assert.Empty(await Charging.GetAllReadingsAsync());
    }

    [Fact]
    public async Task ReadingSetCreate_InvalidNumber_ShowsMessageAtTheFieldAndStoresNothing()
    {
        await SeedFacilityAsync(8);
        var first = await SubMeterAsync(0);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find($"#kwh-{first.Id}").Input("abc");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains("som et tall", cut.Find($"#kwh-{first.Id}").ParentElement!.TextContent));
        Assert.Empty(await Charging.GetAllReadingsAsync());
    }

    [Fact]
    public async Task ReadingSetCreate_NegativeNumber_IsRefused()
    {
        await SeedFacilityAsync(1);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find($"#kwh-{MainMeter.Id}").Input("-5");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("ikke være negativ", cut.Markup));
        Assert.Empty(await Charging.GetAllReadingsAsync());
    }

    [Fact]
    public async Task ReadingSetCreate_LowerThanPreviousReading_ShowsMessageAtTheMeterAndStoresNothing()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        var first = await SubMeterAsync(0);
        var countBefore = (await Charging.GetAllReadingsAsync()).Count;

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-10T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("1100");
        cut.Find($"#kwh-{first.Id}").Input("50");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
            Assert.Contains("kan ikke gå ned", cut.Find($"#kwh-{first.Id}").ParentElement!.TextContent));
        Assert.Equal(countBefore, (await Charging.GetAllReadingsAsync()).Count);
    }

    [Fact]
    public async Task ReadingSetCreate_FutureTime_IsRefused()
    {
        await SeedFacilityAsync(1);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-25T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("ikke være i fremtiden", cut.Markup));
        Assert.Empty(await Charging.GetAllReadingsAsync());
    }

    [Fact]
    public async Task ReadingSetCreate_DatabaseFailure_ShowsGeneralErrorAndStoresNothing()
    {
        await SeedFacilityAsync(1);
        Readings.FailOnAddRange = true;

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-10T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("500");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Ingenting er lagret", cut.Find("[role=alert]").TextContent));
        Assert.Empty(await Charging.GetAllReadingsAsync());
    }

    [Fact]
    public async Task ReadingSetCreate_AfterSave_ShowsConfirmationAndOffersNewRegistration()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);

        var cut = Render<ReadingSetCreate>();
        cut.WaitForElement("#readAt");
        cut.Find("#readAt").Change("2026-11-10T08:00");
        cut.Find($"#kwh-{MainMeter.Id}").Input("1100");
        cut.Find("form").Submit();

        cut.WaitForElement("[role=status] h2");
        Assert.Contains("+100 kWh", cut.Markup);
        Assert.Contains("Registrer en ny avlesning", cut.Markup);

        cut.FindAll("button").First(button => button.TextContent.Contains("Registrer en ny avlesning")).Click();
        cut.WaitForElement("#readAt");
        Assert.Equal(string.Empty, cut.Find($"#kwh-{MainMeter.Id}").GetAttribute("value") ?? string.Empty);
    }

    // Reconciliation

    [Fact]
    public async Task Reconciliation_CompleteWithEightSubMeters_ShowsDifferenceAndPercent()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        await RegisterFullSetAsync(Jan2, 1100, 110);

        var cut = Render<ReconciliationPage>();
        cut.WaitForElement("[data-testid=reconciliation]");

        Assert.Contains("Fullstendig", cut.Markup);
        Assert.Equal("+20 kWh", cut.Find("[data-testid=difference]").TextContent);
        Assert.Equal("+20,0 %", cut.Find("[data-testid=difference-percent]").TextContent);
        Assert.Equal(9, cut.FindAll("tbody tr").Count);
    }

    [Fact]
    public async Task Reconciliation_OnlySevenSubMeters_IsIncompleteWithoutDifference()
    {
        await SeedFacilityAsync(7);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        await RegisterFullSetAsync(Jan2, 1100, 110);

        var cut = Render<ReconciliationPage>();
        cut.WaitForElement("[data-testid=reconciliation]");

        Assert.Contains("Ufullstendig", cut.Markup);
        Assert.Contains("Bare 7 av 8 undermålere", cut.Markup);
        Assert.Equal("Ikke beregnet", cut.Find("[data-testid=difference]").TextContent);
    }

    [Fact]
    public async Task Reconciliation_MissingSubMeterReading_ListsTheMeterAndDoesNotCountItAsZero()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        var first = await SubMeterAsync(0);
        var values = new Dictionary<int, decimal?>();
        foreach (var meter in (await Charging.GetMetersAsync()).Where(meter => !meter.IsMain && meter.Id != first.Id))
        {
            values[meter.Id] = 110;
        }

        await Charging.RegisterReadingSetAsync(new MeterReadingSetDetails { ReadAt = Jan2, MainMeterKwh = 1100, SubMeterKwh = values });

        var cut = Render<ReconciliationPage>();
        cut.WaitForElement("[data-testid=reconciliation]");

        Assert.Contains("Ufullstendig", cut.Markup);
        Assert.Contains("Testveien 1", cut.Find(".alert-warning").TextContent);
        Assert.Contains("mangler avlesning ved slutt", cut.Find(".alert-warning").TextContent);
        Assert.Equal("Ikke beregnet", cut.Find("[data-testid=difference]").TextContent);
    }

    [Fact]
    public async Task Reconciliation_ApproximateReading_ShowsToleranceExplanationAndActualReadingTimes()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        await RegisterFullSetAsync(Jan2, 1100, 110);
        var first = await SubMeterAsync(0);
        foreach (var reading in await Charging.GetReadingsAsync(first.Id))
        {
            await Charging.DeleteReadingAsync(reading.Id);
        }

        await Charging.RegisterReadingAsync(first.Id, new MeterReadingDetails { ReadAt = Jan1.AddHours(10), Kwh = 101 });
        await Charging.RegisterReadingAsync(first.Id, new MeterReadingDetails { ReadAt = Jan2.AddHours(-10), Kwh = 109 });

        var cut = Render<ReconciliationPage>();
        cut.WaitForElement("[data-testid=reconciliation]");

        Assert.Contains("Tilnærmet", cut.Markup);
        Assert.Contains("24 timer", cut.Markup);
        Assert.Contains("Avlest 01.11.2026 18:00", cut.Markup);
    }

    [Fact]
    public async Task Reconciliation_ChoosingAnotherPeriod_ShowsThatPeriod()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        await RegisterFullSetAsync(Jan2, 1100, 110);
        await RegisterFullSetAsync(Jan2.AddDays(1), 1150, 115);

        var cut = Render<ReconciliationPage>();
        cut.WaitForElement("[data-testid=reconciliation]");
        Assert.Equal(2, cut.FindAll("#period option").Count);
        Assert.Equal("+10 kWh", cut.Find("[data-testid=difference]").TextContent);

        cut.Find("#period").Change("0");

        cut.WaitForAssertion(() => Assert.Equal("+20 kWh", cut.Find("[data-testid=difference]").TextContent));
    }

    [Fact]
    public async Task Reconciliation_WithoutTwoMainReadings_ShowsEmptyState()
    {
        await SeedFacilityAsync(8);

        var cut = Render<ReconciliationPage>();

        cut.WaitForAssertion(() => Assert.Contains("Ingen perioder å avstemme", cut.Markup));
    }

    // History, correction and deletion

    [Fact]
    public async Task MeterHistory_MainMeter_ShowsReadingsNewestFirst()
    {
        await SeedFacilityAsync(1);
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 10 });
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan2, Kwh = 30 });

        var cut = Render<MeterHistoryPage>(parameters => parameters.Add(page => page.MeterId, MainMeter.Id));
        cut.WaitForElement(".data-table");

        var rows = cut.FindAll("tbody tr");
        Assert.Contains("30 kWh", rows[0].TextContent);
        Assert.Contains("10 kWh", rows[1].TextContent);
        Assert.Contains("Hovedmåler", cut.Find("h1").TextContent);
    }

    [Fact]
    public async Task MeterHistory_UnknownMeter_ShowsNotFound()
    {
        await SeedFacilityAsync(1);

        var cut = Render<MeterHistoryPage>(parameters => parameters.Add(page => page.MeterId, 999));

        cut.WaitForAssertion(() => Assert.Contains("Fant ikke måleren", cut.Markup));
    }

    [Fact]
    public async Task Correction_ValidValue_UpdatesReading()
    {
        await SeedFacilityAsync(1);
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 10 });
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan2, Kwh = 30 });

        var cut = Render<MeterReadingsPanel>(parameters => parameters.Add(panel => panel.MeterId, MainMeter.Id));
        cut.WaitForElement(".data-table");
        cut.FindAll("button").First(button => button.TextContent == "Korriger").Click();
        cut.Find($"#correct-kwh-{MainMeter.Id}").Input("35");
        cut.Find(".correction-form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Avlesningen er korrigert", cut.Markup));
        var readings = await Charging.GetReadingsAsync(MainMeter.Id);
        Assert.Equal(35m, readings[1].Kwh);
    }

    [Fact]
    public async Task Correction_ValueBreakingChronology_ShowsMessageAndKeepsReading()
    {
        await SeedFacilityAsync(1);
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 10 });
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan2, Kwh = 30 });

        var cut = Render<MeterReadingsPanel>(parameters => parameters.Add(panel => panel.MeterId, MainMeter.Id));
        cut.WaitForElement(".data-table");
        cut.FindAll("button").First(button => button.TextContent == "Korriger").Click();
        cut.Find($"#correct-kwh-{MainMeter.Id}").Input("5");
        cut.Find(".correction-form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("kan ikke gå ned", cut.Find(".correction-form").TextContent));
        Assert.Equal(30m, (await Charging.GetReadingsAsync(MainMeter.Id))[1].Kwh);
    }

    [Fact]
    public async Task Correction_InvalidNumber_ShowsMessageAtField()
    {
        await SeedFacilityAsync(1);
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 10 });

        var cut = Render<MeterReadingsPanel>(parameters => parameters.Add(panel => panel.MeterId, MainMeter.Id));
        cut.WaitForElement(".data-table");
        cut.FindAll("button").First(button => button.TextContent == "Korriger").Click();
        cut.Find($"#correct-kwh-{MainMeter.Id}").Input("xyz");
        cut.Find(".correction-form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("som et tall", cut.Find(".correction-form").TextContent));
        Assert.Equal(10m, (await Charging.GetReadingsAsync(MainMeter.Id))[0].Kwh);
    }

    [Fact]
    public async Task Delete_RequiresConfirmation_AndCancelKeepsReading()
    {
        await SeedFacilityAsync(1);
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 10 });

        var cut = Render<MeterReadingsPanel>(parameters => parameters.Add(panel => panel.MeterId, MainMeter.Id));
        cut.WaitForElement(".data-table");
        cut.FindAll("button").First(button => button.TextContent == "Slett").Click();

        Assert.Contains("kan ikke angres", cut.Find("[role=alertdialog]").TextContent);
        Assert.Single(await Charging.GetReadingsAsync(MainMeter.Id));

        cut.FindAll("button").First(button => button.TextContent == "Avbryt").Click();
        Assert.Empty(cut.FindAll("[role=alertdialog]"));
        Assert.Single(await Charging.GetReadingsAsync(MainMeter.Id));
    }

    [Fact]
    public async Task Delete_Confirmed_RemovesReading()
    {
        await SeedFacilityAsync(1);
        await Charging.RegisterReadingAsync(MainMeter.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 10 });

        var cut = Render<MeterReadingsPanel>(parameters => parameters.Add(panel => panel.MeterId, MainMeter.Id));
        cut.WaitForElement(".data-table");
        cut.FindAll("button").First(button => button.TextContent == "Slett").Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Ja, slett")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Avlesningen er slettet", cut.Markup));
        Assert.Empty(await Charging.GetReadingsAsync(MainMeter.Id));
    }

    // Unit page and dashboard

    [Fact]
    public async Task UnitDetail_WithCharger_ShowsChargerSectionWithLastReading()
    {
        await SeedFacilityAsync(1);
        var sub = await SubMeterAsync(0);
        await Charging.RegisterReadingAsync(sub.Id, new MeterReadingDetails { ReadAt = Jan1, Kwh = 42 });

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, Units[0].Id));
        cut.WaitForElement("#unit-charging-title");

        var section = cut.Find("#unit-charging-title").ParentElement!.ParentElement!;
        Assert.Contains("42 kWh", section.TextContent);
        Assert.Contains("Lader L1", section.TextContent);
    }

    [Fact]
    public async Task UnitDetail_WithoutCharger_OffersToRegisterOne()
    {
        await Charging.SetUpFacilityAsync("Ladeanlegg");
        var unit = await Service.RegisterAsync("Testveien 9");

        var cut = Render<HousingUnitDetail>(parameters => parameters.Add(page => page.Id, unit.Id));
        cut.WaitForElement("#unit-charging-title");

        var link = cut.Find("a[href^='/elbillading/ny']");
        Assert.Equal($"/elbillading/ny?boenhet={unit.Id}", link.GetAttribute("href"));
    }

    [Fact]
    public async Task Dashboard_ShowsAShortChargingStatusWithTheLatestReconciliation()
    {
        await SeedFacilityAsync(8);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        await RegisterFullSetAsync(Jan2, 1100, 110);

        var cut = Render<Dashboard>();
        cut.WaitForElement("[data-testid=charging-status]");

        var section = cut.FindByTestId("charging-status");
        Assert.Contains("+20 kWh", section.TextContent);
        Assert.DoesNotContain("ladere registrert", section.TextContent);
    }

    [Fact]
    public async Task Dashboard_ChargingStatusLinksToChargingAndToRegisteringAReading()
    {
        await SeedFacilityAsync(8);

        var cut = Render<Dashboard>();
        cut.WaitForElement("[data-testid=charging-status]");

        var links = cut.FindByTestId("charging-status").QuerySelectorAll("a").ToDictionary(link => link.TextContent, link => link.GetAttribute("href"));
        Assert.Equal("/elbillading", links["Elbillading"]);
        Assert.Equal("/elbillading/avlesning", links["Registrer avlesning"]);
    }

    [Fact]
    public async Task Dashboard_WithoutAFacility_SaysSoAndOnlyLinksToCharging()
    {
        await Service.RegisterAsync("Storgata 1A");

        var cut = Render<Dashboard>();
        cut.WaitForElement("[data-testid=charging-status]");

        var section = cut.FindByTestId("charging-status");
        Assert.Contains("Ladeanlegget er ikke satt opp", section.TextContent);
        Assert.DoesNotContain("Registrer avlesning", section.TextContent);
    }

    [Fact]
    public async Task Dashboard_IncompleteReconciliation_SaysSo()
    {
        await SeedFacilityAsync(3);
        await RegisterFullSetAsync(Jan1, 1000, 100);
        await RegisterFullSetAsync(Jan2, 1100, 110);

        var cut = Render<Dashboard>();
        cut.WaitForElement("[data-testid=charging-status]");

        Assert.Contains("ufullstendig", cut.FindByTestId("charging-status").TextContent);
    }
}
