using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Infrastructure.Persistence;

/// <summary>
/// Each method uses its own short-lived DbContext, so tasks returned here are never tracked by a context.
/// </summary>
public class EfTaskItemRepository : ITaskItemRepository
{
    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;

    public EfTaskItemRepository(IDbContextFactory<SameieHubDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(TaskItem task)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.TaskItems.Add(task);
        await context.SaveChangesAsync();
    }

    public async Task<TaskItem?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.TaskItems
            .AsNoTracking()
            .FirstOrDefaultAsync(task => task.Id == id);
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.TaskItems
            .AsNoTracking()
            .OrderBy(task => task.Id)
            .ToListAsync();
    }

    public async Task UpdateAsync(TaskItem task)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // The task was loaded by another context, so Update tells this one to save all its values.
        context.TaskItems.Update(task);
        await context.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var task = await context.TaskItems.FirstOrDefaultAsync(existing => existing.Id == id);
        if (task == null)
        {
            return false;
        }

        context.TaskItems.Remove(task);
        await context.SaveChangesAsync();
        return true;
    }
}
