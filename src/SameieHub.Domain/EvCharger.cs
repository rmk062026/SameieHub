namespace SameieHub.Domain;

/// <summary>
/// The EV charger of one housing unit in the charging facility. A housing unit has at most one charger in this
/// version. The charger keeps its sub meter and all readings, whatever its status.
/// </summary>
public class EvCharger
{
    public const int MaxChargerNumberLength = 20;
    public const int MaxManufacturerLength = 100;
    public const int MaxModelLength = 100;
    public const int MaxNotesLength = 2000;

    public int Id { get; private set; }

    public int ChargingFacilityId { get; private set; }

    public int HousingUnitId { get; private set; }

    public string? ChargerNumber { get; private set; }

    public string? Manufacturer { get; private set; }

    public string? Model { get; private set; }

    public ChargerStatus Status { get; private set; }

    public bool? HasElectricVehicle { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>UTC. Changes only when the information really changes.</summary>
    public DateTime UpdatedAt { get; private set; }

    // Used by EF Core when it reads a charger from the database.
    private EvCharger()
    {
    }

    /// <exception cref="ArgumentException">
    /// A value is invalid. <see cref="ArgumentException.ParamName"/> names the invalid property.
    /// </exception>
    public EvCharger(int chargingFacilityId, int housingUnitId, EvChargerDetails details, DateTime createdAt)
    {
        TextRules.EnsurePositiveId(chargingFacilityId, nameof(chargingFacilityId));
        TextRules.EnsurePositiveId(housingUnitId, nameof(housingUnitId));
        TextRules.EnsureUtc(createdAt, nameof(createdAt));

        ChargingFacilityId = chargingFacilityId;
        HousingUnitId = housingUnitId;
        Apply(details);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Called by a repository when the charger is stored. The Id can only be assigned once.</summary>
    public void AssignId(int id)
    {
        TextRules.EnsurePositiveId(id, nameof(id));

        if (Id != 0)
        {
            throw new InvalidOperationException("The charger already has an Id.");
        }

        Id = id;
    }

    /// <summary>
    /// Changes all the information, or nothing: every value is checked before any is changed.
    /// <see cref="UpdatedAt"/> changes only when something is different.
    /// </summary>
    public void UpdateDetails(EvChargerDetails details, DateTime now)
    {
        TextRules.EnsureUtc(now, nameof(now));

        var before = ToDetails();
        Apply(details);

        if (ToDetails() != before)
        {
            UpdatedAt = now;
        }
    }

    public void ChangeStatus(ChargerStatus newStatus, DateTime now)
    {
        UpdateDetails(ToDetails() with { Status = newStatus }, now);
    }

    public EvChargerDetails ToDetails()
    {
        return new EvChargerDetails
        {
            ChargerNumber = ChargerNumber,
            Manufacturer = Manufacturer,
            Model = Model,
            Status = Status,
            HasElectricVehicle = HasElectricVehicle,
            Notes = Notes
        };
    }

    private void Apply(EvChargerDetails details)
    {
        var chargerNumber = TextRules.NormalizeOptional(details.ChargerNumber, MaxChargerNumberLength, nameof(details.ChargerNumber));
        var manufacturer = TextRules.NormalizeOptional(details.Manufacturer, MaxManufacturerLength, nameof(details.Manufacturer));
        var model = TextRules.NormalizeOptional(details.Model, MaxModelLength, nameof(details.Model));
        var notes = TextRules.NormalizeOptional(details.Notes, MaxNotesLength, nameof(details.Notes));

        if (!Enum.IsDefined(details.Status))
        {
            throw new ArgumentOutOfRangeException(nameof(details.Status), "Status is not valid.");
        }

        ChargerNumber = chargerNumber;
        Manufacturer = manufacturer;
        Model = model;
        Status = details.Status;
        HasElectricVehicle = details.HasElectricVehicle;
        Notes = notes;
    }
}
