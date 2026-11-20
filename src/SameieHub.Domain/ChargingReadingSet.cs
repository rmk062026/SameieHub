namespace SameieHub.Domain;

/// <summary>
/// All meters of the facility read at the same time and registered in one go: the main meter first, then the
/// sub meters. A reading set is not stored as a record of its own. The readings of a set are ordinary readings
/// that share the same <see cref="ReadAt"/>.
/// </summary>
public record MeterReadingSetDetails
{
    /// <summary>When the meters were read, as a UTC time. The same time is used for every meter in the set.</summary>
    public required DateTime ReadAt { get; init; }

    /// <summary>The main meter value in kWh. Null means the main meter was not read: it is left out, never stored as zero.</summary>
    public decimal? MainMeterKwh { get; init; }

    /// <summary>
    /// The sub meter values in kWh, by meter Id. A missing entry or a null value means the meter was not read:
    /// it is left out, never stored as zero. Zero must be given as 0.
    /// </summary>
    public IReadOnlyDictionary<int, decimal?> SubMeterKwh { get; init; } = new Dictionary<int, decimal?>();

    /// <summary>Notes stored with every reading in the set.</summary>
    public string? Notes { get; init; }
}

/// <summary>What happened to one meter when a reading set was registered.</summary>
/// <param name="Meter">The meter.</param>
/// <param name="Reading">The registered reading, or null when the meter was not read in this set.</param>
/// <param name="PreviousReading">The reading before this one in the chronology, or null when there is none.</param>
public record ReadingSetLine(Meter Meter, MeterReading? Reading, MeterReading? PreviousReading)
{
    public bool WasRead => Reading != null;

    /// <summary>True when this is the first reading of the meter. A start value has no consumption.</summary>
    public bool IsStartValue => Reading != null && PreviousReading == null;

    /// <summary>The consumption since the previous reading of the same meter, or null for a start value or a meter that was not read.</summary>
    public decimal? ConsumptionKwh => Reading != null && PreviousReading != null ? Reading.Kwh - PreviousReading.Kwh : null;
}

public record ReadingSetResult
{
    /// <summary>UTC.</summary>
    public required DateTime ReadAt { get; init; }

    /// <summary>The main meter first, then the sub meters in the order of their Id.</summary>
    public IReadOnlyList<ReadingSetLine> Lines { get; init; } = [];

    /// <summary>The meters that were not read in this set. They are missing, not zero.</summary>
    public IReadOnlyList<Meter> MetersNotRead { get; init; } = [];

    /// <summary>
    /// The reconciliation from the previous main meter reading to this one. Null when the main meter was not
    /// read in this set or has no earlier reading (a start value).
    /// </summary>
    public ChargingReconciliation? Reconciliation { get; init; }
}
