namespace SameieHub.Domain;

public class BoardCase
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxDecisionLength = 4000;
    public const int MaxNotesLength = 2000;
    public static readonly DateOnly MinMeetingDate = new(2000, 1, 1);
    public static readonly DateOnly MaxMeetingDate = new(2100, 12, 31);

    public int Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public BoardCaseStatus Status { get; private set; } = BoardCaseStatus.New;

    public BoardCasePriority Priority { get; private set; }

    /// <summary>Always a UTC time.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Always a UTC time. Equal to <see cref="CreatedAt"/> until the case is changed.</summary>
    public DateTime UpdatedAt { get; private set; }

    public DateOnly? MeetingDate { get; private set; }

    public string? Decision { get; private set; }

    /// <summary>Null means the case concerns the whole housing cooperative.</summary>
    public int? HousingUnitId { get; private set; }

    public string? Notes { get; private set; }

    /// <param name="createdAt">Must be a UTC time (<see cref="DateTimeKind.Utc"/>).</param>
    /// <exception cref="ArgumentException">
    /// A value is invalid. <see cref="ArgumentException.ParamName"/> names the invalid property.
    /// </exception>
    public BoardCase(BoardCaseDetails details, DateTime createdAt)
    {
        if (createdAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("CreatedAt must be a UTC time.", nameof(createdAt));
        }

        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        UpdateDetails(details, createdAt);
    }

    /// <summary>
    /// Called by a repository when the case is stored. The Id can only be assigned once.
    /// </summary>
    public void AssignId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        if (Id != 0)
        {
            throw new InvalidOperationException($"Board case already has Id {Id}.");
        }

        Id = id;
    }

    /// <summary>
    /// Replaces title, description, priority, meeting date, housing unit link and notes.
    /// Either every value is valid and applied, or nothing changes. <see cref="UpdatedAt"/> is set to
    /// <paramref name="now"/> only when a value actually changes.
    /// </summary>
    /// <param name="now">The time of the change. Must be UTC.</param>
    /// <exception cref="ArgumentException">
    /// A value is invalid. <see cref="ArgumentException.ParamName"/> is the name of the property in <see cref="BoardCaseDetails"/>.
    /// </exception>
    public void UpdateDetails(BoardCaseDetails details, DateTime now)
    {
        EnsureUtc(now);

        var title = NormalizeTitle(details.Title);
        var description = NormalizeOptionalText(details.Description, MaxDescriptionLength, nameof(details.Description));
        var priority = ValidatePriority(details.Priority);
        var meetingDate = ValidateMeetingDate(details.MeetingDate);
        var housingUnitId = ValidateHousingUnitId(details.HousingUnitId);
        var notes = NormalizeOptionalText(details.Notes, MaxNotesLength, nameof(details.Notes));

        var isUnchanged = title == Title
            && description == Description
            && priority == Priority
            && meetingDate == MeetingDate
            && housingUnitId == HousingUnitId
            && notes == Notes;
        if (isUnchanged)
        {
            return;
        }

        Title = title;
        Description = description;
        Priority = priority;
        MeetingDate = meetingDate;
        HousingUnitId = housingUnitId;
        Notes = notes;
        UpdatedAt = now;
    }

    /// <summary>
    /// Any status can be changed to any other, so mistakes can be corrected. The only rule is that
    /// a case can be marked as decided only when it has a decision.
    /// </summary>
    /// <exception cref="InvalidOperationException">The case is changed to decided without a decision.</exception>
    public void ChangeStatus(BoardCaseStatus newStatus, DateTime now)
    {
        EnsureUtc(now);

        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "Status is not valid.");
        }

        if (newStatus == Status)
        {
            return;
        }

        if (newStatus == BoardCaseStatus.Decided && Decision == null)
        {
            throw new InvalidOperationException("A decision must be registered before the case can be marked as decided.");
        }

        Status = newStatus;
        UpdatedAt = now;
    }

    /// <summary>
    /// Registers or corrects the decision. The status is not changed; use <see cref="ChangeStatus"/> for that.
    /// </summary>
    public void RegisterDecision(string decision, DateTime now)
    {
        EnsureUtc(now);

        var normalizedDecision = NormalizeOptionalText(decision, MaxDecisionLength, nameof(Decision));
        if (normalizedDecision == null)
        {
            throw new ArgumentException("Decision is required.", nameof(Decision));
        }

        if (normalizedDecision == Decision)
        {
            return;
        }

        Decision = normalizedDecision;
        UpdatedAt = now;
    }

    /// <summary>
    /// Removes a decision that was registered by mistake.
    /// </summary>
    /// <exception cref="InvalidOperationException">The case is decided. Change the status first.</exception>
    public void ClearDecision(DateTime now)
    {
        EnsureUtc(now);

        if (Status == BoardCaseStatus.Decided)
        {
            throw new InvalidOperationException("A decided case must keep its decision. Change the status first.");
        }

        if (Decision == null)
        {
            return;
        }

        Decision = null;
        UpdatedAt = now;
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

    private static void EnsureUtc(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time of the change must be a UTC time.", nameof(now));
        }
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(BoardCaseDetails.Title));
        }

        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Title can be at most {MaxTitleLength} characters.", nameof(BoardCaseDetails.Title));
        }

        return trimmed;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} can be at most {maxLength} characters.", fieldName);
        }

        return trimmed;
    }

    private static BoardCasePriority ValidatePriority(BoardCasePriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(BoardCaseDetails.Priority), "Priority is not valid.");
        }

        return priority;
    }

    private static DateOnly? ValidateMeetingDate(DateOnly? meetingDate)
    {
        if (meetingDate < MinMeetingDate || meetingDate > MaxMeetingDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BoardCaseDetails.MeetingDate), $"MeetingDate must be between {MinMeetingDate} and {MaxMeetingDate}.");
        }

        return meetingDate;
    }

    private static int? ValidateHousingUnitId(int? housingUnitId)
    {
        if (housingUnitId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BoardCaseDetails.HousingUnitId), "HousingUnitId must be positive.");
        }

        return housingUnitId;
    }
}
