using SameieHub.Domain;

namespace SameieHub.Infrastructure.Demo;

/// <summary>One fictional charger of the demo and the meter values that belong to it.</summary>
/// <param name="UnitNumber">The housing unit, for example "10A".</param>
/// <param name="Cabinet">The fuse cabinet of the sub meter. This distribution only applies to the demo.</param>
/// <param name="StartKwh">The accumulated meter value at the first reading.</param>
/// <param name="PeriodKwh">The consumption in each of the four periods between the five reading rounds.</param>
public record DemoChargerPlan(
    string UnitNumber,
    int Cabinet,
    ChargerStatus Status,
    bool HasElectricVehicle,
    string Model,
    decimal StartKwh,
    decimal[] PeriodKwh);

/// <summary>
/// The fixed content of the demo charging facility. The seeder creates it, and the completeness check compares
/// the database with it, so the two can never disagree. Meter values are accumulated; the consumption of a period
/// is the difference between two readings and is never stored.
/// </summary>
public static class DemoChargingPlan
{
    public const string FacilityName = "Fiktivdalen ladeanlegg";
    public const string Manufacturer = "Fiktiv Ladeteknikk AS";

    /// <summary>The reading rounds, as days before the day the demo is created. Each round is read at 08:00 Norwegian time.</summary>
    public static readonly int[] RoundDaysAgo = [110, 80, 50, 25, 4];

    /// <summary>Round 2 (index 1) is only partly read: the sub meter of 14B is not read, and the one of 16A is added the next day.</summary>
    public const int IncompleteRoundIndex = 1;

    public const string NotReadUnitNumber = "14B";
    public const string CompletedLaterUnitNumber = "16A";

    /// <summary>The charger of 14A is set to temporarily disabled after this round (index 3), but its meter is still read.</summary>
    public const int DisabledAfterRoundIndex = 3;
    public const string DisabledUnitNumber = "14A";

    public const decimal MainStartKwh = 48210.0m;

    /// <summary>
    /// The consumption of the main meter in each period. In the third period the main meter measured 1250 kWh and
    /// the sub meters 1220 kWh, a difference of 30 kWh (2,4 %). Other things also use electricity, such as common lighting.
    /// </summary>
    public static readonly decimal[] MainPeriodKwh = [1099m, 1154m, 1250m, 1080m];

    public static readonly DemoChargerPlan[] Chargers =
    [
        new("10A", 1, ChargerStatus.InUse, true, "FL-7", 5120.4m, [210m, 230m, 280m, 255m]),
        new("10B", 1, ChargerStatus.NotInUse, false, "FL-7", 14.2m, [0m, 0m, 0m, 0m]),
        new("12A", 1, ChargerStatus.InUse, true, "FL-11", 4380.0m, [190m, 205m, 245m, 230m]),
        new("12B", 1, ChargerStatus.NotInUse, false, "FL-7", 0m, [0m, 0m, 0m, 0m]),
        new("14A", 2, ChargerStatus.InUse, true, "FL-7", 3890.6m, [160m, 140m, 100m, 0m]),
        new("14B", 2, ChargerStatus.NotInUse, false, "FL-7", 12.4m, [0m, 0m, 0m, 0m]),
        new("16A", 2, ChargerStatus.InUse, true, "FL-11", 7210.3m, [340m, 365m, 390m, 370m]),
        new("16B", 2, ChargerStatus.InUse, true, "FL-7", 2950.8m, [180m, 195m, 205m, 210m])
    ];

    public static string ChargerNumber(string unitNumber) => "L" + unitNumber;

    /// <summary>The accumulated main meter values, one per round.</summary>
    public static List<decimal> MainValues()
    {
        return AccumulatedValues(MainStartKwh, MainPeriodKwh);
    }

    /// <summary>The accumulated values of a sub meter, one per round that the meter was read in the first place.</summary>
    public static List<decimal> SubMeterValues(DemoChargerPlan charger)
    {
        var values = AccumulatedValues(charger.StartKwh, charger.PeriodKwh);
        if (charger.UnitNumber == NotReadUnitNumber)
        {
            values.RemoveAt(IncompleteRoundIndex);
        }

        return values;
    }

    private static List<decimal> AccumulatedValues(decimal start, decimal[] periodKwh)
    {
        var values = new List<decimal> { start };
        var total = start;
        foreach (var kwh in periodKwh)
        {
            total += kwh;
            values.Add(total);
        }

        return values;
    }
}
