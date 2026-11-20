using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

/// <summary>Sets up the physical charging facility (one main meter, eight chargers with sub meters) in a test database.</summary>
internal sealed class ChargingSetup
{
    public required ChargingFacility Facility { get; init; }

    public required Meter MainMeter { get; init; }

    public required IReadOnlyList<HousingUnit> Units { get; init; }

    public required IReadOnlyList<EvCharger> Chargers { get; init; }

    public required IReadOnlyList<Meter> SubMeters { get; init; }

    /// <summary>The test clock starts at 2026-11-18 10:30 UTC (11:30 in Norway).</summary>
    public static DateTime Now { get; } = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

    public static DateTime At(int daysAgo, int hour = 8) => Now.Date.AddDays(-daysAgo).AddHours(hour);

    public static async Task<ChargingSetup> CreateAsync(SqliteTestDatabase database, int chargerCount = 8)
    {
        var facility = await database.Charging.SetUpFacilityAsync("Felles ladeanlegg");
        var main = (await database.Charging.GetMainMeterAsync())!;

        var units = new List<HousingUnit>();
        var chargers = new List<EvCharger>();
        var subMeters = new List<Meter>();

        for (var number = 1; number <= chargerCount; number++)
        {
            var unit = await database.Service.RegisterAsync($"Fiktivdalen {number}");
            var charger = await database.Charging.RegisterChargerAsync(
                unit.Id,
                new EvChargerDetails { ChargerNumber = $"L{number}", Model = "EL-7" },
                number <= 4 ? 1 : 2);

            units.Add(unit);
            chargers.Add(charger);
            subMeters.Add((await database.Charging.GetSubMeterAsync(charger.Id))!);
        }

        return new ChargingSetup { Facility = facility, MainMeter = main, Units = units, Chargers = chargers, SubMeters = subMeters };
    }

    /// <summary>A reading set: the main meter and the sub meters in order. A null value means the meter is not read.</summary>
    public MeterReadingSetDetails Set(int daysAgo, decimal? main, params decimal?[] subs)
    {
        var values = new Dictionary<int, decimal?>();
        for (var index = 0; index < subs.Length; index++)
        {
            values[SubMeters[index].Id] = subs[index];
        }

        return new MeterReadingSetDetails { ReadAt = At(daysAgo), MainMeterKwh = main, SubMeterKwh = values };
    }

    public static decimal?[] Same(decimal value, int count = 8) => Enumerable.Repeat((decimal?)value, count).ToArray();
}
