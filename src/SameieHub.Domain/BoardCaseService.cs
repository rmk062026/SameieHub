namespace SameieHub.Domain;

public class BoardCaseService
{
    private readonly IBoardCaseRepository _boardCaseRepository;
    private readonly IHousingUnitRepository _housingUnitRepository;
    private readonly TimeProvider _timeProvider;

    public BoardCaseService(
        IBoardCaseRepository boardCaseRepository,
        IHousingUnitRepository housingUnitRepository,
        TimeProvider timeProvider)
    {
        _boardCaseRepository = boardCaseRepository;
        _housingUnitRepository = housingUnitRepository;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Registers a new case. A housing unit link must refer to an existing, active housing unit.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A value is invalid, or the housing unit does not exist or is inactive (ParamName "HousingUnitId").
    /// </exception>
    public async Task<BoardCase> RegisterAsync(BoardCaseDetails details)
    {
        await EnsureHousingUnitCanBeLinkedAsync(details.HousingUnitId);

        var boardCase = new BoardCase(details, GetUtcNow());

        await _boardCaseRepository.AddAsync(boardCase);

        return boardCase;
    }

    public Task<BoardCase?> GetByIdAsync(int id)
    {
        return _boardCaseRepository.GetByIdAsync(id);
    }

    public Task<IReadOnlyList<BoardCase>> GetAllAsync()
    {
        return _boardCaseRepository.GetAllAsync();
    }

    /// <summary>
    /// A link to a housing unit that is unchanged is kept even if the unit has been deactivated since.
    /// A new link must refer to an existing, active housing unit.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No case has the given Id.</exception>
    /// <exception cref="ArgumentException">
    /// A value is invalid, or the housing unit does not exist or is inactive (ParamName "HousingUnitId").
    /// </exception>
    public async Task UpdateDetailsAsync(int id, BoardCaseDetails details)
    {
        var boardCase = await GetExistingCaseAsync(id);

        await SaveDetailsAsync(boardCase, details);
    }

    /// <summary>
    /// Links the case to a housing unit, or removes the link when <paramref name="housingUnitId"/> is null
    /// (the case then concerns the whole housing cooperative). Other values are not changed.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No case has the given Id.</exception>
    /// <exception cref="ArgumentException">The housing unit does not exist or is inactive (ParamName "HousingUnitId").</exception>
    public async Task ChangeHousingUnitAsync(int id, int? housingUnitId)
    {
        var boardCase = await GetExistingCaseAsync(id);

        await SaveDetailsAsync(boardCase, boardCase.ToDetails() with { HousingUnitId = housingUnitId });
    }

    /// <exception cref="KeyNotFoundException">No case has the given Id.</exception>
    /// <exception cref="InvalidOperationException">The case is changed to decided without a decision.</exception>
    public async Task ChangeStatusAsync(int id, BoardCaseStatus newStatus)
    {
        var boardCase = await GetExistingCaseAsync(id);

        boardCase.ChangeStatus(newStatus, GetUtcNow());

        await _boardCaseRepository.UpdateAsync(boardCase);
    }

    /// <exception cref="KeyNotFoundException">No case has the given Id.</exception>
    public async Task RegisterDecisionAsync(int id, string decision)
    {
        var boardCase = await GetExistingCaseAsync(id);

        boardCase.RegisterDecision(decision, GetUtcNow());

        await _boardCaseRepository.UpdateAsync(boardCase);
    }

    /// <exception cref="KeyNotFoundException">No case has the given Id.</exception>
    /// <exception cref="InvalidOperationException">The case is decided. Change the status first.</exception>
    public async Task ClearDecisionAsync(int id)
    {
        var boardCase = await GetExistingCaseAsync(id);

        boardCase.ClearDecision(GetUtcNow());

        await _boardCaseRepository.UpdateAsync(boardCase);
    }

    /// <summary>
    /// Permanently deletes the case.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No case has the given Id.</exception>
    /// <exception cref="BoardCaseInUseException">Other records, such as tasks, refer to the case. Nothing is deleted.</exception>
    public async Task DeleteAsync(int id)
    {
        var wasDeleted = await _boardCaseRepository.DeleteAsync(id);
        if (!wasDeleted)
        {
            throw new KeyNotFoundException($"Board case {id} was not found.");
        }
    }

    private async Task SaveDetailsAsync(BoardCase boardCase, BoardCaseDetails details)
    {
        var isLinkChanged = details.HousingUnitId != boardCase.HousingUnitId;
        if (isLinkChanged)
        {
            await EnsureHousingUnitCanBeLinkedAsync(details.HousingUnitId);
        }

        boardCase.UpdateDetails(details, GetUtcNow());

        await _boardCaseRepository.UpdateAsync(boardCase);
    }

    private async Task EnsureHousingUnitCanBeLinkedAsync(int? housingUnitId)
    {
        if (housingUnitId == null)
        {
            return;
        }

        var housingUnit = await _housingUnitRepository.GetByIdAsync(housingUnitId.Value);
        if (housingUnit == null)
        {
            throw new ArgumentException("The housing unit does not exist.", nameof(BoardCaseDetails.HousingUnitId));
        }

        if (!housingUnit.IsActive)
        {
            throw new ArgumentException("The housing unit is not active.", nameof(BoardCaseDetails.HousingUnitId));
        }
    }

    private async Task<BoardCase> GetExistingCaseAsync(int id)
    {
        var boardCase = await _boardCaseRepository.GetByIdAsync(id);
        if (boardCase == null)
        {
            throw new KeyNotFoundException($"Board case {id} was not found.");
        }

        return boardCase;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
