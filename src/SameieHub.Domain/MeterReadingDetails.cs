namespace SameieHub.Domain;

public record MeterReadingDetails
{
    /// <summary>
    /// When the meter was read, as a UTC time. This is not the time the reading was registered.
    /// The user enters Norwegian local time, which the user interface converts.
    /// </summary>
    public required DateTime ReadAt { get; init; }

    /// <summary>The meter value in kWh, with at most three decimals.</summary>
    public required decimal Kwh { get; init; }

    public string? Notes { get; init; }
}
