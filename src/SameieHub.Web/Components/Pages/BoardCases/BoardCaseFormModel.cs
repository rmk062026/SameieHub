using System.ComponentModel.DataAnnotations;
using SameieHub.Domain;

namespace SameieHub.Web.Components.Pages.BoardCases;

/// <summary>
/// The values bound to the form fields. Only the required title is checked here;
/// all other rules are enforced by <see cref="BoardCase"/> and <see cref="BoardCaseService"/>.
/// </summary>
public class BoardCaseFormModel
{
    [Required(ErrorMessage = "Tittelen må fylles ut.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public BoardCasePriority Priority { get; set; } = BoardCasePriority.Normal;

    public DateOnly? MeetingDate { get; set; }

    public int? HousingUnitId { get; set; }

    public string? Notes { get; set; }

    public static BoardCaseFormModel From(BoardCase boardCase)
    {
        return new BoardCaseFormModel
        {
            Title = boardCase.Title,
            Description = boardCase.Description,
            Priority = boardCase.Priority,
            MeetingDate = boardCase.MeetingDate,
            HousingUnitId = boardCase.HousingUnitId,
            Notes = boardCase.Notes
        };
    }

    public BoardCaseDetails ToDetails()
    {
        return new BoardCaseDetails
        {
            Title = Title,
            Description = Description,
            Priority = Priority,
            MeetingDate = MeetingDate,
            HousingUnitId = HousingUnitId,
            Notes = Notes
        };
    }
}
