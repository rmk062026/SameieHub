using SameieHub.Domain;

namespace SameieHub.Tests.Domain;

public class TaskItemTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = CreatedAt.AddHours(1);
    private static readonly DateOnly Today = new(2026, 11, 18);

    private static TaskItemDetails FullDetails() => new()
    {
        Title = "Rense takrenner",
        Description = "Fiktiv beskrivelse.",
        Category = TaskItemCategory.Maintenance,
        Priority = TaskItemPriority.High,
        DueDate = new DateOnly(2026, 12, 1),
        AssignedTo = "Test Person",
        HousingUnitId = 7,
        BoardCaseId = 3,
        Notes = "Fiktive merknader."
    };

    private static TaskItem NewTask() => new(FullDetails(), CreatedAt);

    // Creation

    [Fact]
    public void Constructor_OnlyTitle_UsesDefaultsAndNoOptionalValues()
    {
        var task = new TaskItem(new TaskItemDetails { Title = "Bytt pære" }, CreatedAt);

        Assert.Equal("Bytt pære", task.Title);
        Assert.Equal(TaskItemStatus.NotStarted, task.Status);
        Assert.Equal(TaskItemCategory.General, task.Category);
        Assert.Equal(TaskItemPriority.Normal, task.Priority);
        Assert.Equal(CreatedAt, task.CreatedAt);
        Assert.Equal(CreatedAt, task.UpdatedAt);
        Assert.Equal(0, task.Id);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.Description);
        Assert.Null(task.DueDate);
        Assert.Null(task.AssignedTo);
        Assert.Null(task.HousingUnitId);
        Assert.Null(task.BoardCaseId);
        Assert.Null(task.Notes);
    }

    [Fact]
    public void Constructor_AllDetails_SetsAllValues()
    {
        var task = NewTask();

        AssertHasDetails(task, FullDetails());
        Assert.Equal(TaskItemStatus.NotStarted, task.Status);
        Assert.Equal(CreatedAt, task.CreatedAt);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Constructor_CreatedAtNotUtc_Throws(DateTimeKind kind)
    {
        var notUtc = new DateTime(2026, 11, 18, 10, 30, 0, kind);

        var exception = Assert.Throws<ArgumentException>(() => new TaskItem(FullDetails(), notUtc));

        Assert.Equal("createdAt", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_MissingTitle_Throws(string? title)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TaskItem(FullDetails() with { Title = title! }, CreatedAt));

        Assert.Equal("Title", exception.ParamName);
    }

    [Fact]
    public void Constructor_TextWithSurroundingSpaces_IsTrimmed()
    {
        var task = new TaskItem(new TaskItemDetails
        {
            Title = "  Rense takrenner  ",
            Description = " Beskrivelse ",
            AssignedTo = "  Test Person ",
            Notes = "  Merknad  "
        }, CreatedAt);

        Assert.Equal("Rense takrenner", task.Title);
        Assert.Equal("Beskrivelse", task.Description);
        Assert.Equal("Test Person", task.AssignedTo);
        Assert.Equal("Merknad", task.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void UpdateDetails_BlankOptionalText_BecomesNull(string? blank)
    {
        var task = NewTask();

        task.UpdateDetails(FullDetails() with { Description = blank, AssignedTo = blank, Notes = blank }, Later);

        Assert.Null(task.Description);
        Assert.Null(task.AssignedTo);
        Assert.Null(task.Notes);
    }

    // Limits and invalid values

    [Fact]
    public void UpdateDetails_ValuesAtTheLimits_AreAccepted()
    {
        var task = NewTask();

        task.UpdateDetails(new TaskItemDetails
        {
            Title = new string('t', TaskItem.MaxTitleLength),
            Description = new string('d', TaskItem.MaxDescriptionLength),
            AssignedTo = new string('a', TaskItem.MaxAssignedToLength),
            Notes = new string('n', TaskItem.MaxNotesLength),
            DueDate = TaskItem.MinDueDate,
            HousingUnitId = 1,
            BoardCaseId = 1
        }, Later);
        task.UpdateDetails(FullDetails() with { DueDate = TaskItem.MaxDueDate }, Later.AddHours(1));

        Assert.Equal(TaskItem.MaxDueDate, task.DueDate);
    }

    [Fact]
    public void UpdateDetails_TooLongText_ThrowsAndChangesNothing()
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = new string('t', TaskItem.MaxTitleLength + 1) }, "Title");
        AssertRejectedAndUnchanged(FullDetails() with { Description = new string('d', TaskItem.MaxDescriptionLength + 1) }, "Description");
        AssertRejectedAndUnchanged(FullDetails() with { AssignedTo = new string('a', TaskItem.MaxAssignedToLength + 1) }, "AssignedTo");
        AssertRejectedAndUnchanged(FullDetails() with { Notes = new string('n', TaskItem.MaxNotesLength + 1) }, "Notes");
    }

    [Fact]
    public void UpdateDetails_BlankTitle_ThrowsAndChangesNothing()
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "   ", Notes = "Skal ikke lagres" }, "Title");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void UpdateDetails_UndefinedCategory_ThrowsAndChangesNothing(int category)
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "Ny tittel", Category = (TaskItemCategory)category }, "Category");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void UpdateDetails_UndefinedPriority_ThrowsAndChangesNothing(int priority)
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "Ny tittel", Priority = (TaskItemPriority)priority }, "Priority");
    }

    [Fact]
    public void UpdateDetails_DueDateOutsideAllowedRange_ThrowsAndChangesNothing()
    {
        AssertRejectedAndUnchanged(FullDetails() with { DueDate = TaskItem.MinDueDate.AddDays(-1) }, "DueDate");
        AssertRejectedAndUnchanged(FullDetails() with { DueDate = TaskItem.MaxDueDate.AddDays(1) }, "DueDate");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateDetails_InvalidHousingUnitId_ThrowsAndChangesNothing(int id)
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "Ny tittel", HousingUnitId = id }, "HousingUnitId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateDetails_InvalidBoardCaseId_ThrowsAndChangesNothing(int id)
    {
        AssertRejectedAndUnchanged(FullDetails() with { Title = "Ny tittel", BoardCaseId = id }, "BoardCaseId");
    }

    [Fact]
    public void Constructor_InvalidOptionalValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TaskItem(FullDetails() with { HousingUnitId = 0 }, CreatedAt));
    }

    // Update

    [Fact]
    public void UpdateDetails_NewValues_ReplacesAllDetailsAndKeepsStatusAndCreatedAt()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.InProgress, Later);
        var changed = new TaskItemDetails
        {
            Title = "Ny tittel",
            Category = TaskItemCategory.General,
            Priority = TaskItemPriority.Low,
            DueDate = new DateOnly(2027, 1, 15)
        };

        task.UpdateDetails(changed, Later.AddHours(1));

        AssertHasDetails(task, changed);
        Assert.Null(task.Description);
        Assert.Null(task.HousingUnitId);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(CreatedAt, task.CreatedAt);
        Assert.Equal(Later.AddHours(1), task.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_RealChange_SetsUpdatedAt()
    {
        var task = NewTask();

        task.UpdateDetails(FullDetails() with { Title = "Ny tittel" }, Later);

        Assert.Equal(Later, task.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_SameValues_DoesNotChangeUpdatedAt()
    {
        var task = NewTask();

        task.UpdateDetails(FullDetails(), Later);

        Assert.Equal(CreatedAt, task.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_SameValuesWithExtraSpaces_DoesNotChangeUpdatedAt()
    {
        var task = NewTask();

        task.UpdateDetails(FullDetails() with { Title = "  Rense takrenner  ", AssignedTo = " Test Person " }, Later);

        Assert.Equal(CreatedAt, task.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_InvalidValue_DoesNotChangeUpdatedAt()
    {
        var task = NewTask();

        Assert.Throws<ArgumentException>(() => task.UpdateDetails(FullDetails() with { Title = " " }, Later));

        Assert.Equal(CreatedAt, task.UpdatedAt);
    }

    [Fact]
    public void UpdateDetails_CompletedTask_KeepsCompletedAt()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Completed, Later);

        task.UpdateDetails(FullDetails() with { Title = "Ny tittel" }, Later.AddHours(1));

        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(Later, task.CompletedAt);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Changes_TimeNotUtc_Throws(DateTimeKind kind)
    {
        var task = NewTask();
        var notUtc = new DateTime(2026, 11, 19, 10, 0, 0, kind);

        Assert.Equal("now", Assert.Throws<ArgumentException>(() => task.UpdateDetails(FullDetails() with { Title = "Ny" }, notUtc)).ParamName);
        Assert.Equal("now", Assert.Throws<ArgumentException>(() => task.ChangeStatus(TaskItemStatus.Completed, notUtc)).ParamName);
        Assert.Equal(CreatedAt, task.UpdatedAt);
        Assert.Equal(TaskItemStatus.NotStarted, task.Status);
    }

    // References

    [Fact]
    public void References_AreOptionalAndIndependent()
    {
        var neither = new TaskItem(FullDetails() with { HousingUnitId = null, BoardCaseId = null }, CreatedAt);
        var onlyUnit = new TaskItem(FullDetails() with { HousingUnitId = 5, BoardCaseId = null }, CreatedAt);
        var onlyCase = new TaskItem(FullDetails() with { HousingUnitId = null, BoardCaseId = 6 }, CreatedAt);
        var both = new TaskItem(FullDetails() with { HousingUnitId = 5, BoardCaseId = 6 }, CreatedAt);

        Assert.Null(neither.HousingUnitId);
        Assert.Null(neither.BoardCaseId);
        Assert.Equal(5, onlyUnit.HousingUnitId);
        Assert.Null(onlyUnit.BoardCaseId);
        Assert.Null(onlyCase.HousingUnitId);
        Assert.Equal(6, onlyCase.BoardCaseId);
        Assert.Equal((5, 6), (both.HousingUnitId, both.BoardCaseId));
    }

    [Fact]
    public void UpdateDetails_RemovingOneReference_KeepsTheOther()
    {
        var task = NewTask();

        task.UpdateDetails(FullDetails() with { HousingUnitId = null }, Later);

        Assert.Null(task.HousingUnitId);
        Assert.Equal(3, task.BoardCaseId);
    }

    // Status and completion

    [Fact]
    public void ChangeStatus_Start_SetsInProgressWithoutCompletionTime()
    {
        var task = NewTask();

        task.ChangeStatus(TaskItemStatus.InProgress, Later);

        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Null(task.CompletedAt);
        Assert.Equal(Later, task.UpdatedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.NotStarted)]
    [InlineData(TaskItemStatus.InProgress)]
    public void ChangeStatus_ToCompleted_SetsCompletedAtAndUpdatedAtFromTheGivenTime(TaskItemStatus from)
    {
        var task = NewTask();
        task.ChangeStatus(from, CreatedAt);

        task.ChangeStatus(TaskItemStatus.Completed, Later);

        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(Later, task.CompletedAt);
        Assert.Equal(Later, task.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, task.CompletedAt!.Value.Kind);
    }

    [Theory]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.NotStarted)]
    public void ChangeStatus_ReopeningACompletedTask_ClearsCompletedAt(TaskItemStatus reopenedStatus)
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Completed, Later);

        task.ChangeStatus(reopenedStatus, Later.AddHours(1));

        Assert.Equal(reopenedStatus, task.Status);
        Assert.Null(task.CompletedAt);
        Assert.Equal(Later.AddHours(1), task.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_CompletedAgainAfterReopening_GetsTheNewCompletionTime()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Completed, Later);
        task.ChangeStatus(TaskItemStatus.InProgress, Later.AddHours(1));

        task.ChangeStatus(TaskItemStatus.Completed, Later.AddHours(2));

        Assert.Equal(Later.AddHours(2), task.CompletedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.NotStarted)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Completed)]
    public void ChangeStatus_SameStatus_ChangesNeitherCompletedAtNorUpdatedAt(TaskItemStatus status)
    {
        var task = NewTask();
        task.ChangeStatus(status, Later);
        var completedAt = task.CompletedAt;
        var updatedAt = task.UpdatedAt;

        task.ChangeStatus(status, Later.AddHours(5));

        Assert.Equal(completedAt, task.CompletedAt);
        Assert.Equal(updatedAt, task.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_BetweenNotStartedAndInProgress_NeverSetsCompletedAt()
    {
        var task = NewTask();

        task.ChangeStatus(TaskItemStatus.InProgress, Later);
        task.ChangeStatus(TaskItemStatus.NotStarted, Later.AddHours(1));

        Assert.Null(task.CompletedAt);
        Assert.Equal(TaskItemStatus.NotStarted, task.Status);
    }

    [Fact]
    public void ChangeStatus_EveryTransition_KeepsCompletedAtInLineWithStatus()
    {
        var statuses = Enum.GetValues<TaskItemStatus>();

        foreach (var from in statuses)
        {
            foreach (var to in statuses)
            {
                var task = NewTask();
                task.ChangeStatus(from, Later);

                task.ChangeStatus(to, Later.AddHours(1));

                Assert.Equal(to == TaskItemStatus.Completed, task.CompletedAt != null);
                Assert.Equal(CreatedAt, task.CreatedAt);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void ChangeStatus_UndefinedStatus_ThrowsAndChangesNothing(int status)
    {
        var task = NewTask();

        Assert.Throws<ArgumentOutOfRangeException>(() => task.ChangeStatus((TaskItemStatus)status, Later));

        Assert.Equal(TaskItemStatus.NotStarted, task.Status);
        Assert.Equal(CreatedAt, task.UpdatedAt);
    }

    // Overdue

    [Fact]
    public void IsOverdue_DueDateBeforeToday_IsTrue()
    {
        var task = new TaskItem(FullDetails() with { DueDate = Today.AddDays(-1) }, CreatedAt);

        Assert.True(task.IsOverdue(Today));
    }

    [Fact]
    public void IsOverdue_DueToday_IsFalseUntilTheNextDay()
    {
        var task = new TaskItem(FullDetails() with { DueDate = Today }, CreatedAt);

        Assert.False(task.IsOverdue(Today));
        Assert.True(task.IsOverdue(Today.AddDays(1)));
    }

    [Fact]
    public void IsOverdue_DueDateInTheFuture_IsFalse()
    {
        var task = new TaskItem(FullDetails() with { DueDate = Today.AddDays(1) }, CreatedAt);

        Assert.False(task.IsOverdue(Today));
    }

    [Fact]
    public void IsOverdue_NoDueDate_IsFalse()
    {
        var task = new TaskItem(FullDetails() with { DueDate = null }, CreatedAt);

        Assert.False(task.IsOverdue(Today.AddYears(10)));
    }

    [Theory]
    [InlineData(TaskItemStatus.NotStarted)]
    [InlineData(TaskItemStatus.InProgress)]
    public void IsOverdue_PastDueAndNotCompleted_IsTrue(TaskItemStatus status)
    {
        var task = new TaskItem(FullDetails() with { DueDate = Today.AddDays(-3) }, CreatedAt);
        task.ChangeStatus(status, Later);

        Assert.True(task.IsOverdue(Today));
    }

    [Fact]
    public void IsOverdue_CompletedTask_IsNeverOverdue()
    {
        var task = new TaskItem(FullDetails() with { DueDate = Today.AddDays(-30) }, CreatedAt);
        task.ChangeStatus(TaskItemStatus.Completed, Later);

        Assert.False(task.IsOverdue(Today));
        Assert.False(task.IsOverdue(Today.AddYears(1)));
    }

    [Fact]
    public void IsOverdue_ReopenedTaskWithPastDueDate_IsOverdueAgain()
    {
        var task = new TaskItem(FullDetails() with { DueDate = Today.AddDays(-1) }, CreatedAt);
        task.ChangeStatus(TaskItemStatus.Completed, Later);
        task.ChangeStatus(TaskItemStatus.InProgress, Later.AddHours(1));

        Assert.True(task.IsOverdue(Today));
    }

    // Id

    [Fact]
    public void AssignId_PositiveId_SetsId()
    {
        var task = NewTask();

        task.AssignId(5);

        Assert.Equal(5, task.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AssignId_ZeroOrNegative_Throws(int id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewTask().AssignId(id));
    }

    [Fact]
    public void AssignId_AlreadyAssigned_ThrowsAndKeepsOldId()
    {
        var task = NewTask();
        task.AssignId(5);

        Assert.Throws<InvalidOperationException>(() => task.AssignId(6));

        Assert.Equal(5, task.Id);
    }

    private static void AssertRejectedAndUnchanged(TaskItemDetails invalid, string expectedField)
    {
        var task = NewTask();

        var exception = Assert.ThrowsAny<ArgumentException>(() => task.UpdateDetails(invalid, Later));

        Assert.Equal(expectedField, exception.ParamName);
        AssertHasDetails(task, FullDetails());
        Assert.Equal(CreatedAt, task.UpdatedAt);
    }

    private static void AssertHasDetails(TaskItem task, TaskItemDetails expected)
    {
        Assert.Equal(expected.Title, task.Title);
        Assert.Equal(expected.Description, task.Description);
        Assert.Equal(expected.Category, task.Category);
        Assert.Equal(expected.Priority, task.Priority);
        Assert.Equal(expected.DueDate, task.DueDate);
        Assert.Equal(expected.AssignedTo, task.AssignedTo);
        Assert.Equal(expected.HousingUnitId, task.HousingUnitId);
        Assert.Equal(expected.BoardCaseId, task.BoardCaseId);
        Assert.Equal(expected.Notes, task.Notes);
    }
}
