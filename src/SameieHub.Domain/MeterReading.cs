namespace SameieHub.Domain;

/// <summary>
/// A meter value read from the main meter or a sub meter at a given time. Consumption is never stored: it is
/// calculated from the difference between readings (see <see cref="MeterHistory"/>).
/// </summary>
public class MeterReading
{
    public const int MaxNotesLength = 500;
    public const int KwhDecimals = 3;
    public const decimal MaxKwh = 9_999_999.999m;
    public static readonly DateTime MinReadAt = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public int Id { get; private set; }

    public int MeterId { get; private set; }

    /// <summary>UTC. When the meter was read.</summary>
    public DateTime ReadAt { get; private set; }

    public decimal Kwh { get; private set; }

    /// <summary>UTC. When the reading was registered in SameieHub, which can be later than <see cref="ReadAt"/>.</summary>
    public DateTime RegisteredAt { get; private set; }

    /// <summary>UTC. Equals <see cref="RegisteredAt"/> until the reading is corrected.</summary>
    public DateTime UpdatedAt { get; private set; }

    public string? Notes { get; private set; }

    // Used by EF Core when it reads a reading from the database.
    private MeterReading()
    {
    }

    /// <exception cref="ArgumentException">
    /// A value is invalid. <see cref="ArgumentException.ParamName"/> is "ReadAt", "Kwh" or "Notes".
    /// </exception>
    public MeterReading(int meterId, MeterReadingDetails details, DateTime registeredAt)
    {
        TextRules.EnsurePositiveId(meterId, nameof(meterId));
        TextRules.EnsureUtc(registeredAt, nameof(registeredAt));

        MeterId = meterId;
        Apply(details);
        RegisteredAt = registeredAt;
        UpdatedAt = registeredAt;
    }

    /// <summary>Called by a repository when the reading is stored. The Id can only be assigned once.</summary>
    public void AssignId(int id)
    {
        TextRules.EnsurePositiveId(id, nameof(id));

        if (Id != 0)
        {
            throw new InvalidOperationException("The reading already has an Id.");
        }

        Id = id;
    }

    /// <summary>
    /// Corrects a reading that was registered wrongly. Nothing is changed if a value is invalid.
    /// Whether the new values fit between the neighbouring readings is checked by <see cref="ChargingService"/>.
    /// </summary>
    public void Correct(MeterReadingDetails details, DateTime now)
    {
        TextRules.EnsureUtc(now, nameof(now));

        var before = ToDetails();
        Apply(details);

        if (ToDetails() != before)
        {
            UpdatedAt = now;
        }
    }

    public MeterReadingDetails ToDetails()
    {
        return new MeterReadingDetails { ReadAt = ReadAt, Kwh = Kwh, Notes = Notes };
    }

    /// <summary>
    /// The rules for a meter value: from 0 to <see cref="MaxKwh"/> and at most <see cref="KwhDecimals"/> decimals.
    /// Forms can call this to show the message at the right field, so the rule exists in one place.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not allowed (ParamName "Kwh").</exception>
    public static void CheckKwh(decimal kwh)
    {
        if (kwh < 0 || kwh > MaxKwh)
        {
            throw new ArgumentException($"Kwh must be between 0 and {MaxKwh}.", nameof(MeterReadingDetails.Kwh));
        }

        if (decimal.Round(kwh, KwhDecimals) != kwh)
        {
            throw new ArgumentException($"Kwh can have at most {KwhDecimals} decimals.", nameof(MeterReadingDetails.Kwh));
        }
    }

    private void Apply(MeterReadingDetails details)
    {
        var notes = TextRules.NormalizeOptional(details.Notes, MaxNotesLength, nameof(details.Notes));

        if (details.ReadAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("ReadAt must be a UTC time.", nameof(details.ReadAt));
        }

        if (details.ReadAt < MinReadAt)
        {
            throw new ArgumentException($"ReadAt cannot be before {MinReadAt:yyyy-MM-dd}.", nameof(details.ReadAt));
        }

        CheckKwh(details.Kwh);

        ReadAt = details.ReadAt;
        Kwh = details.Kwh;
        Notes = notes;
    }
}
