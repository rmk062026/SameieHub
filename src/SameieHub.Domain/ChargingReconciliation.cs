namespace SameieHub.Domain;

/// <summary>One meter in a reconciliation, with its charger when it is a sub meter.</summary>
public record ReconciliationLine(Meter Meter, EvCharger? Charger, MeterConsumption Consumption);

/// <summary>A period between two readings of the main meter.</summary>
public record ReconciliationPeriod(DateTime From, DateTime To);

/// <summary>
/// Compares what the main meter measured with the sum of the sub meters in the same period.
/// Nothing is estimated: when a meter has no reading near the start or the end of the period, the
/// reconciliation is incomplete and has no difference. A meter without readings is missing, never zero.
/// </summary>
public record ChargingReconciliation
{
    /// <summary>UTC.</summary>
    public required DateTime From { get; init; }

    /// <summary>UTC.</summary>
    public required DateTime To { get; init; }

    public required TimeSpan Tolerance { get; init; }

    public ReconciliationLine? Main { get; init; }

    public IReadOnlyList<ReconciliationLine> SubMeters { get; init; } = [];

    /// <summary>The consumption of the main meter, or null when it is not documented.</summary>
    public decimal? MainKwh { get; init; }

    /// <summary>The sum of the sub meters that are documented. Meters without documentation are not included.</summary>
    public decimal SubMeterKwh { get; init; }

    /// <summary>The meters (main and sub) that have no documented consumption in the period.</summary>
    public IReadOnlyList<ReconciliationLine> MissingMeters { get; init; } = [];

    /// <summary>True when the main meter and every sub meter are documented in the period.</summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// True when at least one documented meter was read at a time other than exactly the start or end of the period.
    /// The figures are then for the intervals between the readings, not for exactly the period.
    /// </summary>
    public bool HasApproximateReadings { get; init; }

    /// <summary>Main meter consumption minus the sum of the sub meters. Null when the reconciliation is incomplete.</summary>
    public decimal? Difference { get; init; }

    /// <summary>The difference as a percent of the main meter consumption. Null when incomplete or when the main meter consumption is zero.</summary>
    public decimal? DifferencePercent { get; init; }
}

public static class ChargingReconciler
{
    /// <summary>
    /// Difference = main meter consumption − sum of the sub meter consumption.
    /// Percent = difference / main meter consumption × 100, when the main meter consumption is not zero.
    /// Every meter uses the reading nearest to the start and the end of the period, within the tolerance.
    /// </summary>
    public static ChargingReconciliation Reconcile(
        IEnumerable<Meter> meters,
        IEnumerable<EvCharger> chargers,
        IEnumerable<MeterReading> readings,
        DateTime from,
        DateTime to,
        TimeSpan tolerance)
    {
        var chargerById = chargers.ToDictionary(charger => charger.Id);
        var readingList = readings.ToList();
        var lines = new List<ReconciliationLine>();

        foreach (var meter in meters.OrderBy(existing => existing.Id))
        {
            var meterReadings = readingList.Where(reading => reading.MeterId == meter.Id);
            var consumption = MeterHistory.ConsumptionBetween(meterReadings, from, to, tolerance);
            EvCharger? charger = null;
            if (meter.EvChargerId != null)
            {
                chargerById.TryGetValue(meter.EvChargerId.Value, out charger);
            }

            lines.Add(new ReconciliationLine(meter, charger, consumption));
        }

        var main = lines.FirstOrDefault(line => line.Meter.IsMain);
        var subMeters = lines.Where(line => !line.Meter.IsMain).ToList();
        var missing = lines.Where(line => line.Consumption.State != MeterConsumptionState.Documented).ToList();

        var subMeterKwh = 0m;
        foreach (var line in subMeters)
        {
            subMeterKwh += line.Consumption.Kwh ?? 0m;
        }

        var mainKwh = main?.Consumption.Kwh;
        var isComplete = main != null && subMeters.Count > 0 && missing.Count == 0;
        decimal? difference = isComplete ? mainKwh - subMeterKwh : null;
        decimal? percent = isComplete && mainKwh != 0m ? difference / mainKwh * 100m : null;

        var hasApproximateReadings = lines.Any(line => line.Consumption.State == MeterConsumptionState.Documented && line.Consumption.IsApproximate);

        return new ChargingReconciliation
        {
            From = from,
            To = to,
            Tolerance = tolerance,
            Main = main,
            SubMeters = subMeters,
            MainKwh = mainKwh,
            SubMeterKwh = subMeterKwh,
            MissingMeters = missing,
            IsComplete = isComplete,
            HasApproximateReadings = hasApproximateReadings,
            Difference = difference,
            DifferencePercent = percent
        };
    }

    /// <summary>The periods between consecutive readings of the main meter, oldest first.</summary>
    public static List<ReconciliationPeriod> MainMeterPeriods(IEnumerable<MeterReading> mainMeterReadings)
    {
        var sorted = MeterHistory.Sort(mainMeterReadings);
        var periods = new List<ReconciliationPeriod>();

        for (var index = 1; index < sorted.Count; index++)
        {
            periods.Add(new ReconciliationPeriod(sorted[index - 1].ReadAt, sorted[index].ReadAt));
        }

        return periods;
    }
}
