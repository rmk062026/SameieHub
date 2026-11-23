using SameieHub.Domain;
using SameieHub.Infrastructure.Demo;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// Read-only checks of the charging data in the seeded demo. They share one seeded database
/// (<see cref="SeededDemoFixture"/>) and never change it. Tests that create, change, reset or break the demo
/// are in <see cref="DemoChargingSeedTests"/> and have their own database.
/// </summary>
public class DemoChargingContentTests : IClassFixture<SeededDemoFixture>
{
    private static readonly string[] UnitNumbers = ["10A", "10B", "12A", "12B", "14A", "14B", "16A", "16B"];

    private readonly SeededDemoFixture _demo;

    public DemoChargingContentTests(SeededDemoFixture demo)
    {
        _demo = demo;
    }

    private async Task<Dictionary<string, EvCharger>> ChargersByUnitNumberAsync()
    {
        var units = await _demo.UnitsAsync();
        var chargers = await _demo.ChargersAsync();
        var result = new Dictionary<string, EvCharger>();
        foreach (var unit in units)
        {
            var number = unit.Address.Replace(DemoDatabaseSeeder.AddressPrefix, string.Empty);
            result[number] = chargers.Single(charger => charger.HousingUnitId == unit.Id);
        }

        return result;
    }

    private async Task<List<ReconciliationPeriod>> PeriodsAsync()
    {
        var main = (await _demo.MainMeterAsync())!;

        return ChargingReconciler.MainMeterPeriods(await _demo.ReadingsAsync(main.Id));
    }

    private async Task<ChargingReconciliation> ReconcilePeriodAsync(int index)
    {
        var period = (await PeriodsAsync())[index];

        return await _demo.ReconcileAsync(period.From, period.To);
    }

    [Fact]
    public async Task Seed_CreatesOneFacilityOneMainMeterEightChargersAndEightSubMeters()
    {
        Assert.NotNull(await _demo.FacilityAsync());
        var meters = await _demo.MetersAsync();
        Assert.Single(meters, meter => meter.IsMain);
        Assert.Equal(8, meters.Count(meter => !meter.IsMain));
        Assert.Equal(8, (await _demo.ChargersAsync()).Count);
    }

    [Fact]
    public async Task Seed_PutsFourSubMetersInEachCabinetWithTheDemoDistribution()
    {
        var chargers = await ChargersByUnitNumberAsync();
        var cabinetOne = new[] { "10A", "10B", "12A", "12B" };
        foreach (var number in UnitNumbers)
        {
            var subMeter = (await _demo.SubMeterAsync(chargers[number].Id))!;
            Assert.Equal(cabinetOne.Contains(number) ? 1 : 2, subMeter.CabinetNumber);
        }

        var meters = await _demo.MetersAsync();
        Assert.Equal(4, meters.Count(meter => meter.CabinetNumber == 1));
        Assert.Equal(4, meters.Count(meter => meter.CabinetNumber == 2));
    }

    [Fact]
    public async Task Seed_ConnectsEachHousingUnitToItsOwnChargerAndSubMeter()
    {
        var units = await _demo.UnitsAsync();
        var chargers = await _demo.ChargersAsync();
        var meters = await _demo.MetersAsync();

        Assert.Equal(8, chargers.Select(charger => charger.HousingUnitId).Distinct().Count());
        foreach (var unit in units)
        {
            var charger = chargers.Single(existing => existing.HousingUnitId == unit.Id);
            Assert.Single(meters, meter => meter.EvChargerId == charger.Id);
            Assert.Contains(unit.UnitNumber!, charger.ChargerNumber);
        }
    }

    [Fact]
    public async Task Seed_GivesAllThreeChargerStatusesAndSomeHouseholdsWithoutAnElectricVehicle()
    {
        var chargers = await ChargersByUnitNumberAsync();
        Assert.Equal(4, chargers.Values.Count(charger => charger.Status == ChargerStatus.InUse));
        Assert.Equal(3, chargers.Values.Count(charger => charger.Status == ChargerStatus.NotInUse));
        Assert.Single(chargers.Values, charger => charger.Status == ChargerStatus.TemporarilyDisabled);
        Assert.Equal(ChargerStatus.TemporarilyDisabled, chargers["14A"].Status);
        Assert.True(chargers.Values.Count(charger => charger.HasElectricVehicle == false) >= 2);
    }

