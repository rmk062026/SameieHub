namespace SameieHub.Domain;

public class TaskItemService
{
    private readonly ITaskItemRepository _taskRepository;
    private readonly IHousingUnitRepository _housingUnitRepository;
    private readonly IBoardCaseRepository _boardCaseRepository;
    private readonly TimeProvider _timeProvider;

    public TaskItemService(
        ITaskItemRepository taskRepository,
        IHousingUnitRepository housingUnitRepository,
        IBoardCaseRepository boardCaseRepository,
        TimeProvider timeProvider)
    {
        _taskRepository = taskRepository;
        _housingUnitRepository = housingUnitRepository;
        _boardCaseRepository = boardCaseRepository;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Registers a new task. A housing unit link must refer to an existing, active housing unit,
    /// and a board case link must refer to an existing board case (of any status).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A value is invalid, or a linked object is not allowed (ParamName "HousingUnitId" or "BoardCaseId").
    /// </exception>
    public async Task<TaskItem> RegisterAsync(TaskItemDetails details)
    {
        await EnsureHousingUnitCanBeLinkedAsync(details.HousingUnitId);
        await EnsureBoardCaseExistsAsync(details.BoardCaseId);

        var task = new TaskItem(details, GetUtcNow());

        await _taskRepository.AddAsync(task);

        return task;
    }

    public Task<TaskItem?> GetByIdAsync(int id)
    {
        return _taskRepository.GetByIdAsync(id);
    }

    public Task<IReadOnlyList<TaskItem>> GetAllAsync()
    {
        return _taskRepository.GetAllAsync();
    }

    /// <summary>
    /// Links that are unchanged are kept without being checked again, so a task stays linked to a housing unit
    /// that is deactivated later and can still be edited. A new link is checked before anything is changed.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No task has the given Id.</exception>
    /// <exception cref="ArgumentException">
    /// A value is invalid, or a new link is not allowed. The stored task is not changed.
    /// </exception>
    public async Task UpdateDetailsAsync(int id, TaskItemDetails details)
    {
        var task = await GetExistingTaskAsync(id);

        await SaveDetailsAsync(task, details);
    }

    /// <summary>
    /// Links the task to a housing unit, or removes the link when <paramref name="housingUnitId"/> is null.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No task has the given Id.</exception>
    /// <exception cref="ArgumentException">The housing unit does not exist or is inactive (ParamName "HousingUnitId").</exception>
    public async Task ChangeHousingUnitAsync(int id, int? housingUnitId)
    {
        var task = await GetExistingTaskAsync(id);

        await SaveDetailsAsync(task, task.ToDetails() with { HousingUnitId = housingUnitId });
    }

    /// <summary>
    /// Links the task to a board case, or removes the link when <paramref name="boardCaseId"/> is null.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No task has the given Id.</exception>
    /// <exception cref="ArgumentException">The board case does not exist (ParamName "BoardCaseId").</exception>
    public async Task ChangeBoardCaseAsync(int id, int? boardCaseId)
    {
        var task = await GetExistingTaskAsync(id);

        await SaveDetailsAsync(task, task.ToDetails() with { BoardCaseId = boardCaseId });
    }

    /// <exception cref="KeyNotFoundException">No task has the given Id.</exception>
    public async Task ChangeStatusAsync(int id, TaskItemStatus newStatus)
    {
        var task = await GetExistingTaskAsync(id);

        task.ChangeStatus(newStatus, GetUtcNow());

        await _taskRepository.UpdateAsync(task);
    }

    /// <summary>
    /// Permanently deletes the task. The housing unit and board case it refers to are not touched.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No task has the given Id.</exception>
    public async Task DeleteAsync(int id)
    {
        var wasDeleted = await _taskRepository.DeleteAsync(id);
        if (!wasDeleted)
        {
            throw new KeyNotFoundException($"Task {id} was not found.");
        }
    }

    /// <summary>
    /// Today's date in Norwegian time. Use it with <see cref="TaskItem.IsOverdue"/>.
    /// </summary>
    public DateOnly GetToday()
    {
        return NorwegianTime.DateOf(_timeProvider.GetUtcNow());
    }

    /// <summary>
    /// The tasks that are overdue today, with the earliest due date first.
    /// </summary>
    public async Task<IReadOnlyList<TaskItem>> GetOverdueAsync()
    {
        var today = GetToday();
        var tasks = await _taskRepository.GetAllAsync();

        return tasks
            .Where(task => task.IsOverdue(today))
            .OrderBy(task => task.DueDate)
            .ThenBy(task => task.Id)
            .ToList();
    }

    private async Task SaveDetailsAsync(TaskItem task, TaskItemDetails details)
    {
        if (details.HousingUnitId != task.HousingUnitId)
        {
            await EnsureHousingUnitCanBeLinkedAsync(details.HousingUnitId);
        }

        if (details.BoardCaseId != task.BoardCaseId)
        {
            await EnsureBoardCaseExistsAsync(details.BoardCaseId);
        }

        task.UpdateDetails(details, GetUtcNow());

        await _taskRepository.UpdateAsync(task);
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
            throw new ArgumentException("The housing unit does not exist.", nameof(TaskItemDetails.HousingUnitId));
        }

        if (!housingUnit.IsActive)
        {
            throw new ArgumentException("The housing unit is not active.", nameof(TaskItemDetails.HousingUnitId));
        }
    }

    private async Task EnsureBoardCaseExistsAsync(int? boardCaseId)
    {
        if (boardCaseId == null)
        {
            return;
        }

        var boardCase = await _boardCaseRepository.GetByIdAsync(boardCaseId.Value);
        if (boardCase == null)
        {
            throw new ArgumentException("The board case does not exist.", nameof(TaskItemDetails.BoardCaseId));
        }
    }

    private async Task<TaskItem> GetExistingTaskAsync(int id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
        {
            throw new KeyNotFoundException($"Task {id} was not found.");
        }

        return task;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
