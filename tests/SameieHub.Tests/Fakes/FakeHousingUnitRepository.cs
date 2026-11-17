using SameieHub.Domain;

namespace SameieHub.Tests.Fakes;

public class FakeHousingUnitRepository : IHousingUnitRepository
{
    private readonly Dictionary<int, HousingUnit> _units = [];
    private int _nextId = 1;

    public Task AddAsync(HousingUnit unit)
    {
        unit.AssignId(_nextId);
        _units[unit.Id] = unit;
        _nextId++;

        return Task.CompletedTask;
    }

    public Task<HousingUnit?> GetByIdAsync(int id)
    {
        _units.TryGetValue(id, out var unit);

        return Task.FromResult(unit);
    }

    public Task<IReadOnlyList<HousingUnit>> GetAllAsync()
    {
        IReadOnlyList<HousingUnit> units = _units.Values.ToList();

        return Task.FromResult(units);
    }

    public Task UpdateAsync(HousingUnit unit)
    {
        return Task.CompletedTask;
    }
}
