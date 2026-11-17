using Microsoft.EntityFrameworkCore;
using SameieHub.Domain;

namespace SameieHub.Infrastructure.Persistence;

/// <summary>
/// Each method uses its own short-lived DbContext, so units returned here are never tracked by a context.
/// </summary>
public class EfHousingUnitRepository : IHousingUnitRepository
{
    private readonly IDbContextFactory<SameieHubDbContext> _contextFactory;

    public EfHousingUnitRepository(IDbContextFactory<SameieHubDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(HousingUnit unit)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        context.HousingUnits.Add(unit);
        await context.SaveChangesAsync();
    }

    public async Task<HousingUnit?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.HousingUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(unit => unit.Id == id);
    }

    public async Task<IReadOnlyList<HousingUnit>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.HousingUnits
            .AsNoTracking()
            .OrderBy(unit => unit.Id)
            .ToListAsync();
    }

    public async Task UpdateAsync(HousingUnit unit)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // The unit was loaded by another context, so Update tells this one to save all its values.
        context.HousingUnits.Update(unit);
        await context.SaveChangesAsync();
    }
}
