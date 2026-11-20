namespace SameieHub.Domain;

/// <summary>A housing unit can have only one charger in this version.</summary>
public class EvChargerAlreadyExistsException : InvalidOperationException
{
    public EvChargerAlreadyExistsException(int housingUnitId, Exception? innerException = null)
        : base($"Housing unit {housingUnitId} already has a charger.", innerException)
    {
    }
}

/// <summary>Only one charging facility is supported in this version.</summary>
public class ChargingFacilityAlreadyExistsException : InvalidOperationException
{
    public ChargingFacilityAlreadyExistsException()
        : base("A charging facility already exists. Only one facility is supported.")
    {
    }
}

/// <summary>
/// A reading does not fit in the chronology: a meter only counts up, so a later reading cannot be lower
/// than an earlier one.
/// </summary>
public class MeterReadingSequenceException : InvalidOperationException
{
    public MeterReadingSequenceException(bool isLowerThanEarlier, MeterReading neighbour)
        : base(isLowerThanEarlier
            ? $"The reading is lower than the earlier reading of {neighbour.ReadAt:u} ({neighbour.Kwh} kWh)."
            : $"The reading is higher than the later reading of {neighbour.ReadAt:u} ({neighbour.Kwh} kWh).")
    {
        IsLowerThanEarlier = isLowerThanEarlier;
        NeighbourReadAt = neighbour.ReadAt;
        NeighbourKwh = neighbour.Kwh;
    }

    /// <summary>True when the value is lower than an earlier reading; false when it is higher than a later one.</summary>
    public bool IsLowerThanEarlier { get; }

    /// <summary>UTC.</summary>
    public DateTime NeighbourReadAt { get; }

    public decimal NeighbourKwh { get; }
}
