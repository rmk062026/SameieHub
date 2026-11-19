using SameieHub.Domain;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Domain;

public class TaskItemServiceTests
{
    private static readonly DateTime StartTime = new(2026, 11, 18, 10, 30, 0, DateTimeKind.Utc);

    private readonly FakeTaskItemRepository _taskRepository = new();
    private readonly ManualTimeProvider _time = new(StartTime);
    private readonly HousingUnitService _housingUnitService;
    private readonly BoardCaseService _boardCaseService;
    private readonly TaskItemService _service;

    public TaskItemServiceTests()
    {
        var housingUnitRepository = new FakeHousingUnitRepository();
        var boardCaseRepository = new FakeBoardCaseRepository();
        _housingUnitService = new HousingUnitService(housingUnitRepository);
        _boardCaseService = new BoardCaseService(boardCaseRepository, housingUnitRepository, _time);
        _service = new TaskItemService(_taskRepository, housingUnitRepository, boardCaseRepository, _time);
    }

    private static TaskItemDetails Details(string title = "Rense takrenner") => new() { Title = title };

    private async Task<TaskItem> RegisterTaskAsync(TaskItemDetails? details = null)
    {
        return await _service.RegisterAsync(details ?? Details());
    }

    private async Task<TaskItem> StoredAsync(TaskItem task)
    {
        return (await _service.GetByIdAsync(task.Id))!;
    }

    private void MoveClockForward()
    {
        _time.Advance(TimeSpan.FromHours(2));
    }

    private async Task<BoardCase> RegisterBoardCaseAsync(int? housingUnitId = null)
    {
        return await _boardCaseService.RegisterAsync(new BoardCaseDetails { Title = "Maling av porter", HousingUnitId = housingUnitId });
    }

    // Registration

    [Fact]
    public async Task RegisterAsync_OnlyTitle_ReturnsStoredTaskWithIdDefaultsAndTimestamps()
    {
        var task = await _service.RegisterAsync(Details());

        Assert.True(task.Id > 0);
        Assert.Equal(TaskItemStatus.NotStarted, task.Status);
        Assert.Equal(TaskItemCategory.General, task.Category);
        Assert.Equal(TaskItemPriority.Normal, task.Priority);
        Assert.Equal(StartTime, task.CreatedAt);
        Assert.Equal(StartTime, task.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, task.CreatedAt.Kind);
        Assert.Null(task.CompletedAt);
        Assert.NotNull(await _service.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task RegisterAsync_AllOptionalFields_AreStored()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterBoardCaseAsync();

        var task = await _service.RegisterAsync(new TaskItemDetails
        {
            Title = "Rense takrenner",
            Description = "Fiktiv beskrivelse.",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.High,
            DueDate = new DateOnly(2026, 12, 1),
            AssignedTo = "Test Person",
            HousingUnitId = unit.Id,
            BoardCaseId = boardCase.Id,
            Notes = "Fiktive merknader."
        });

        var stored = await StoredAsync(task);
        Assert.Equal(TaskItemCategory.Maintenance, stored.Category);
        Assert.Equal(TaskItemPriority.High, stored.Priority);
        Assert.Equal(new DateOnly(2026, 12, 1), stored.DueDate);
        Assert.Equal("Test Person", stored.AssignedTo);
        Assert.Equal(unit.Id, stored.HousingUnitId);
        Assert.Equal(boardCase.Id, stored.BoardCaseId);
        Assert.Equal("Fiktive merknader.", stored.Notes);
    }

    [Fact]
    public async Task RegisterAsync_UsesTheTimeFromTheTimeProvider()
    {
        MoveClockForward();

        var task = await _service.RegisterAsync(Details());

        Assert.Equal(StartTime.AddHours(2), task.CreatedAt);
    }

    [Fact]
    public async Task RegisterAsync_TwoTasks_GetDifferentIds()
    {
        var first = await _service.RegisterAsync(Details("Oppgave 1"));
        var second = await _service.RegisterAsync(Details("Oppgave 2"));

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task RegisterAsync_InvalidTitle_ThrowsAndStoresNothing()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterAsync(Details("   ")));

        Assert.Empty(await _service.GetAllAsync());
    }

    // Retrieval

