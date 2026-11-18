using SameieHub.Domain;
using SameieHub.Tests.Fakes;

namespace SameieHub.Tests.Domain;

public class BoardCaseServiceTests
{
    private static readonly DateTime StartTime = new(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeBoardCaseRepository _boardCaseRepository = new();
    private readonly ManualTimeProvider _time = new(StartTime);
    private readonly HousingUnitService _housingUnitService;
    private readonly BoardCaseService _service;

    public BoardCaseServiceTests()
    {
        var housingUnitRepository = new FakeHousingUnitRepository();
        _housingUnitService = new HousingUnitService(housingUnitRepository);
        _service = new BoardCaseService(_boardCaseRepository, housingUnitRepository, _time);
    }

    private static BoardCaseDetails Details(string title = "Maling av fellesarealer") => new() { Title = title };

    private async Task<BoardCase> RegisterCaseAsync(BoardCaseDetails? details = null)
    {
        return await _service.RegisterAsync(details ?? Details());
    }

    private void MoveClockForward()
    {
        _time.Advance(TimeSpan.FromHours(2));
    }

    // Registration

    [Fact]
    public async Task RegisterAsync_OnlyTitle_ReturnsStoredCaseWithIdDefaultsAndTimestamps()
    {
        var boardCase = await _service.RegisterAsync(Details());

        Assert.True(boardCase.Id > 0);
        Assert.Equal(BoardCaseStatus.New, boardCase.Status);
        Assert.Equal(BoardCasePriority.Normal, boardCase.Priority);
        Assert.Null(boardCase.HousingUnitId);
        Assert.Equal(StartTime, boardCase.CreatedAt);
        Assert.Equal(StartTime, boardCase.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, boardCase.CreatedAt.Kind);
        Assert.NotNull(await _service.GetByIdAsync(boardCase.Id));
    }

    [Fact]
    public async Task RegisterAsync_UsesTheTimeFromTheTimeProvider()
    {
        MoveClockForward();

        var boardCase = await _service.RegisterAsync(Details());

        Assert.Equal(StartTime.AddHours(2), boardCase.CreatedAt);
    }

    [Fact]
    public async Task RegisterAsync_TwoCases_GetDifferentIds()
    {
        var first = await _service.RegisterAsync(Details("Sak 1"));
        var second = await _service.RegisterAsync(Details("Sak 2"));

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
    public async Task GetByIdAsync_ExistingCase_ReturnsIt()
    {
        var registered = await RegisterCaseAsync();

        var found = await _service.GetByIdAsync(registered.Id);

        Assert.Equal("Maling av fellesarealer", found!.Title);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        Assert.Null(await _service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetAllAsync_NoCases_ReturnsEmptyList()
    {
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_TwoCases_ReturnsBoth()
    {
        await RegisterCaseAsync(Details("Sak 1"));
        await RegisterCaseAsync(Details("Sak 2"));

        var cases = await _service.GetAllAsync();

        Assert.Equal(["Sak 1", "Sak 2"], cases.Select(boardCase => boardCase.Title));
    }

    // Update

    [Fact]
    public async Task UpdateDetailsAsync_NewValues_AreStoredAndUpdatedAtChanges()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await _service.UpdateDetailsAsync(boardCase.Id, new BoardCaseDetails
        {
            Title = "Ny tittel",
            Description = "Fiktiv beskrivelse.",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(2026, 12, 3),
            Notes = "Fiktive merknader."
        });

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal("Ny tittel", stored!.Title);
        Assert.Equal("Fiktiv beskrivelse.", stored.Description);
        Assert.Equal(BoardCasePriority.High, stored.Priority);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.MeetingDate);
        Assert.Equal(StartTime, stored.CreatedAt);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnchangedValues_DoNotChangeUpdatedAt()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await _service.UpdateDetailsAsync(boardCase.Id, Details());

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal(StartTime, stored!.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_InvalidValue_ThrowsAndKeepsEverythingIncludingUpdatedAt()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.UpdateDetailsAsync(boardCase.Id, new BoardCaseDetails
        {
            Title = "Ny tittel",
            Priority = BoardCasePriority.High,
            MeetingDate = new DateOnly(1999, 1, 1)
        }));

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal("Maling av fellesarealer", stored!.Title);
        Assert.Equal(BoardCasePriority.Normal, stored.Priority);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnknownId_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateDetailsAsync(999, Details()));
    }

    // Housing unit link

    [Fact]
    public async Task RegisterAsync_WithActiveHousingUnit_StoresTheLink()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");

        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = unit.Id });

