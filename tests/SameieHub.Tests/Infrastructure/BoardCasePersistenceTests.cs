using SameieHub.Domain;

namespace SameieHub.Tests.Infrastructure;

public class BoardCasePersistenceTests : IAsyncLifetime
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

    private static BoardCaseDetails FullDetails() => new()
    {
        Title = "Maling av fellesarealer",
        Description = "Fiktiv beskrivelse.",
        Priority = BoardCasePriority.High,
        MeetingDate = new DateOnly(2026, 12, 3),
        Notes = "Fiktive merknader."
    };

    // Storing and reading

    [Fact]
    public async Task Register_AllDetails_AreReadBackAfterRestart()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());

        await _database.RestartAsync();

        var found = await _database.BoardCases.GetByIdAsync(registered.Id);
        Assert.NotNull(found);
        Assert.Equal("Maling av fellesarealer", found.Title);
        Assert.Equal("Fiktiv beskrivelse.", found.Description);
        Assert.Equal(BoardCaseStatus.New, found.Status);
        Assert.Equal(BoardCasePriority.High, found.Priority);
        Assert.Equal(new DateOnly(2026, 12, 3), found.MeetingDate);
        Assert.Null(found.Decision);
        Assert.Null(found.HousingUnitId);
        Assert.Equal("Fiktive merknader.", found.Notes);
    }

    [Fact]
    public async Task Register_OnlyTitle_StoresDefaultsAndNoOptionalValues()
    {
        var registered = await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Vedtekter" });

        var found = await _database.BoardCases.GetByIdAsync(registered.Id);

        Assert.Equal(BoardCaseStatus.New, found!.Status);
        Assert.Equal(BoardCasePriority.Normal, found.Priority);
        Assert.Null(found.Description);
        Assert.Null(found.MeetingDate);
        Assert.Null(found.Decision);
        Assert.Null(found.HousingUnitId);
        Assert.Null(found.Notes);
    }

    [Fact]
    public async Task Register_GetsPositiveIdFromDatabaseSetOnTheObject()
    {
        var first = await _database.BoardCases.RegisterAsync(FullDetails());
        var second = await _database.BoardCases.RegisterAsync(FullDetails());

        Assert.True(first.Id > 0);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task GetAll_ReturnsAllCasesInIdOrder()
    {
        await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak 1" });
        await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak 2" });

        var cases = await _database.BoardCases.GetAllAsync();

        Assert.Equal(["Sak 1", "Sak 2"], cases.Select(boardCase => boardCase.Title));
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        Assert.Null(await _database.BoardCases.GetByIdAsync(999));
    }

    // Changes

    [Fact]
    public async Task UpdateDetails_IsStoredAndCreatedAtIsKept()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(2));

        await _database.BoardCases.UpdateDetailsAsync(registered.Id, FullDetails() with
        {
            Title = "Ny tittel",
            Priority = BoardCasePriority.Low,
            MeetingDate = null,
            Notes = null
        });
        await _database.RestartAsync();

        var found = await _database.BoardCases.GetByIdAsync(registered.Id);
        Assert.Equal("Ny tittel", found!.Title);
        Assert.Equal(BoardCasePriority.Low, found.Priority);
        Assert.Null(found.MeetingDate);
        Assert.Null(found.Notes);
        Assert.Equal(registered.CreatedAt, found.CreatedAt);
        Assert.Equal(registered.CreatedAt.AddHours(2), found.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetails_NothingChanged_KeepsStoredUpdatedAt()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(2));

        await _database.BoardCases.UpdateDetailsAsync(registered.Id, FullDetails());
        await _database.RestartAsync();

        var found = await _database.BoardCases.GetByIdAsync(registered.Id);
        Assert.Equal(registered.CreatedAt, found!.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetails_InvalidValue_KeepsStoredValues()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(2));

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _database.BoardCases.UpdateDetailsAsync(registered.Id, FullDetails() with { Title = "Ny tittel", MeetingDate = new DateOnly(1999, 1, 1) }));
        await _database.RestartAsync();

        var found = await _database.BoardCases.GetByIdAsync(registered.Id);
        Assert.Equal("Maling av fellesarealer", found!.Title);
        Assert.Equal(registered.CreatedAt, found.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatus_IsStored()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());

        await _database.BoardCases.ChangeStatusAsync(registered.Id, BoardCaseStatus.InProgress);
        await _database.RestartAsync();

        Assert.Equal(BoardCaseStatus.InProgress, (await _database.BoardCases.GetByIdAsync(registered.Id))!.Status);
    }

    [Fact]
    public async Task Decision_RegisteredCorrectedAndClearedAreStored()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());

        await _database.BoardCases.RegisterDecisionAsync(registered.Id, "Feil vedtak.");
        await _database.BoardCases.ChangeStatusAsync(registered.Id, BoardCaseStatus.Decided);
        await _database.BoardCases.RegisterDecisionAsync(registered.Id, "Riktig vedtak.");
        await _database.RestartAsync();
        var corrected = await _database.BoardCases.GetByIdAsync(registered.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.BoardCases.ClearDecisionAsync(registered.Id));
        await _database.BoardCases.ChangeStatusAsync(registered.Id, BoardCaseStatus.InProgress);
        await _database.BoardCases.ClearDecisionAsync(registered.Id);
        await _database.RestartAsync();
        var cleared = await _database.BoardCases.GetByIdAsync(registered.Id);

        Assert.Equal("Riktig vedtak.", corrected!.Decision);
        Assert.Equal(BoardCaseStatus.Decided, corrected.Status);
        Assert.Null(cleared!.Decision);
        Assert.Equal(BoardCaseStatus.InProgress, cleared.Status);
    }

    // Time and dates

    [Fact]
    public async Task Timestamps_AreUtcAndEqualBeforeAndAfterStorage()
    {
        _database.Clock.Advance(TimeSpan.FromTicks(1234567));
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromTicks(7654321));
        await _database.BoardCases.ChangeStatusAsync(registered.Id, BoardCaseStatus.InProgress);
        var updatedBeforeStorage = (await _database.BoardCases.GetByIdAsync(registered.Id))!.UpdatedAt;

        await _database.RestartAsync();
        var found = await _database.BoardCases.GetByIdAsync(registered.Id);

        Assert.Equal(DateTimeKind.Utc, found!.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, found.UpdatedAt.Kind);
        Assert.Equal(registered.CreatedAt, found.CreatedAt);
        Assert.Equal(updatedBeforeStorage, found.UpdatedAt);
        Assert.NotEqual(found.CreatedAt, found.UpdatedAt);
    }

    [Fact]
    public async Task Timestamps_AreStoredAsUtcTextWithoutTimeZoneShift()
    {
        await _database.BoardCases.RegisterAsync(FullDetails());

        var stored = await _database.QueryTextAsync("SELECT CreatedAt FROM BoardCases");

        Assert.StartsWith("2026-11-18 10:30:00", stored);
    }

    [Fact]
    public async Task Reading_DoesNotChangeUpdatedAt()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());
        _database.Clock.Advance(TimeSpan.FromHours(5));

        await _database.BoardCases.GetByIdAsync(registered.Id);
        await _database.BoardCases.GetAllAsync();
        await _database.RestartAsync();

        var found = await _database.BoardCases.GetByIdAsync(registered.Id);
        Assert.Equal(registered.CreatedAt, found!.UpdatedAt);
    }

    [Fact]
    public async Task MeetingDate_IsStoredAsDateWithoutTime_AndNullStaysNull()
    {
        await _database.BoardCases.RegisterAsync(FullDetails());
        await _database.BoardCases.RegisterAsync(FullDetails() with { MeetingDate = null });

        Assert.Equal("2026-12-03", await _database.QueryTextAsync("SELECT MeetingDate FROM BoardCases WHERE Id = 1"));
        Assert.Equal("1", await _database.QueryTextAsync("SELECT COUNT(*) FROM BoardCases WHERE MeetingDate IS NULL"));
    }

    // Enums as text

    [Fact]
    public async Task StatusAndPriority_AreStoredAsReadableText()
    {
        var registered = await _database.BoardCases.RegisterAsync(FullDetails());
        await _database.BoardCases.ChangeStatusAsync(registered.Id, BoardCaseStatus.InProgress);

        Assert.Equal("InProgress", await _database.QueryTextAsync("SELECT Status FROM BoardCases"));
        Assert.Equal("High", await _database.QueryTextAsync("SELECT Priority FROM BoardCases"));
    }

    // Deletion

    [Fact]
    public async Task Delete_ExistingCase_IsGoneAfterRestartAndOtherCasesRemain()
    {
        var deleted = await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak 1" });
        var kept = await _database.BoardCases.RegisterAsync(new BoardCaseDetails { Title = "Sak 2" });

        await _database.BoardCases.DeleteAsync(deleted.Id);
        await _database.RestartAsync();

        Assert.Null(await _database.BoardCases.GetByIdAsync(deleted.Id));
        Assert.Equal(kept.Id, Assert.Single(await _database.BoardCases.GetAllAsync()).Id);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsFalseFromRepositoryAndThrowsFromService()
    {
        Assert.False(await _database.BoardCaseRepository.DeleteAsync(999));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _database.BoardCases.DeleteAsync(999));
    }

    [Fact]
    public async Task Delete_DoesNotReuseTheDeletedId()
    {
        var deleted = await _database.BoardCases.RegisterAsync(FullDetails());
        await _database.BoardCases.DeleteAsync(deleted.Id);

        var next = await _database.BoardCases.RegisterAsync(FullDetails());

        Assert.True(next.Id > deleted.Id);
    }
}
