using SameieHub.Domain;

namespace SameieHub.Tests.Fakes;

public class FakeTaskItemRepository : ITaskItemRepository
{
    private readonly Dictionary<int, TaskItem> _tasks = [];
    private int _nextId = 1;

    /// <summary>Simulates that the database is unavailable.</summary>
    public bool IsFailing { get; set; }

    /// <summary>When set, <see cref="AddAsync"/> waits until this completes, so tests can simulate a slow save.</summary>
    public TaskCompletionSource? AddBlocker { get; set; }

    public async Task AddAsync(TaskItem task)
    {
        ThrowIfFailing();

        if (AddBlocker != null)
        {
            await AddBlocker.Task;
        }

        task.AssignId(_nextId);
        _tasks[task.Id] = task;
        _nextId++;
    }

    public Task<TaskItem?> GetByIdAsync(int id)
    {
        ThrowIfFailing();
        _tasks.TryGetValue(id, out var task);

        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<TaskItem>> GetAllAsync()
    {
        ThrowIfFailing();
        IReadOnlyList<TaskItem> tasks = _tasks.Values.ToList();

        return Task.FromResult(tasks);
    }

    /// <summary>When set, <see cref="UpdateAsync"/> waits until this completes, so tests can simulate a slow save.</summary>
    public TaskCompletionSource? UpdateBlocker { get; set; }

    /// <summary>How many times <see cref="UpdateAsync"/> has been called.</summary>
    public int UpdateCount { get; private set; }

    public async Task UpdateAsync(TaskItem task)
    {
        ThrowIfFailing();
        UpdateCount++;

        if (UpdateBlocker != null)
        {
            await UpdateBlocker.Task;
        }
    }

    public Task<bool> DeleteAsync(int id)
    {
        ThrowIfFailing();
        return Task.FromResult(_tasks.Remove(id));
    }

    private void ThrowIfFailing()
    {
        if (IsFailing)
        {
            throw new IOException("The database is unavailable.");
        }
    }
}
