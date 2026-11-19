using SameieHub.Domain;

namespace SameieHub.Tests.Fakes;

public class FakeTaskItemRepository : ITaskItemRepository
{
    private readonly Dictionary<int, TaskItem> _tasks = [];
    private int _nextId = 1;

    public Task AddAsync(TaskItem task)
    {
        task.AssignId(_nextId);
        _tasks[task.Id] = task;
        _nextId++;

        return Task.CompletedTask;
    }

    public Task<TaskItem?> GetByIdAsync(int id)
    {
        _tasks.TryGetValue(id, out var task);

        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<TaskItem>> GetAllAsync()
    {
        IReadOnlyList<TaskItem> tasks = _tasks.Values.ToList();

        return Task.FromResult(tasks);
    }

    public Task UpdateAsync(TaskItem task)
    {
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(int id)
    {
        return Task.FromResult(_tasks.Remove(id));
    }
}