    [Fact]
    public async Task Seed_ChargerDetailsAreFictional()
    {
        foreach (var charger in await _demo.ChargersAsync())
        {
            Assert.StartsWith("Fiktiv", charger.Manufacturer);
            Assert.DoesNotContain("@", charger.Notes ?? string.Empty);
        }
    }

    [Fact]
    public async Task Seed_HousingUnitNotesDoNotContradictTheChargerData()
    {
        foreach (var unit in await _demo.UnitsAsync())
        {
            Assert.Contains("Carport", unit.Notes);
            Assert.DoesNotContain("Elbillader:", unit.Notes);
            Assert.DoesNotContain("midlertidig deaktivert", unit.Notes);
        }
    }

    [Fact]
    public async Task Seed_CreatesFortyFourReadingsAndEveryMeterStartsWithAStartValue()
    {
        var readings = await _demo.AllReadingsAsync();
        Assert.Equal(44, readings.Count);
        foreach (var meter in await _demo.MetersAsync())
        {
            var meterReadings = await _demo.ReadingsAsync(meter.Id);
            Assert.NotEmpty(meterReadings);
            var consumption = MeterHistory.TotalDocumented(meterReadings);
            Assert.NotNull(consumption);
        }
    }

    [Fact]
    public async Task Seed_ReadingsAreAccumulatedAndFollowTheChronologyRules()
    {
        var now = _demo.Now;
        foreach (var meter in await _demo.MetersAsync())
        {
            var readings = await _demo.ReadingsAsync(meter.Id);
            for (var index = 0; index < readings.Count; index++)
            {
                Assert.True(readings[index].ReadAt <= now);
                Assert.True(readings[index].RegisteredAt <= now);
                if (index > 0)
                {
                    Assert.True(readings[index].ReadAt > readings[index - 1].ReadAt);
                    Assert.True(readings[index].Kwh >= readings[index - 1].Kwh, "Accumulated values never fall.");
                }
            }
        }
    }

    [Fact]
    public async Task Seed_SpreadsTheReadingsOverTheLastMonthsInFiveRounds()
    {
        var main = (await _demo.MainMeterAsync())!;
        var times = (await _demo.ReadingsAsync(main.Id)).Select(reading => reading.ReadAt).ToList();
        Assert.Equal(5, times.Count);
        var now = _demo.Now;
        Assert.InRange((now - times[0]).TotalDays, 90, 150);
        Assert.InRange((now - times[^1]).TotalDays, 1, 10);
    }

    [Fact]
    public async Task Seed_HasAtLeastThreeCompleteRoundsAndOneIncompleteRound()
    {
        var byTime = (await _demo.AllReadingsAsync()).GroupBy(reading => reading.ReadAt).ToList();
        Assert.True(byTime.Count(round => round.Count() == 9) >= 3);
        Assert.Contains(byTime, round => round.Count() < 9);
    }

    [Fact]
    public async Task Seed_ARoundWithMissingReadingsIsCompletedLater()
    {
        var secondRound = (await _demo.AllReadingsAsync())
            .GroupBy(reading => reading.ReadAt)
            .OrderBy(round => round.Key)
            .ElementAt(1)
            .ToList();

        var latest = secondRound.Max(reading => reading.RegisteredAt);
        var earliest = secondRound.Min(reading => reading.RegisteredAt);
        Assert.True(latest > earliest, "One reading of the round was registered later than the others.");
        Assert.Equal(1, secondRound.Count(reading => reading.RegisteredAt == latest));
    }

    [Fact]
    public async Task Seed_HasFourPeriodsBetweenTheMainMeterReadings()
    {
        Assert.Equal(4, (await PeriodsAsync()).Count);
    }

    [Fact]
    public async Task Seed_TheDocumentedPeriodHas1250MainAnd1220SubMeterKwhWithADifferenceOf30Kwh()
    {
        var reconciliation = await ReconcilePeriodAsync(2);

        Assert.True(reconciliation.IsComplete);
        Assert.Equal(1250m, reconciliation.MainKwh);
        Assert.Equal(1220m, reconciliation.SubMeterKwh);
        Assert.Equal(30m, reconciliation.Difference);
        Assert.Equal(2.4m, decimal.Round(reconciliation.DifferencePercent!.Value, 1));
        Assert.False(reconciliation.HasApproximateReadings);
    }