        Assert.Equal(unit.Id, boardCase.HousingUnitId);
    }

    [Fact]
    public async Task RegisterAsync_UnknownHousingUnit_ThrowsAndStoresNothing()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegisterAsync(Details() with { HousingUnitId = 999 }));

        Assert.Equal("HousingUnitId", exception.ParamName);
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task RegisterAsync_InactiveHousingUnit_ThrowsAndStoresNothing()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        await _housingUnitService.DeactivateAsync(unit.Id);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegisterAsync(Details() with { HousingUnitId = unit.Id }));

        Assert.Equal("HousingUnitId", exception.ParamName);
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task UpdateDetailsAsync_LinkToOtherActiveUnit_ChangesTheLink()
    {
        var first = await _housingUnitService.RegisterAsync("Storgata 1A");
        var second = await _housingUnitService.RegisterAsync("Storgata 1B");
        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = first.Id });

        await _service.UpdateDetailsAsync(boardCase.Id, Details() with { HousingUnitId = second.Id });

        Assert.Equal(second.Id, (await _service.GetByIdAsync(boardCase.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task UpdateDetailsAsync_LinkToInactiveUnit_ThrowsAndChangesNothing()
    {
        var active = await _housingUnitService.RegisterAsync("Storgata 1A");
        var inactive = await _housingUnitService.RegisterAsync("Storgata 1B");
        await _housingUnitService.DeactivateAsync(inactive.Id);
        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = active.Id });
        MoveClockForward();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(boardCase.Id, Details("Ny tittel") with { HousingUnitId = inactive.Id }));

        Assert.Equal("HousingUnitId", exception.ParamName);
        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal(active.Id, stored!.HousingUnitId);
        Assert.Equal("Maling av fellesarealer", stored.Title);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task UpdateDetailsAsync_LinkToUnknownUnit_ThrowsAndChangesNothing()
    {
        var boardCase = await RegisterCaseAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateDetailsAsync(boardCase.Id, Details() with { HousingUnitId = 999 }));

        Assert.Null((await _service.GetByIdAsync(boardCase.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnitDeactivatedAfterLinking_KeepsLinkAndOtherFieldsCanBeEdited()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = unit.Id });
        await _housingUnitService.DeactivateAsync(unit.Id);

        await _service.UpdateDetailsAsync(boardCase.Id, Details("Ny tittel") with { HousingUnitId = unit.Id });

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal("Ny tittel", stored!.Title);
        Assert.Equal(unit.Id, stored.HousingUnitId);
    }

    [Fact]
    public async Task UpdateDetailsAsync_UnitDeactivatedAfterLinking_LinkCanBeRemoved()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = unit.Id });
        await _housingUnitService.DeactivateAsync(unit.Id);

        await _service.UpdateDetailsAsync(boardCase.Id, Details());

        Assert.Null((await _service.GetByIdAsync(boardCase.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_ActiveUnit_LinksAndKeepsOtherValues()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterCaseAsync(Details() with { Priority = BoardCasePriority.High, Notes = "Merknad" });
        MoveClockForward();

        await _service.ChangeHousingUnitAsync(boardCase.Id, unit.Id);

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal(unit.Id, stored!.HousingUnitId);
        Assert.Equal(BoardCasePriority.High, stored.Priority);
        Assert.Equal("Merknad", stored.Notes);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_Null_RemovesLinkSoCaseConcernsTheWholeCooperative()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = unit.Id });

        await _service.ChangeHousingUnitAsync(boardCase.Id, null);

        Assert.Null((await _service.GetByIdAsync(boardCase.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_InactiveOrUnknownUnit_ThrowsAndKeepsLink()
    {
        var active = await _housingUnitService.RegisterAsync("Storgata 1A");
        var inactive = await _housingUnitService.RegisterAsync("Storgata 1B");
        await _housingUnitService.DeactivateAsync(inactive.Id);
        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = active.Id });

        await Assert.ThrowsAsync<ArgumentException>(() => _service.ChangeHousingUnitAsync(boardCase.Id, inactive.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ChangeHousingUnitAsync(boardCase.Id, 999));

        Assert.Equal(active.Id, (await _service.GetByIdAsync(boardCase.Id))!.HousingUnitId);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_SameDeactivatedUnit_IsAcceptedAndChangesNothing()
    {
        var unit = await _housingUnitService.RegisterAsync("Storgata 1A");
        var boardCase = await RegisterCaseAsync(Details() with { HousingUnitId = unit.Id });
        await _housingUnitService.DeactivateAsync(unit.Id);
        MoveClockForward();

        await _service.ChangeHousingUnitAsync(boardCase.Id, unit.Id);

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal(unit.Id, stored!.HousingUnitId);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeHousingUnitAsync_UnknownCase_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeHousingUnitAsync(999, null));
    }

    // Status

    [Fact]
    public async Task ChangeStatusAsync_ToInProgress_IsStoredAndUpdatedAtChanges()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.InProgress);

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal(BoardCaseStatus.InProgress, stored!.Status);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_SameStatus_DoesNotChangeUpdatedAt()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.New);

        Assert.Equal(StartTime, (await _service.GetByIdAsync(boardCase.Id))!.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_DecidedWithoutDecision_ThrowsAndChangesNothing()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided));

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal(BoardCaseStatus.New, stored!.Status);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_DecidedWithDecision_IsDecidedButNotClosed()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);

        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);

        Assert.Equal(BoardCaseStatus.Decided, (await _service.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ClosedCase_CanBeReopened()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Closed);

        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.InProgress);

        Assert.Equal(BoardCaseStatus.InProgress, (await _service.GetByIdAsync(boardCase.Id))!.Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownCase_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ChangeStatusAsync(999, BoardCaseStatus.InProgress));
    }

    // Decision

    [Fact]
    public async Task RegisterDecisionAsync_ValidText_IsStoredWithoutChangingStatus()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await _service.RegisterDecisionAsync(boardCase.Id, "  Fiktivt vedtak.  ", null);

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(BoardCaseStatus.New, stored.Status);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task RegisterDecisionAsync_ExistingDecision_IsCorrected()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Feil vedtak.", null);
        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);

        await _service.RegisterDecisionAsync(boardCase.Id, "Riktig vedtak.", null);

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal("Riktig vedtak.", stored!.Decision);
        Assert.Equal(BoardCaseStatus.Decided, stored.Status);
    }

    [Fact]
    public async Task RegisterDecisionAsync_Blank_ThrowsAndKeepsExistingDecision()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.RegisterDecisionAsync(boardCase.Id, "   ", null));

        Assert.Equal("Fiktivt vedtak.", (await _service.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task ClearDecisionAsync_CaseNotDecided_RemovesDecision()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Registrert ved en feil.", null);

        await _service.ClearDecisionAsync(boardCase.Id);

        Assert.Null((await _service.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task ClearDecisionAsync_CaseIsDecided_ThrowsAndKeepsDecision()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ClearDecisionAsync(boardCase.Id));

        Assert.Equal("Fiktivt vedtak.", (await _service.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task ClearDecisionAsync_AfterStatusIsChangedBackFromDecided_Works()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);
        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);
        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.InProgress);

        await _service.ClearDecisionAsync(boardCase.Id);

        Assert.Null((await _service.GetByIdAsync(boardCase.Id))!.Decision);
    }

    [Fact]
    public async Task DecisionMethods_UnknownCase_ThrowKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RegisterDecisionAsync(999, "Vedtak", null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ClearDecisionAsync(999));
    }

    // Deletion

    [Fact]
    public async Task DeleteAsync_ExistingCase_RemovesOnlyThatCase()
    {
        var first = await RegisterCaseAsync(Details("Sak 1"));
        var second = await RegisterCaseAsync(Details("Sak 2"));

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
    public async Task DeleteAsync_CaseInUse_ThrowsAndKeepsTheCase()
    {
        var boardCase = await RegisterCaseAsync();
        _boardCaseRepository.IsDeletionBlocked = true;

        await Assert.ThrowsAsync<BoardCaseInUseException>(() => _service.DeleteAsync(boardCase.Id));

        _boardCaseRepository.IsDeletionBlocked = false;
        Assert.NotNull(await _service.GetByIdAsync(boardCase.Id));
    }

    // Decision date

    [Fact]
    public async Task RegisterDecisionAsync_WithDate_StoresDateAndSetsUpdatedAt()
    {
        var boardCase = await RegisterCaseAsync();
        MoveClockForward();

        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 3));

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal(new DateOnly(2026, 12, 3), stored!.DecisionDate);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task RegisterDecisionAsync_WithoutDate_StoresNoDate()
    {
        var boardCase = await RegisterCaseAsync();

        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);

        Assert.Null((await _service.GetByIdAsync(boardCase.Id))!.DecisionDate);
    }

    [Fact]
    public async Task RegisterDecisionAsync_CorrectingTheDate_ChangesOnlyTheDate()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 3));
        MoveClockForward();

        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 10));

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(new DateOnly(2026, 12, 10), stored.DecisionDate);
        Assert.Equal(StartTime.AddHours(2), stored.UpdatedAt);
    }

    [Fact]
    public async Task RegisterDecisionAsync_SameTextAndDate_DoesNotChangeUpdatedAt()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 3));
        MoveClockForward();

        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 3));

        Assert.Equal(StartTime, (await _service.GetByIdAsync(boardCase.Id))!.UpdatedAt);
    }

    [Fact]
    public async Task RegisterDecisionAsync_InvalidDate_ThrowsAndKeepsEverything()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", new DateOnly(2026, 12, 3));
        MoveClockForward();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.RegisterDecisionAsync(boardCase.Id, "Nytt vedtak.", new DateOnly(1999, 1, 1)));

        Assert.Equal("DecisionDate", exception.ParamName);
        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Equal("Fiktivt vedtak.", stored!.Decision);
        Assert.Equal(new DateOnly(2026, 12, 3), stored.DecisionDate);
        Assert.Equal(StartTime, stored.UpdatedAt);
    }

    [Fact]
    public async Task ClearDecisionAsync_RemovesTheDecisionDate()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Registrert ved en feil.", new DateOnly(2026, 12, 3));

        await _service.ClearDecisionAsync(boardCase.Id);

        var stored = await _service.GetByIdAsync(boardCase.Id);
        Assert.Null(stored!.Decision);
        Assert.Null(stored.DecisionDate);
    }

    [Fact]
    public async Task ChangeStatusAsync_DecidedWithDecisionButNoDate_IsAllowed()
    {
        var boardCase = await RegisterCaseAsync();
        await _service.RegisterDecisionAsync(boardCase.Id, "Fiktivt vedtak.", null);

        await _service.ChangeStatusAsync(boardCase.Id, BoardCaseStatus.Decided);

        Assert.Equal(BoardCaseStatus.Decided, (await _service.GetByIdAsync(boardCase.Id))!.Status);
    }
}
