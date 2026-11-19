using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class TaskItemPersistenceTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    private static TaskItemDetails FullDetails() => new()
    {
        Title = "Rense takrenner",
        Description = "Fiktiv beskrivelse.",
        Category = TaskItemCategory.Maintenance,
        Priority = TaskItemPriority.High,
        DueDate = new DateOnly(2026, 12, 1),
        AssignedTo = "Test Person",
        Notes = "Fiktive merknader."
    };

    private async Task<TaskItem> StoredAsync(TaskItem task)
    {
        return (await _database.Tasks.GetByIdAsync(task.Id))!;
    }

    // Storing and reading

    [Fact]
    public async Task Register_AllDetails_AreReadBackAfterRestart()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());

        await _database.RestartAsync();

        var found = await _database.Tasks.GetByIdAsync(registered.Id);
        Assert.NotNull(found);
        Assert.Equal("Rense takrenner", found.Title);
        Assert.Equal("Fiktiv beskrivelse.", found.Description);
        Assert.Equal(TaskItemCategory.Maintenance, found.Category);
        Assert.Equal(TaskItemStatus.NotStarted, found.Status);
        Assert.Equal(TaskItemPriority.High, found.Priority);
        Assert.Equal(new DateOnly(2026, 12, 1), found.DueDate);
        Assert.Equal("Test Person", found.AssignedTo);
        Assert.Null(found.HousingUnitId);
        Assert.Null(found.BoardCaseId);
        Assert.Null(found.CompletedAt);
        Assert.Equal("Fiktive merknader.", found.Notes);
    }

    [Fact]
    public async Task Register_OnlyTitle_StoresDefaultsAndNullsForOptionalValues()
    {
        var registered = await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Bytt pære" });

        await _database.RestartAsync();

        var found = await _database.Tasks.GetByIdAsync(registered.Id);
        Assert.Equal(TaskItemCategory.General, found!.Category);
        Assert.Equal(TaskItemStatus.NotStarted, found.Status);
        Assert.Equal(TaskItemPriority.Normal, found.Priority);
        Assert.Null(found.Description);
        Assert.Null(found.DueDate);
        Assert.Null(found.AssignedTo);
        Assert.Null(found.HousingUnitId);
        Assert.Null(found.BoardCaseId);
        Assert.Null(found.CompletedAt);
        Assert.Null(found.Notes);
        Assert.Equal("1", await _database.QueryTextAsync(
            "SELECT COUNT(*) FROM TaskItems WHERE Description IS NULL AND DueDate IS NULL AND AssignedTo IS NULL " +
            "AND HousingUnitId IS NULL AND BoardCaseId IS NULL AND CompletedAt IS NULL AND Notes IS NULL"));
    }

    [Fact]
    public async Task Register_GetsPositiveIdFromDatabaseSetOnTheObject()
    {
        var first = await _database.Tasks.RegisterAsync(FullDetails());
        var second = await _database.Tasks.RegisterAsync(FullDetails());

        Assert.True(first.Id > 0);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task GetAll_ReturnsAllTasksInIdOrder()
    {
        await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave 1" });
        await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave 2" });

        var tasks = await _database.Tasks.GetAllAsync();

        Assert.Equal(["Oppgave 1", "Oppgave 2"], tasks.Select(task => task.Title));
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(await _database.Tasks.GetByIdAsync(999));
    }

    // Changes

    [Fact]
    public async Task UpdateDetails_IsStoredAndCreatedAtIsKept()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(2));

        await _database.Tasks.UpdateDetailsAsync(registered.Id, FullDetails() with
        {
            Title = "Ny tittel",
            Priority = TaskItemPriority.Low,
            DueDate = null,
            Notes = null
        });
        await _database.RestartAsync();

        var found = await StoredAsync(registered);
        Assert.Equal("Ny tittel", found.Title);
        Assert.Equal(TaskItemPriority.Low, found.Priority);
        Assert.Null(found.DueDate);
        Assert.Null(found.Notes);
        Assert.Equal(registered.CreatedAt, found.CreatedAt);
        Assert.Equal(registered.CreatedAt.AddHours(2), found.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetails_NothingChanged_KeepsStoredUpdatedAt()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(2));

        await _database.Tasks.UpdateDetailsAsync(registered.Id, FullDetails());
        await _database.RestartAsync();

        Assert.Equal(registered.CreatedAt, (await StoredAsync(registered)).UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetails_InvalidValue_KeepsStoredValues()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _database.Tasks.UpdateDetailsAsync(registered.Id, FullDetails() with { Title = "Ny tittel", DueDate = new DateOnly(1999, 1, 1) }));
        await _database.RestartAsync();

        Assert.Equal("Rense takrenner", (await StoredAsync(registered)).Title);
    }

    [Fact]
    public async Task ChangeStatus_Start_IsStored()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());

        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.InProgress);
        await _database.RestartAsync();

        var found = await StoredAsync(registered);
        Assert.Equal(TaskItemStatus.InProgress, found.Status);
        Assert.Null(found.CompletedAt);
    }

    // Completion time and UTC

    [Fact]
    public async Task Complete_CompletedAtIsUtcAndEqualBeforeAndAfterStorage()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromTicks(7654321));
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.Completed);
        var completedBeforeStorage = (await StoredAsync(registered)).CompletedAt;

        await _database.RestartAsync();
        var found = await StoredAsync(registered);

        Assert.Equal(TaskItemStatus.Completed, found.Status);
        Assert.NotNull(found.CompletedAt);
        Assert.Equal(DateTimeKind.Utc, found.CompletedAt.Value.Kind);
        Assert.Equal(completedBeforeStorage, found.CompletedAt);
        Assert.Equal(registered.CreatedAt.AddHours(1).AddTicks(7654321), found.CompletedAt);
        Assert.Equal(found.CompletedAt, found.UpdatedAt);
    }

    [Fact]
    public async Task Reopen_ClearsCompletedAtAlsoAfterRestart()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.Completed);
        await _database.RestartAsync();
        _database.Clock.Advance(TimeSpan.FromHours(1));

        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.InProgress);
        await _database.RestartAsync();

        var found = await StoredAsync(registered);
        Assert.Equal(TaskItemStatus.InProgress, found.Status);
        Assert.Null(found.CompletedAt);
        Assert.Equal("1", await _database.QueryTextAsync("SELECT COUNT(*) FROM TaskItems WHERE CompletedAt IS NULL"));
    }

    [Fact]
    public async Task Complete_AgainAfterReopening_StoresTheNewTime()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.Completed);
        _database.Clock.Advance(TimeSpan.FromHours(1));
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.InProgress);
        _database.Clock.Advance(TimeSpan.FromHours(1));

        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.Completed);
        await _database.RestartAsync();

        Assert.Equal(registered.CreatedAt.AddHours(2), (await StoredAsync(registered)).CompletedAt);
    }

    [Fact]
    public async Task EditingACompletedTask_KeepsCompletedAtAfterRestart()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.Completed);
        _database.Clock.Advance(TimeSpan.FromHours(3));

        await _database.Tasks.UpdateDetailsAsync(registered.Id, FullDetails() with { Title = "Rettet tittel" });
        await _database.RestartAsync();

        var found = await StoredAsync(registered);
        Assert.Equal("Rettet tittel", found.Title);
        Assert.Equal(registered.CreatedAt, found.CompletedAt);
        Assert.Equal(registered.CreatedAt.AddHours(3), found.UpdatedAt);
    }

    [Fact]
    public async Task CreatedAtAndUpdatedAt_AreUtcAndEqualAfterRestart()
    {
        _database.Clock.Advance(TimeSpan.FromTicks(1234567));
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromTicks(7654321));
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.InProgress);
        var updatedBeforeStorage = (await StoredAsync(registered)).UpdatedAt;

        await _database.RestartAsync();
        var found = await StoredAsync(registered);

        Assert.Equal(DateTimeKind.Utc, found.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, found.UpdatedAt.Kind);
        Assert.Equal(registered.CreatedAt, found.CreatedAt);
        Assert.Equal(updatedBeforeStorage, found.UpdatedAt);
    }

    [Fact]
    public async Task Timestamps_AreStoredAsUtcTextWithoutTimeZoneShift()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.Completed);

        Assert.StartsWith("2026-11-18 10:30:00", await _database.QueryTextAsync("SELECT CreatedAt FROM TaskItems"));
        Assert.StartsWith("2026-11-18 10:30:00", await _database.QueryTextAsync("SELECT CompletedAt FROM TaskItems"));
    }

    [Fact]
    public async Task Reading_DoesNotChangeUpdatedAt()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(5));

        await _database.Tasks.GetByIdAsync(registered.Id);
        await _database.Tasks.GetAllAsync();
        await _database.RestartAsync();

        Assert.Equal(registered.CreatedAt, (await StoredAsync(registered)).UpdatedAt);
    }

    // Dates and enums as text

    [Fact]
    public async Task DueDate_IsStoredAsDateWithoutTime_AndNullStaysNull()
    {
        await _database.Tasks.RegisterAsync(FullDetails());
        await _database.Tasks.RegisterAsync(FullDetails() with { DueDate = null });

        Assert.Equal("2026-12-01", await _database.QueryTextAsync("SELECT DueDate FROM TaskItems WHERE Id = 1"));
        Assert.Equal("1", await _database.QueryTextAsync("SELECT COUNT(*) FROM TaskItems WHERE DueDate IS NULL"));
    }

    [Fact]
    public async Task CategoryStatusAndPriority_AreStoredAsReadableText()
    {
        var registered = await _database.Tasks.RegisterAsync(FullDetails());
        await _database.Tasks.ChangeStatusAsync(registered.Id, TaskItemStatus.InProgress);

        Assert.Equal("Maintenance", await _database.QueryTextAsync("SELECT Category FROM TaskItems"));
        Assert.Equal("InProgress", await _database.QueryTextAsync("SELECT Status FROM TaskItems"));
        Assert.Equal("High", await _database.QueryTextAsync("SELECT Priority FROM TaskItems"));
    }

    // Overdue (calculated, never stored)

    [Fact]
    public async Task GetOverdue_UsesTheNorwegianDateAndTheStoredDueDate()
    {
        await _database.Tasks.RegisterAsync(FullDetails() with { Title = "Forfalt", DueDate = new DateOnly(2026, 11, 17) });
        await _database.Tasks.RegisterAsync(FullDetails() with { Title = "Frist i dag", DueDate = new DateOnly(2026, 11, 18) });
        var completed = await _database.Tasks.RegisterAsync(FullDetails() with { Title = "Fullført", DueDate = new DateOnly(2026, 11, 1) });
        await _database.Tasks.ChangeStatusAsync(completed.Id, TaskItemStatus.Completed);
        await _database.RestartAsync();

        var overdue = await _database.Tasks.GetOverdueAsync();

        Assert.Equal("Forfalt", Assert.Single(overdue).Title);
    }

    [Fact]
    public async Task Overdue_IsNotAColumnInTheDatabase()
    {
        Assert.Equal("0", await _database.QueryTextAsync(
            "SELECT COUNT(*) FROM pragma_table_info('TaskItems') WHERE LOWER(name) LIKE '%overdue%'"));
    }

    // Deletion

    [Fact]
    public async Task Delete_ExistingTask_IsGoneAfterRestartAndOtherTasksRemain()
    {
        var deleted = await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave 1" });
        var kept = await _database.Tasks.RegisterAsync(new TaskItemDetails { Title = "Oppgave 2" });

        await _database.Tasks.DeleteAsync(deleted.Id);
        await _database.RestartAsync();

        Assert.Null(await _database.Tasks.GetByIdAsync(deleted.Id));
        Assert.Equal(kept.Id, Assert.Single(await _database.Tasks.GetAllAsync()).Id);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsFalseFromRepositoryAndThrowsFromService()
    {
        Assert.False(await _database.TaskRepository.DeleteAsync(999));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _database.Tasks.DeleteAsync(999));
    }

    [Fact]
    public async Task Delete_DoesNotReuseTheDeletedId()
    {
        var deleted = await _database.Tasks.RegisterAsync(FullDetails());
        await _database.Tasks.DeleteAsync(deleted.Id);

        var next = await _database.Tasks.RegisterAsync(FullDetails());

        Assert.True(next.Id > deleted.Id);
    }
}