    [Fact]
    public async Task GetByIdAsync_ExistingTask_ReturnsIt()
    {
        var registered = await RegisterTaskAsync();

        Assert.Equal("Rense takrenner", (await _service.GetByIdAsync(registered.Id))!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await _service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_NoTasks_ReturnsEmptyList()
    {
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_TwoTasks_ReturnsBoth()
    {
        await RegisterTaskAsync(Details("Oppgave 1"));
        await RegisterTaskAsync(Details("Oppgave 2"));

        var tasks = await _service.GetAllAsync();

        Assert.Equal(["Oppgave 1", "Oppgave 2"], tasks.Select(task => task.Title));
    }

    // Update

    [Fact]
    public async Task UpdateDetailsAsync_NewValues_AreStoredAndUpdatedAtChanges()
    {
        var task = await RegisterTaskAsync();
        MoveClockForward();

        await _service.UpdateDetailsAsync(task.Id, new TaskItemDetails
        {
            Title = "Ny tittel",
            Category = TaskItemCategory.Maintenance,
            Priority = TaskItemPriority.Low,
            DueDate = new DateOnly(2026, 12, 3),
            AssignedTo = "Test Person"
        });

        var stored = await StoredAsync(task);
        Assert.Equal("Ny tittel", stored.Title);
        Assert.Equal(TaskItemCategory.Maintenance, stored.Category);
        Assert.Equal(TaskItemPriority.Low, stored.Priority);
        Assert.Equal(StartTime, stored.CreatedAt);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnchangedValues_DoNotChangeUpdatedAt()
    {
        var task = await RegisterTaskAsync();
        MoveClockForward();

        await _service.UpdateDetailsAsync(task.Id, Details());

        Assert.Equal(StartTime, (await StoredAsync(task)).UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_InvalidValue_ThrowsAndKeepsEverythingIncludingUpdatedAt()
    {
        var task = await RegisterTaskAsync();
        MoveClockForward();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.UpdateDetailsAsync(task.Id, new TaskItemDetails
        {
            Title = "Ny tittel",
            Priority = TaskItemPriority.High,
            DueDate = new DateOnly(1999, 1, 1)
        }));

        var stored = await StoredAsync(task);
        Assert.Equal("Rense takrenner", stored.Title);
        Assert.Equal(TaskItemPriority.Normal, stored.Priority);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateDetailsAsync(999, Details()));
    }

    // Status

    [Fact]
    public async Task ChangeStatusAsync_Start_IsStoredAndUpdatedAtChanges()
    {
        var task = await RegisterTaskAsync();
        MoveClockForward();

        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);

        var stored = await StoredAsync(task);
        Assert.Equal(TaskItemStatus.InProgress, stored.Status);
        Assert.Null(stored.CompletedAt);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_Complete_SetsCompletedAtFromTheTimeProviderInUtc()
    {
        var task = await RegisterTaskAsync();
        MoveClockForward();

        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        var stored = await StoredAsync(task);
        Assert.Equal(TaskItemStatus.Completed, stored.Status);
        Assert.Equal(StartTime.AddHours(2), stored.CompletedAt);
        Assert.Equal(DateTimeKind.Utc, stored.CompletedAt!.Value.Kind);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_Reopen_ClearsCompletedAt()
    {
        var task = await RegisterTaskAsync();
        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        MoveClockForward();

        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);

        var stored = await StoredAsync(task);
        Assert.Equal(TaskItemStatus.InProgress, stored.Status);
        Assert.Null(stored.CompletedAt);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_CompletedAgainAfterReopening_GetsTheNewTime()
    {
        var task = await RegisterTaskAsync();
        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        MoveClockForward();
        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);
        MoveClockForward();

        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        Assert.Equal(StartTime.AddHours(4), (await StoredAsync(task)).CompletedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.NotStarted)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Completed)]
    public async Task ChangeStatusAsync_SameStatus_ChangesNoTimestamps(TaskItemStatus status)
    {
        var task = await RegisterTaskAsync();
        await _service.ChangeStatusAsync(task.Id, status);
        var before = await StoredAsync(task);
        var completedAt = before.CompletedAt;
        var updatedAt = before.UpdatedAt;
        MoveClockForward();

        await _service.ChangeStatusAsync(task.Id, status);

        var after = await StoredAsync(task);
        Assert.Equal(completedAt, after.CompletedAt);
        Assert.Equal(updatedAt, after.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_UndefinedStatus_ThrowsAndKeepsTheTask()
    {
        var task = await RegisterTaskAsync();
        MoveClockForward();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.ChangeStatusAsync(task.Id, (TaskItemStatus)99));

        var stored = await StoredAsync(task);
        Assert.Equal(TaskItemStatus.NotStarted, stored.Status);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeStatusAsync(999, TaskItemStatus.InProgress));
    }

    [Fact]
    public async Task UpdateDetailsAsync_CompletedTask_CanBeEditedAndKeepsCompletedAt()
    {
        var task = await RegisterTaskAsync();
        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        MoveClockForward();

        await _service.UpdateDetailsAsync(task.Id, Details("Rettet tittel"));

        var stored = await StoredAsync(task);
        Assert.Equal("Rettet tittel", stored.Title);
        Assert.Equal(TaskItemStatus.Completed, stored.Status);
        Assert.Equal(StartTime, stored.CompletedAt);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    // Housing unit link

    [Fact]
    public async Task RegisterAsync_WithActiveHousingUnit_StoresTheLink()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");

        var task = await RegisterTaskAsync(Details() with { HousingUnitId = unit.Id });

        Assert.Equal(unit.Id, task.HousingUnitId);
    }

    [Fact]
    public async Task RegisterAsync_UnknownHousingUnit_ThrowsWithParamNameAndStoresNothing()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterAsync(Details() with { HousingUnitId = 999 }));

        Assert.Equal("HousingUnitId", exception.ParamName);
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task RegisterAsync_InactiveHousingUnit_ThrowsAndStoresNothing()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        await _housingUnitService.DeactivateAsync(unit.Id);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterAsync(Details() with { HousingUnitId = unit.Id }));

        Assert.Equal("HousingUnitId", exception.ParamName);
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task UpdateDetailsAsync_LinkToOtherActiveUnit_ChangesTheLink()
    {
        var first = await _housingUnitService.RegisterAsync("Storgata 1A");
        var second = await _housingUnitService.RegisterAsync("Storgata 1B");
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = first.Id });

