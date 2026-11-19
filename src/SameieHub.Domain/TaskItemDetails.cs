namespace SameieHub.Domain;

/// <summary>
/// The editable information about a task. The status is changed with <see cref="TaskItem.ChangeStatus"/>
/// because it also controls the completion time.
/// </summary>
public record TaskItemDetails
{
    public required string Title { get; init; }

    public string? Description { get; init; }

    public TaskItemCategory Category { get; init; } = TaskItemCategory.General;

    public TaskItemPriority Priority { get; init; } = TaskItemPriority.Normal;

    public DateOnly? DueDate { get; init; }

    public string? AssignedTo { get; init; }

    /// <summary>Optional link to one housing unit.</summary>
    public int? HousingUnitId { get; init; }

    /// <summary>Optional link to one board case. It does not have to concern the same housing unit as the task.</summary>
    public int? BoardCaseId { get; init; }

    public string? Notes { get; init; }
}
