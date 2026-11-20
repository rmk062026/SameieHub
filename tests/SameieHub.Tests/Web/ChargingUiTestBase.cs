using SameieHub.Domain;

namespace SameieHub.Tests.Web;

/// <summary>Sets up a fictitious charging facility for the charging page tests.</summary>
public abstract class ChargingUiTestBase : UiTestBase
{
    protected List<HousingUnit> Units { get; } = [];

    protected List<EvCharger> Chargers { get; } = [];

    protected Meter MainMeter { get; private set; } = default!;

    /// <summary>Sets up the facility with <paramref name="chargerCount"/> chargers: the first four in cabinet 1, the rest in cabinet 2.</summary>
    protected async Task SeedFacilityAsync(int chargerCount = 8)
    {
        await Charging.SetUpFacilityAsync("Ladeanlegg");
        MainMeter = (await Charging.GetMainMeterAsync())!;

        for (var index = 1; index <= chargerCount; index++)
        {
            var unit = await Service.RegisterAsync($"Testveien {index}");
            Units.Add(unit);

            var cabinet = index <= 4 ? 1 : 2;
            var charger = await Charging.RegisterChargerAsync(unit.Id, new EvChargerDetails { ChargerNumber = $"L{index}" }, cabinet);
            Chargers.Add(charger);
        }
    }

    protected async Task<Meter> SubMeterAsync(int chargerIndex)
    {
        return (await Charging.GetSubMeterAsync(Chargers[chargerIndex].Id))!;
    }

    /// <summary>Registers a complete reading set: the main meter and every sub meter at the same time.</summary>
    protected async Task RegisterFullSetAsync(DateTime readAtUtc, decimal mainKwh, decimal subKwh)
    {
        var subValues = new Dictionary<int, decimal?>();
        foreach (var meter in (await Charging.GetMetersAsync()).Where(meter => !meter.IsMain))
        {
            subValues[meter.Id] = subKwh;
        }

        await Charging.RegisterReadingSetAsync(new MeterReadingSetDetails
        {
            ReadAt = readAtUtc,
            MainMeterKwh = mainKwh,
            SubMeterKwh = subValues
        });
    }
}
