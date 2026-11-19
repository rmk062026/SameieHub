using System.ComponentModel.DataAnnotations;
using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.TaskItems;

/// <summary>
/// The values bound to the form fields. Only the required title is checked here;
/// all other rules are enforced by <see cref="TaskItem"/> and <see cref="TaskItemService"/>.
/// </summary>
public class TaskFormModel
{
    [Required(ErrorMessage = "Tittelen må fylles ut.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskItemCategory Category { get; set; } = TaskItemCategory.General;

    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Normal;

    public DateOnly? DueDate { get; set; }

    public string? AssignedTo { get; set; }

    public int? HousingUnitId { get; set; }

    public int? BoardCaseId { get; set; }

    public string? Notes { get; set; }

    public static TaskFormModel From(TaskItem task)
    {
        return new TaskFormModel
        {
            Title = task.Title,
            Description = task.Description,
            Category = task.Category,
            Priority = task.Priority,
            DueDate = task.DueDate,
            AssignedTo = task.AssignedTo,
            HousingUnitId = task.HousingUnitId,
            BoardCaseId = task.BoardCaseId,
            Notes = task.Notes
        };
    }

    public TaskItemDetails ToDetails()
    {
        return new TaskItemDetails
        {
            Title = Title,
            Description = Description,
            Category = Category,
            Priority = Priority,
            DueDate = DueDate,
            AssignedTo = AssignedTo,
            HousingUnitId = HousingUnitId,
            BoardCaseId = BoardCaseId,
            Notes = Notes
        };
    }
}
