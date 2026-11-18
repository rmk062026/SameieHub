using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Infrastructure.Persistence;

/// <summary>
/// Each method uses its own short-lived DbContext, so cases returned here are never tracked by a context.
/// </summary>
public class EfBoardCaseRepository : IBoardCaseRepository
{
    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;

    public EfBoardCaseRepository(IDbContextFactory<SameieHubDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(BoardCase boardCase)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.BoardCases.Add(boardCase);
        await context.SaveChangesAsync();
    }

    public async Task<BoardCase?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.BoardCases
            .AsNoTracking()
            .FirstOrDefaultAsync(boardCase => boardCase.Id == id);
    }

    public async Task<IReadOnlyList<BoardCase>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.BoardCases
            .AsNoTracking()
            .OrderBy(boardCase => boardCase.Id)
            .ToListAsync();
    }

    public async Task UpdateAsync(BoardCase boardCase)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // The case was loaded by another context, so Update tells this one to save all its values.
        context.BoardCases.Update(boardCase);
        await context.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var boardCase = await context.BoardCases.FirstOrDefaultAsync(existing => existing.Id == id);
        if (boardCase == null)
        {
            return false;
        }

        context.BoardCases.Remove(boardCase);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (SqliteConstraintErrors.IsForeignKeyViolation(exception))
        {
            throw new BoardCaseInUseException(id, exception);
        }

        return true;
    }
}
