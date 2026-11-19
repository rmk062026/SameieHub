namespace SameieHub.Domain;

public interface ITaskItemRepository
{
    /// <summary>
    /// Stores a new task. When the task completes, <see cref="TaskItem.Id"/> has been assigned.
    /// </summary>
    Task AddAsync(TaskItem task);

    Task<TaskItem?> GetByIdAsync(int id);

    Task<IReadOnlyList<TaskItem>> GetAllAsync();

    Task UpdateAsync(TaskItem task);

    /// <summary>
    /// Permanently deletes the task, and nothing it refers to. Returns false when no task has the given Id.
    /// </summary>
    Task<bool> DeleteAsync(int id);
}
