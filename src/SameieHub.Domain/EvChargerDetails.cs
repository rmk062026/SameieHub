namespace SameieHub.Domain;

/// <summary>
/// The editable information about an EV charger. The housing unit is chosen when the charger is registered
/// and does not change.
/// </summary>
public record EvChargerDetails
{
    /// <summary>Optional charger or parking space number, for example "L3".</summary>
    public string? ChargerNumber { get; init; }

    public string? Manufacturer { get; init; }

    public string? Model { get; init; }

    public ChargerStatus Status { get; init; } = ChargerStatus.InUse;

    /// <summary>Whether the household has an electric vehicle. Null means unknown.</summary>
    public bool? HasElectricVehicle { get; init; }

    public string? Notes { get; init; }
}
