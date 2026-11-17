namespace SameieHub.Domain;

/// <summary>
/// The editable information about a housing unit. Values are validated and normalized by <see cref="HousingUnit"/>.
/// </summary>
public record HousingUnitDetails
{
    public required string Address { get; init; }

    public string? UnitNumber { get; init; }

    public int? SectionNumber { get; init; }

    public HousingType? HousingType { get; init; }

    public decimal? AreaSquareMeters { get; init; }

    public string? OwnerName { get; init; }

    public string? OwnerPhoneNumber { get; init; }

    public string? OwnerEmail { get; init; }

    public string? SecondOwnerName { get; init; }

    public string? SecondOwnerPhoneNumber { get; init; }

    public string? SecondOwnerEmail { get; init; }

    public string? Notes { get; init; }
}
