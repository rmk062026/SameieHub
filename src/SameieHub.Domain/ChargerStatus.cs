namespace SameieHub.Domain;

/// <summary>
/// The administrative status of an EV charger. It says nothing about whether the housing unit is active,
/// and it does not control the charger physically.
/// </summary>
public enum ChargerStatus
{
    InUse = 1,
    NotInUse = 2,
    TemporarilyDisabled = 3
}
