namespace SameieHub.Domain;

public class HousingUnitService
{
    private readonly IHousingUnitRepository _repository;

    public HousingUnitService(IHousingUnitRepository repository)
    {
        _repository = repository;
    }

    public async Task<HousingUnit> RegisterAsync(string address)
    {
        var unit = new HousingUnit(address);

        await _repository.AddAsync(unit);

        return unit;
    }

    public Task<HousingUnit?> GetByIdAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }

    public Task<IReadOnlyList<HousingUnit>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }

    /// <exception cref="KeyNotFoundException">No housing unit has the given Id.</exception>
    public async Task ChangeAddressAsync(int id, string newAddress)
    {
        var unit = await _repository.GetByIdAsync(id);
        if (unit == null)
        {
            throw new KeyNotFoundException($"Housing unit {id} was not found.");
        }

        unit.ChangeAddress(newAddress);

        await _repository.UpdateAsync(unit);
    }
}
