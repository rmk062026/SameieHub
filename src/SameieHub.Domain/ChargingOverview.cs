namespace SameieHub.Domain;

/// <summary>A charger with its housing unit, sub meter and the latest reading of the sub meter.</summary>
public record ChargingOverviewRow(HousingUnit Unit, EvCharger Charger, Meter? SubMeter, MeterReading? LastReading);

/// <summary>
/// What the charging overview page shows: counts per charger status, one row per charger, the housing units that
/// have no charger, and the main meter with its latest reading. Calculated from existing data, nothing is stored.
/// </summary>
public record ChargingOverview
{
    private static readonly StringComparer AddressComparer =
        StringComparer.Create(new System.Globalization.CultureInfo("nb-NO"), ignoreCase: true);

    public int RegisteredChargers { get; init; }

    public int InUse { get; init; }

    public int NotInUse { get; init; }

    public int TemporarilyDisabled { get; init; }

    public IReadOnlyList<ChargingOverviewRow> Rows { get; init; } = [];

    public IReadOnlyList<HousingUnit> UnitsWithoutCharger { get; init; } = [];

    public Meter? MainMeter { get; init; }

    public MeterReading? MainMeterLastReading { get; init; }

    public static ChargingOverview Create(
        IEnumerable<HousingUnit> housingUnits,
        IEnumerable<EvCharger> chargers,
        IEnumerable<Meter> meters,
        IEnumerable<MeterReading> readings)
    {
        var chargerList = chargers.ToList();
        var meterList = meters.ToList();
        var readingList = readings.ToList();

        var rows = new List<ChargingOverviewRow>();
        var unitsWithoutCharger = new List<HousingUnit>();

        foreach (var unit in housingUnits)
        {
            var charger = chargerList.FirstOrDefault(existing => existing.HousingUnitId == unit.Id);
            if (charger == null)
            {
                unitsWithoutCharger.Add(unit);
                continue;
            }

            var subMeter = meterList.FirstOrDefault(meter => meter.EvChargerId == charger.Id);
            rows.Add(new ChargingOverviewRow(unit, charger, subMeter, subMeter == null ? null : LastReadingOf(subMeter, readingList)));
        }

        var main = meterList.FirstOrDefault(meter => meter.IsMain);

        return new ChargingOverview
        {
            RegisteredChargers = chargerList.Count,
            InUse = chargerList.Count(charger => charger.Status == ChargerStatus.InUse),
            NotInUse = chargerList.Count(charger => charger.Status == ChargerStatus.NotInUse),
            TemporarilyDisabled = chargerList.Count(charger => charger.Status == ChargerStatus.TemporarilyDisabled),
            Rows = rows.OrderBy(row => row.Unit.Address, AddressComparer).ToList(),
            UnitsWithoutCharger = unitsWithoutCharger.OrderBy(unit => unit.Address, AddressComparer).ToList(),
            MainMeter = main,
            MainMeterLastReading = main == null ? null : LastReadingOf(main, readingList)
        };
    }

    private static MeterReading? LastReadingOf(Meter meter, List<MeterReading> readings)
    {
        var sorted = MeterHistory.Sort(readings.Where(reading => reading.MeterId == meter.Id));

        return sorted.Count == 0 ? null : sorted[^1];
    }
}