        await _service.UpdateDetailsAsync(task.Id, Details() with { HousingUnitId = second.Id });

        Assert.Equal(second.Id, (await StoredAsync(task)).HousingUnitId);
    }

    [Fact]
    public async Task UpdateDetailsAsync_LinkToInactiveOrUnknownUnit_ThrowsAndChangesNothing()
    {
        var active = await _housingUnitService.RegisterAsync("Storgata 1A");
        var inactive = await _housingUnitService.RegisterAsync("Storgata 1B");
        await _housingUnitService.DeactivateAsync(inactive.Id);
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = active.Id });
        MoveClockForward();

        var inactiveError = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(task.Id, Details("Ny tittel") with { HousingUnitId = inactive.Id }));
        var unknownError = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(task.Id, Details("Ny tittel") with { HousingUnitId = 999 }));

        Assert.Equal("HousingUnitId", inactiveError.ParamName);
        Assert.Equal("HousingUnitId", unknownError.ParamName);
        var stored = await StoredAsync(task);
        Assert.Equal(active.Id, stored.HousingUnitId);
        Assert.Equal("Rense takrenner", stored.Title);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnitDeactivatedAfterLinking_KeepsLinkAndOtherFieldsCanBeEdited()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = unit.Id });
        await _housingUnitService.DeactivateAsync(unit.Id);

        await _service.UpdateDetailsAsync(task.Id, Details("Ny tittel") with { HousingUnitId = unit.Id });

        var stored = await StoredAsync(task);
        Assert.Equal("Ny tittel", stored.Title);
        Assert.Equal(unit.Id, stored.HousingUnitId);
    }

    [Fact]
    public async Task ChangeStatusAsync_UnitDeactivatedAfterLinking_StillWorks()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = unit.Id });
        await _housingUnitService.DeactivateAsync(unit.Id);

        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        Assert.Equal(TaskItemStatus.Completed, (await StoredAsync(task)).Status);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_ActiveUnit_LinksAndKeepsOtherValues()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var task = await RegisterTaskAsync(Details() with { Priority = TaskItemPriority.High, Notes = "Merknad" });
        MoveClockForward();

        await _service.ChangeHousingUnitAsync(task.Id, unit.Id);

        var stored = await StoredAsync(task);
        Assert.Equal(unit.Id, stored.HousingUnitId);
        Assert.Equal(TaskItemPriority.High, stored.Priority);
        Assert.Equal("Merknad", stored.Notes);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_Null_RemovesTheLinkEvenWhenTheUnitIsInactive()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = unit.Id });
        await _housingUnitService.DeactivateAsync(unit.Id);

        await _service.ChangeHousingUnitAsync(task.Id, null);

        Assert.Null((await StoredAsync(task)).HousingUnitId);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_InactiveOrUnknownUnit_ThrowsAndKeepsLink()
    {
        var active = await _housingUnitService.RegisterAsync("Storgata 1A");
        var inactive = await _housingUnitService.RegisterAsync("Storgata 1B");
        await _housingUnitService.DeactivateAsync(inactive.Id);
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = active.Id });

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ChangeHousingUnitAsync(task.Id, inactive.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ChangeHousingUnitAsync(task.Id, 999));

        Assert.Equal(active.Id, (await StoredAsync(task)).HousingUnitId);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_SameDeactivatedUnit_IsAcceptedAndChangesNothing()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = unit.Id });
        await _housingUnitService.DeactivateAsync(unit.Id);
        MoveClockForward();

        await _service.ChangeHousingUnitAsync(task.Id, unit.Id);

        var stored = await StoredAsync(task);
        Assert.Equal(unit.Id, stored.HousingUnitId);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    // Board case link

    [Fact]
    public async Task RegisterAsync_WithExistingBoardCase_StoresTheLink()
    {
        var boardCase = await RegisterBoardCaseAsync();

        var task = await RegisterTaskAsync(Details() with { BoardCaseId = boardCase.Id });

        Assert.Equal(boardCase.Id, task.BoardCaseId);
    }

    [Theory]
    [InlineData(BoardCaseStatus.New)]
    [InlineData(BoardCaseStatus.InProgress)]
    [InlineData(BoardCaseStatus.Closed)]
    public async Task RegisterAsync_BoardCaseWithAnyStatus_CanBeLinked(BoardCaseStatus status)
    {
        var boardCase = await RegisterBoardCaseAsync();
        await _boardCaseService.ChangeStatusAsync(boardCase.Id, status);

        var task = await RegisterTaskAsync(Details() with { BoardCaseId = boardCase.Id });

        Assert.Equal(boardCase.Id, task.BoardCaseId);
    }

    [Fact]
    public async Task RegisterAsync_DecidedBoardCase_CanBeLinked()
    {
        var boardCase = await RegisterBoardCaseAsync();
        await _boardCaseService.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        await _boardCaseService.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);

        var task = await RegisterTaskAsync(Details() with { BoardCaseId = boardCase.Id });

        Assert.Equal(boardCase.Id, task.BoardCaseId);
    }

    [Fact]
    public async Task RegisterAsync_UnknownBoardCase_ThrowsWithParamNameAndStoresNothing()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterAsync(Details() with { BoardCaseId = 999 }));

        Assert.Equal("BoardCaseId", exception.ParamName);
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnknownBoardCase_ThrowsAndChangesNothing()
    {
        var task = await RegisterTaskAsync();
        MoveClockForward();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(task.Id, Details("Ny tittel") with { BoardCaseId = 999 }));

        Assert.Equal("BoardCaseId", exception.ParamName);
        var stored = await StoredAsync(task);
        Assert.Null(stored.BoardCaseId);
        Assert.Equal("Rense takrenner", stored.Title);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeBoardCaseAsync_ChangesAndRemovesTheLink()
    {
        var first = await RegisterBoardCaseAsync();
        var second = await RegisterBoardCaseAsync();
        var task = await RegisterTaskAsync(Details() with { BoardCaseId = first.Id });

        await _service.ChangeBoardCaseAsync(task.Id, second.Id);
        Assert.Equal(second.Id, (await StoredAsync(task)).BoardCaseId);

        await _service.ChangeBoardCaseAsync(task.Id, null);
        Assert.Null((await StoredAsync(task)).BoardCaseId);
    }

    [Fact]
    public async Task ChangeBoardCaseAsync_UnknownBoardCase_ThrowsAndKeepsLink()
    {
        var boardCase = await RegisterBoardCaseAsync();
        var task = await RegisterTaskAsync(Details() with { BoardCaseId = boardCase.Id });

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ChangeBoardCaseAsync(task.Id, 999));

        Assert.Equal(boardCase.Id, (await StoredAsync(task)).BoardCaseId);
    }

    [Fact]
    public async Task ChangeBoardCaseAsync_UnknownTask_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeBoardCaseAsync(999, null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeHousingUnitAsync(999, null));
    }

    // Both links

    [Fact]
    public async Task RegisterAsync_LinkedToBothUnitAndBoardCase_StoresBoth()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterBoardCaseAsync();

        var task = await RegisterTaskAsync(Details() with { HousingUnitId = unit.Id, BoardCaseId = boardCase.Id });

        Assert.Equal((unit.Id, boardCase.Id), (task.HousingUnitId, task.BoardCaseId));
    }

    [Fact]
    public async Task Links_AreIndependent_BoardCaseCanConcernAnotherUnitOrTheWholeCooperative()
    {
        var taskUnit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var caseUnit = await _housingUnitService.RegisterAsync("Storgata 1B");
        var caseForOtherUnit = await RegisterBoardCaseAsync(caseUnit.Id);
        var caseForWholeCooperative = await RegisterBoardCaseAsync();

        var withOtherUnit = await RegisterTaskAsync(Details("Sak A") with { HousingUnitId = taskUnit.Id, BoardCaseId = caseForOtherUnit.Id });
        var withoutUnit = await RegisterTaskAsync(Details("Sak B") with { BoardCaseId = caseForOtherUnit.Id });
        var wholeCooperative = await RegisterTaskAsync(Details("Sak C") with { HousingUnitId = taskUnit.Id, BoardCaseId = caseForWholeCooperative.Id });

        Assert.Equal(taskUnit.Id, withOtherUnit.HousingUnitId);
        Assert.Equal(caseUnit.Id, (await _boardCaseService.GetByIdAsync(caseForOtherUnit.Id))!.HousingUnitId);
        Assert.Null(withoutUnit.HousingUnitId);
        Assert.Equal(taskUnit.Id, wholeCooperative.HousingUnitId);
    }

    [Fact]
    public async Task ChangeBoardCaseAsync_DoesNotCopyTheBoardCasesHousingUnit()
    {
        var caseUnit = await _housingUnitService.RegisterAsync("Storgata 1B");
        var boardCase = await RegisterBoardCaseAsync(caseUnit.Id);
        var task = await RegisterTaskAsync();

        await _service.ChangeBoardCaseAsync(task.Id, boardCase.Id);

        Assert.Null((await StoredAsync(task)).HousingUnitId);
    }

    // Atomic update

    [Fact]
    public async Task UpdateDetailsAsync_ValidUnitButUnknownBoardCase_ChangesNothing()
    {
        var oldUnit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var newUnit = await _housingUnitService.RegisterAsync("Storgata 1B");
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = oldUnit.Id });
        MoveClockForward();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(task.Id, Details("Ny tittel") with { HousingUnitId = newUnit.Id, BoardCaseId = 999 }));

        var stored = await StoredAsync(task);
        Assert.Equal(oldUnit.Id, stored.HousingUnitId);
        Assert.Null(stored.BoardCaseId);
        Assert.Equal("Rense takrenner", stored.Title);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnknownUnitButValidBoardCase_ChangesNothing()
    {
        var boardCase = await RegisterBoardCaseAsync();
        var task = await RegisterTaskAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(task.Id, Details("Ny tittel") with { HousingUnitId = 999, BoardCaseId = boardCase.Id }));

        var stored = await StoredAsync(task);
        Assert.Null(stored.HousingUnitId);
        Assert.Null(stored.BoardCaseId);
        Assert.Equal("Rense takrenner", stored.Title);
    }

    [Fact]
    public async Task UpdateDetailsAsync_ValidReferencesButInvalidField_ChangesNothing()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterBoardCaseAsync();
        var task = await RegisterTaskAsync();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.UpdateDetailsAsync(task.Id, new TaskItemDetails
        {
            Title = "Ny tittel",
            HousingUnitId = unit.Id,
            BoardCaseId = boardCase.Id,
            DueDate = new DateOnly(1999, 1, 1)
        }));

        var stored = await StoredAsync(task);
        Assert.Null(stored.HousingUnitId);
        Assert.Null(stored.BoardCaseId);
        Assert.Equal("Rense takrenner", stored.Title);
    }

    [Fact]
    public async Task UpdateDetailsAsync_BothReferencesChangedAtOnce_AreBothStored()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterBoardCaseAsync();
        var task = await RegisterTaskAsync();

        await _service.UpdateDetailsAsync(task.Id, Details() with { HousingUnitId = unit.Id, BoardCaseId = boardCase.Id });

        var stored = await StoredAsync(task);
        Assert.Equal(unit.Id, stored.HousingUnitId);
        Assert.Equal(boardCase.Id, stored.BoardCaseId);
    }

    // Deletion

    [Fact]
    public async Task DeleteAsync_ExistingTask_RemovesOnlyThatTask()
    {
        var first = await RegisterTaskAsync(Details("Oppgave 1"));
        var second = await RegisterTaskAsync(Details("Oppgave 2"));

        await _service.DeleteAsync(first.Id);

        Assert.Null(await _service.GetByIdAsync(first.Id));
        Assert.Equal(second.Id, Assert.Single(await _service.GetAllAsync()).Id);
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteAsync(999));
    }

    [Fact]
    public async Task DeleteAsync_TaskWithLinks_DoesNotDeleteTheUnitOrTheBoardCase()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterBoardCaseAsync();
        var task = await RegisterTaskAsync(Details() with { HousingUnitId = unit.Id, BoardCaseId = boardCase.Id });

        await _service.DeleteAsync(task.Id);

        Assert.Null(await _service.GetByIdAsync(task.Id));
        Assert.NotNull(await _housingUnitService.GetByIdAsync(unit.Id));
        Assert.NotNull(await _boardCaseService.GetByIdAsync(boardCase.Id));
    }

    // Overdue and today

    [Fact]
    public void GetToday_WinterEvening_IsTheNorwegianDate()
    {
        Assert.Equal(new DateOnly(2026, 11, 18), _service.GetToday());
    }

    [Fact]
    public void GetToday_ShortlyAfterMidnightInNorway_IsAlreadyTheNextDate()
    {
        var time = new ManualTimeProvider(new DateTime(2026, 11, 18, 23, 30, 0, DateTimeKind.Utc));
        var service = new TaskItemService(_taskRepository, new FakeHousingUnitRepository(), new FakeBoardCaseRepository(), time);

        Assert.Equal(new DateOnly(2026, 11, 19), service.GetToday());
    }

    [Fact]
    public void GetToday_SummerTime_UsesTwoHoursOffset()
    {
        var time = new ManualTimeProvider(new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc));
        var service = new TaskItemService(_taskRepository, new FakeHousingUnitRepository(), new FakeBoardCaseRepository(), time);

        Assert.Equal(new DateOnly(2026, 7, 2), service.GetToday());
    }

    [Fact]
    public async Task GetOverdueAsync_ReturnsOnlyOverdueTasksOrderedByDueDate()
    {
        await RegisterTaskAsync(Details("Forfalt sent") with { DueDate = new DateOnly(2026, 11, 17) });
        await RegisterTaskAsync(Details("Forfalt tidlig") with { DueDate = new DateOnly(2026, 11, 1) });
        await RegisterTaskAsync(Details("Frist i dag") with { DueDate = new DateOnly(2026, 11, 18) });
        await RegisterTaskAsync(Details("Frist senere") with { DueDate = new DateOnly(2026, 11, 19) });
        await RegisterTaskAsync(Details("Uten frist"));

        var overdue = await _service.GetOverdueAsync();

        Assert.Equal(["Forfalt tidlig", "Forfalt sent"], overdue.Select(task => task.Title));
    }

    [Fact]
    public async Task GetOverdueAsync_CompletedTasks_AreNotOverdue()
    {
        var task = await RegisterTaskAsync(Details() with { DueDate = new DateOnly(2026, 11, 1) });
        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);

        Assert.Empty(await _service.GetOverdueAsync());
    }

    [Fact]
    public async Task GetOverdueAsync_TaskDueToday_BecomesOverdueWhenTheNorwegianDateChanges()
    {
        await RegisterTaskAsync(Details() with { DueDate = new DateOnly(2026, 11, 18) });
        Assert.Empty(await _service.GetOverdueAsync());

        _time.Advance(TimeSpan.FromHours(13));

        Assert.Single(await _service.GetOverdueAsync());
    }

    [Fact]
    public async Task GetOverdueAsync_ReopenedTask_IsOverdueAgain()
    {
        var task = await RegisterTaskAsync(Details() with { DueDate = new DateOnly(2026, 11, 1) });
        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.Completed);
        await _service.ChangeStatusAsync(task.Id, TaskItemStatus.InProgress);

        Assert.Single(await _service.GetOverdueAsync());
    }
}
