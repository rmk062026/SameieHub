namespace SameieHub.Domain;

/// <summary>
/// The editable information about a board case. Status and decision are changed with their own
/// methods on <see cref="BoardCase"/> because they follow rules.
/// </summary>
public record BoardCaseDetails
{
    public required string Title { get; init; }

    public string? Description { get; init; }

    public BoardCasePriority Priority { get; init; } = BoardCasePriority.Normal;

    public DateOnly? MeetingDate { get; init; }

    /// <summary>Null means the case concerns the whole housing cooperative.</summary>
    public int? HousingUnitId { get; init; }

    public string? Notes { get; init; }
}
