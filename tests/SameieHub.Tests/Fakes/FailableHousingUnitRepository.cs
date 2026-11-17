using SameieHub.Domain;

namespace SameieHub.Tests.Fakes;

/// <summary>
/// Wraps the fake repository and can simulate that the database is unavailable.
/// </summary>
public class FailableHousingUnitRepository : IHousingUnitRepository
{
    private readonly FakeHousingUnitRepository _inner = new();

    public bool IsFailing { get; set; }

    public Task AddAsync(HousingUnit unit)
    {
        ThrowIfFailing();

        return _inner.AddAsync(unit);
    }

    public Task<HousingUnit?> GetByIdAsync(int id)
    {
        ThrowIfFailing();

        return _inner.GetByIdAsync(id);
    }

    public Task<IReadOnlyList<HousingUnit>> GetAllAsync()
    {
        ThrowIfFailing();

        return _inner.GetAllAsync();
    }

    public Task UpdateAsync(HousingUnit unit)
    {
        ThrowIfFailing();

        return _inner.UpdateAsync(unit);
    }

    private void ThrowIfFailing()
    {
        if (IsFailing)
        {
            throw new InvalidOperationException("The database is unavailable.");
        }
    }
}
