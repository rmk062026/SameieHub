namespace SameieHub.Domain;

/// <summary>
/// The shared charging facility of the housing cooperative. It has one main meter for the whole facility,
/// and one charger with its own sub meter for each housing unit that has a carport charger.
/// Only one facility is supported in this version.
/// </summary>
public class ChargingFacility
{
    public const int MaxNameLength = 100;
    public const int MaxNotesLength = 2000;

    /// <summary>
    /// The facility has one charger with its own sub meter for each of eight housing units. A reconciliation is
    /// only complete when all eight sub meters are set up and documented.
    /// </summary>
    public const int ExpectedSubMeterCount = 8;

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    // Used by EF Core when it reads a facility from the database.
    private ChargingFacility()
    {
    }

    /// <exception cref="ArgumentException">The name is missing or too long, or the notes are too long.</exception>
    public ChargingFacility(string name, string? notes, DateTime createdAt)
    {
        TextRules.EnsureUtc(createdAt, nameof(createdAt));

        Name = TextRules.NormalizeRequired(name, MaxNameLength, nameof(Name));
        Notes = TextRules.NormalizeOptional(notes, MaxNotesLength, nameof(Notes));
        CreatedAt = createdAt;
    }

    /// <summary>Called by a repository when the facility is stored. The Id can only be assigned once.</summary>
    public void AssignId(int id)
    {
        TextRules.EnsurePositiveId(id, nameof(id));

        if (Id != 0)
        {
            throw new InvalidOperationException("The facility already has an Id.");
        }

        Id = id;
    }
}
