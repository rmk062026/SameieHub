namespace SameieHub.Domain;

public class TaskItem
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4000;
    public const int MaxAssignedToLength = 100;
    public const int MaxNotesLength = 2000;
    public static readonly DateOnly MinDueDate = new(2000, 1, 1);
    public static readonly DateOnly MaxDueDate = new(2100, 12, 31);

    public int Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public TaskItemCategory Category { get; private set; }

    public TaskItemStatus Status { get; private set; } = TaskItemStatus.NotStarted;

    public TaskItemPriority Priority { get; private set; }

    public DateOnly? DueDate { get; private set; }

    /// <summary>The name of the person responsible. A simple text, not a link to a person.</summary>
    public string? AssignedTo { get; private set; }

    /// <summary>Null means the task is not about a housing unit.</summary>
    public int? HousingUnitId { get; private set; }

    /// <summary>Null means the task is not linked to a board case.</summary>
    public int? BoardCaseId { get; private set; }

    /// <summary>Always a UTC time.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Always a UTC time. Equal to <see cref="CreatedAt"/> until the task is changed.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>The UTC time the task was completed. Null unless the status is completed.</summary>
    public DateTime? CompletedAt { get; private set; }

    public string? Notes { get; private set; }

    /// <param name="createdAt">Must be a UTC time (<see cref="DateTimeKind.Utc"/>).</param>
    /// <exception cref="ArgumentException">
    /// A value is invalid. <see cref="ArgumentException.ParamName"/> names the invalid property.
    /// </exception>
    public TaskItem(TaskItemDetails details, DateTime createdAt)
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
    /// Called by a repository when the task is stored. The Id can only be assigned once.
    /// </summary>
    public void AssignId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        if (Id != 0)
        {
            throw new InvalidOperationException($"Task already has Id {Id}.");
        }

        Id = id;
    }

    /// <summary>
    /// Replaces all details except the status. Either every value is valid and applied, or nothing changes.
    /// <see cref="UpdatedAt"/> is set to <paramref name="now"/> only when a value actually changes.
    /// </summary>
    /// <param name="now">The time of the change. Must be UTC.</param>
    /// <exception cref="ArgumentException">
    /// A value is invalid. <see cref="ArgumentException.ParamName"/> is the name of the property in <see cref="TaskItemDetails"/>.
    /// </exception>
    public void UpdateDetails(TaskItemDetails details, DateTime now)
    {
        EnsureUtc(now);

        var title = NormalizeTitle(details.Title);
        var description = NormalizeOptionalText(details.Description, MaxDescriptionLength, nameof(details.Description));
        var category = ValidateCategory(details.Category);
        var priority = ValidatePriority(details.Priority);
        var dueDate = ValidateDueDate(details.DueDate);
        var assignedTo = NormalizeOptionalText(details.AssignedTo, MaxAssignedToLength, nameof(details.AssignedTo));
        var housingUnitId = ValidateReferenceId(details.HousingUnitId, nameof(details.HousingUnitId));
        var boardCaseId = ValidateReferenceId(details.BoardCaseId, nameof(details.BoardCaseId));
        var notes = NormalizeOptionalText(details.Notes, MaxNotesLength, nameof(details.Notes));

        var isUnchanged = title == Title
            && description == Description
            && category == Category
            && priority == Priority
            && dueDate == DueDate
            && assignedTo == AssignedTo
            && housingUnitId == HousingUnitId
            && boardCaseId == BoardCaseId
            && notes == Notes;
        if (isUnchanged)
        {
            return;
        }

        Title = title;
        Description = description;
        Category = category;
        Priority = priority;
        DueDate = dueDate;
        AssignedTo = assignedTo;
        HousingUnitId = housingUnitId;
        BoardCaseId = boardCaseId;
        Notes = notes;
        UpdatedAt = now;
    }

    /// <summary>
    /// Starts, completes or reopens the task. Any status can be changed to any other, so mistakes can be corrected.
    /// Completing sets <see cref="CompletedAt"/> to <paramref name="now"/>; leaving the completed status clears it.
    /// Setting the same status again changes nothing.
    /// </summary>
    /// <param name="now">The time of the change. Must be UTC.</param>
    public void ChangeStatus(TaskItemStatus newStatus, DateTime now)
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

        Status = newStatus;
        CompletedAt = newStatus == TaskItemStatus.Completed ? now : null;
        UpdatedAt = now;
    }

    /// <summary>
    /// A task is overdue when it is not completed and its due date is before <paramref name="today"/>.
    /// A task due today is not overdue until the next day.
    /// </summary>
    public bool IsOverdue(DateOnly today)
    {
        return Status != TaskItemStatus.Completed && DueDate < today;
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
            throw new ArgumentException("Title is required.", nameof(TaskItemDetails.Title));
        }

        var trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException($"Title can be at most {MaxTitleLength} characters.", nameof(TaskItemDetails.Title));
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

    private static TaskItemCategory ValidateCategory(TaskItemCategory category)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(TaskItemDetails.Category), "Category is not valid.");
        }

        return category;
    }

    private static TaskItemPriority ValidatePriority(TaskItemPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(TaskItemDetails.Priority), "Priority is not valid.");
        }

        return priority;
    }

    private static DateOnly? ValidateDueDate(DateOnly? dueDate)
    {
        if (dueDate < MinDueDate || dueDate > MaxDueDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TaskItemDetails.DueDate), $"DueDate must be between {MinDueDate} and {MaxDueDate}.");
        }

        return dueDate;
    }

    private static int? ValidateReferenceId(int? id, string fieldName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(fieldName, $"{fieldName} must be positive.");
        }

        return id;
    }
}
