namespace SameieHub.Domain;

/// <summary>The consumption between two consecutive readings of one meter.</summary>
public record ConsumptionInterval(MeterReading From, MeterReading To)
{
    public decimal Kwh => To.Kwh - From.Kwh;

    public TimeSpan Duration => To.ReadAt - From.ReadAt;
}

public enum MeterConsumptionState
{
    /// <summary>There is a reading near the start and a different, later reading near the end.</summary>
    Documented,

    NoReadings,

    /// <summary>There is no reading near the start of the period.</summary>
    MissingStart,

    /// <summary>There is no reading near the end of the period.</summary>
    MissingEnd,

    MissingBoth,

    /// <summary>The same reading is nearest to both the start and the end, so nothing is documented between them.</summary>
    SameReading
}

/// <summary>
/// The consumption of one meter in a period, from the reading nearest to the start to the reading nearest
/// to the end. <see cref="Kwh"/> is null unless the state is <see cref="MeterConsumptionState.Documented"/>.
/// </summary>
public record MeterConsumption(MeterConsumptionState State, MeterReading? StartReading, MeterReading? EndReading)
{
    public decimal? Kwh => State == MeterConsumptionState.Documented ? EndReading!.Kwh - StartReading!.Kwh : null;

    /// <summary>
    /// True when a reading used is not at exactly the start or end of the period (it was found within the tolerance).
    /// The consumption is then for the interval between the two readings, which is a little different from the period.
    /// </summary>
    public bool IsApproximate { get; init; }
}

/// <summary>
/// The rules and calculations for the readings of one meter. The main meter and the sub meters use the
/// same rules, so they exist in one place. Meter values are accumulated: only differences between
/// readings are consumption.
/// </summary>
public static class MeterHistory
{
    /// <summary>
    /// How far from the start or end of a period a reading may be, to count as the reading for that time.
    /// </summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromHours(24);

    /// <summary>The readings in time order. Equal times are ordered by Id so the order is always the same.</summary>
    public static List<MeterReading> Sort(IEnumerable<MeterReading> readings)
    {
        return readings.OrderBy(reading => reading.ReadAt).ThenBy(reading => reading.Id).ToList();
    }

    /// <summary>
    /// The consumption between each pair of consecutive readings. The first reading is a start value and has
    /// no interval of its own.
    /// </summary>
    public static List<ConsumptionInterval> BuildIntervals(IEnumerable<MeterReading> readings)
    {
        var sorted = Sort(readings);
        var intervals = new List<ConsumptionInterval>();

        for (var index = 1; index < sorted.Count; index++)
        {
            intervals.Add(new ConsumptionInterval(sorted[index - 1], sorted[index]));
        }

        return intervals;
    }

    /// <summary>The documented consumption from the first to the last reading, or null with fewer than two readings.</summary>
    public static decimal? TotalDocumented(IEnumerable<MeterReading> readings)
    {
        var sorted = Sort(readings);
        if (sorted.Count < 2)
        {
            return null;
        }

        return sorted[^1].Kwh - sorted[0].Kwh;
    }

    /// <summary>The reading nearest to the time, within the tolerance. The earlier reading wins a tie.</summary>
    public static MeterReading? FindNearest(IEnumerable<MeterReading> readings, DateTime time, TimeSpan tolerance)
    {
        MeterReading? nearest = null;
        var nearestDistance = TimeSpan.MaxValue;

        // The readings are sorted by time, so on a tie the earlier reading is kept.
        foreach (var reading in Sort(readings))
        {
            var distance = (reading.ReadAt - time).Duration();

            if (distance <= tolerance && distance < nearestDistance)
            {
                nearest = reading;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    public static MeterConsumption ConsumptionBetween(IEnumerable<MeterReading> readings, DateTime from, DateTime to, TimeSpan tolerance)
    {
        var all = readings.ToList();
        if (all.Count == 0)
        {
            return new MeterConsumption(MeterConsumptionState.NoReadings, null, null);
        }

        var start = FindNearest(all, from, tolerance);
        var end = FindNearest(all, to, tolerance);

        if (start == null && end == null)
        {
            return new MeterConsumption(MeterConsumptionState.MissingBoth, null, null);
        }

        if (start == null)
        {
            return new MeterConsumption(MeterConsumptionState.MissingStart, null, end);
        }

        if (end == null)
        {
            return new MeterConsumption(MeterConsumptionState.MissingEnd, start, null);
        }

        if (start.ReadAt >= end.ReadAt)
        {
            return new MeterConsumption(MeterConsumptionState.SameReading, start, end);
        }

        return new MeterConsumption(MeterConsumptionState.Documented, start, end)
        {
            IsApproximate = start.ReadAt != from || end.ReadAt != to
        };
    }

    /// <summary>
    /// Checks that a reading fits among the other readings of the meter: no other reading at the same time,
    /// and not lower than an earlier reading or higher than a later one.
    /// </summary>
    /// <param name="otherReadings">The readings of the meter, without the reading that is registered or corrected.</param>
    /// <exception cref="ArgumentException">Another reading has the same time (ParamName "ReadAt").</exception>
    /// <exception cref="MeterReadingSequenceException">The value does not fit the chronology.</exception>
    public static void EnsureFits(IEnumerable<MeterReading> otherReadings, DateTime readAt, decimal kwh)
    {
        MeterReading? earlier = null;
        MeterReading? later = null;

        foreach (var reading in Sort(otherReadings))
        {
            if (reading.ReadAt == readAt)
            {
                throw new ArgumentException("The meter already has a reading at this time.", nameof(MeterReadingDetails.ReadAt));
            }

            if (reading.ReadAt < readAt)
            {
                earlier = reading;
            }
            else if (later == null)
            {
                later = reading;
            }
        }

        if (earlier != null && kwh < earlier.Kwh)
        {
            throw new MeterReadingSequenceException(isLowerThanEarlier: true, earlier);
        }

        if (later != null && kwh > later.Kwh)
        {
            throw new MeterReadingSequenceException(isLowerThanEarlier: false, later);
        }
    }
}
