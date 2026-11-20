namespace SameieHub.Domain;

public enum MeterKind
{
    /// <summary>The common main meter of the whole charging facility.</summary>
    Main = 1,

    /// <summary>The sub meter of one charger. Also called a "minusmåler".</summary>
    Sub = 2
}

/// <summary>
/// A meter in the charging facility: the one main meter, or the sub meter of one charger.
/// Both kinds have readings and follow the same rules (see <see cref="MeterHistory"/>).
/// </summary>
public class Meter
{
    public const int MaxNameLength = 100;
    public const int MaxNotesLength = 500;

    /// <summary>The sub meters sit in two physical fuse cabinets, numbered 1 and 2.</summary>
    public const int MinCabinetNumber = 1;

    public const int MaxCabinetNumber = 2;

    public int Id { get; private set; }

    /// <summary>The fuse cabinet (1 or 2) the sub meter sits in. Null for the main meter.</summary>
    public int? CabinetNumber { get; private set; }

    public int ChargingFacilityId { get; private set; }

    public MeterKind Kind { get; private set; }

    /// <summary>Set for a sub meter. Null for the main meter.</summary>
    public int? EvChargerId { get; private set; }

    /// <summary>A label shown to the user, for example "Hovedmåler" or "Undermåler L3".</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    public bool IsMain => Kind == MeterKind.Main;

    // Used by EF Core when it reads a meter from the database.
    private Meter()
    {
    }

    private Meter(int chargingFacilityId, MeterKind kind, int? evChargerId, int? cabinetNumber, string name, string? notes, DateTime createdAt)
    {
        TextRules.EnsurePositiveId(chargingFacilityId, nameof(chargingFacilityId));
        TextRules.EnsureUtc(createdAt, nameof(createdAt));

        ChargingFacilityId = chargingFacilityId;
        Kind = kind;
        EvChargerId = evChargerId;
        CabinetNumber = cabinetNumber;
        Name = TextRules.NormalizeRequired(name, MaxNameLength, nameof(Name));
        Notes = TextRules.NormalizeOptional(notes, MaxNotesLength, nameof(Notes));
        CreatedAt = createdAt;
    }

    public static Meter CreateMain(int chargingFacilityId, string name, DateTime createdAt)
    {
        return new Meter(chargingFacilityId, MeterKind.Main, null, null, name, null, createdAt);
    }

    /// <exception cref="ArgumentException">The cabinet number is not 1 or 2 (ParamName "CabinetNumber").</exception>
    public static Meter CreateSub(int chargingFacilityId, int evChargerId, string name, int cabinetNumber, DateTime createdAt)
    {
        TextRules.EnsurePositiveId(evChargerId, nameof(evChargerId));
        EnsureCabinetNumber(cabinetNumber);

        return new Meter(chargingFacilityId, MeterKind.Sub, evChargerId, cabinetNumber, name, null, createdAt);
    }

    /// <summary>Corrects the cabinet of a sub meter that was registered wrongly. The main meter has no cabinet.</summary>
    public void ChangeCabinetNumber(int cabinetNumber)
    {
        if (IsMain)
        {
            throw new InvalidOperationException("The main meter has no cabinet number.");
        }

        EnsureCabinetNumber(cabinetNumber);
        CabinetNumber = cabinetNumber;
    }

    /// <exception cref="ArgumentException">The cabinet number is not 1 or 2 (ParamName "CabinetNumber").</exception>
    public static void EnsureCabinetNumber(int cabinetNumber)
    {
        if (cabinetNumber < MinCabinetNumber || cabinetNumber > MaxCabinetNumber)
        {
            throw new ArgumentException(
                $"CabinetNumber must be {MinCabinetNumber} or {MaxCabinetNumber}.", nameof(CabinetNumber));
        }
    }

    /// <summary>Called by a repository when the meter is stored. The Id can only be assigned once.</summary>
    public void AssignId(int id)
    {
        TextRules.EnsurePositiveId(id, nameof(id));

        if (Id != 0)
        {
            throw new InvalidOperationException("The meter already has an Id.");
        }

        Id = id;
    }
}