    [Fact]
    public async Task Seed_TheLatestPeriodIsCompleteWithASmallPositiveDifference()
    {
        var reconciliation = await _demo.ReconcileLatestPeriodAsync();

        Assert.NotNull(reconciliation);
        Assert.True(reconciliation.IsComplete);
        Assert.InRange(reconciliation.Difference!.Value, 1m, 50m);
        Assert.InRange(reconciliation.DifferencePercent!.Value, 0.1m, 5m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Seed_TheTwoOldestPeriodsAreIncompleteBecauseOneMeterWasNotRead(int periodIndex)
    {
        var reconciliation = await ReconcilePeriodAsync(periodIndex);

        Assert.False(reconciliation.IsComplete);
        Assert.Null(reconciliation.Difference);
        var missing = Assert.Single(reconciliation.MissingMeters);
        var chargers = await ChargersByUnitNumberAsync();
        Assert.Equal(chargers["14B"].Id, missing.Charger!.Id);
    }

    [Fact]
    public async Task Seed_TheMissingReadingThatWasCompletedLaterIsDocumentedInTheSecondRound()
    {
        var chargers = await ChargersByUnitNumberAsync();
        var meter = (await _demo.SubMeterAsync(chargers["16A"].Id))!;

        var readings = await _demo.ReadingsAsync(meter.Id);

        Assert.Equal(5, readings.Count);
    }

    [Fact]
    public async Task Seed_SubMeterConsumptionAddsUpToTheDocumentedSum()
    {
        var reconciliation = await ReconcilePeriodAsync(2);
        var chargers = await ChargersByUnitNumberAsync();
        var expected = new Dictionary<string, decimal>
        {
            ["10A"] = 280, ["10B"] = 0, ["12A"] = 245, ["12B"] = 0, ["14A"] = 100, ["14B"] = 0, ["16A"] = 390, ["16B"] = 205
        };

        foreach (var (number, kwh) in expected)
        {
            var line = reconciliation.SubMeters.Single(existing => existing.Charger!.Id == chargers[number].Id);
            Assert.Equal(kwh, line.Consumption.Kwh);
        }
    }

    [Fact]
    public async Task Seed_TheDeactivatedChargerKeepsItsHistoricalConsumption()
    {
        var chargers = await ChargersByUnitNumberAsync();
        Assert.Equal(ChargerStatus.TemporarilyDisabled, chargers["14A"].Status);
        var meter = (await _demo.SubMeterAsync(chargers["14A"].Id))!;

        var readings = await _demo.ReadingsAsync(meter.Id);

        Assert.Equal(5, readings.Count);
        Assert.Equal(400m, readings[^2].Kwh - readings[0].Kwh);
        Assert.Equal(readings[^2].Kwh, readings[^1].Kwh);
    }

    [Fact]
    public async Task Seed_AChargerWithoutAnElectricVehicleHasZeroDocumentedConsumptionInAPeriod()
    {
        var chargers = await ChargersByUnitNumberAsync();
        Assert.False(chargers["10B"].HasElectricVehicle);
        var reconciliation = await ReconcilePeriodAsync(2);

        var line = reconciliation.SubMeters.Single(existing => existing.Charger!.Id == chargers["10B"].Id);

        Assert.Equal(MeterConsumptionState.Documented, line.Consumption.State);
        Assert.Equal(0m, line.Consumption.Kwh);
    }

    [Fact]
    public async Task Seed_AMeterCanHaveARealStartValueOfZero()
    {
        var chargers = await ChargersByUnitNumberAsync();
        var meter = (await _demo.SubMeterAsync(chargers["12B"].Id))!;

        var first = (await _demo.ReadingsAsync(meter.Id))[0];

        Assert.Equal(0m, first.Kwh);
    }

    [Fact]
    public async Task Seed_PeriodConsumptionIsCalculatedFromAccumulatedValuesNotStoredAsReadings()
    {
        var main = (await _demo.MainMeterAsync())!;
        var readings = await _demo.ReadingsAsync(main.Id);

        Assert.All(readings, reading => Assert.True(reading.Kwh > 10000m));
        Assert.Equal(1250m, readings[3].Kwh - readings[2].Kwh);
    }
}
