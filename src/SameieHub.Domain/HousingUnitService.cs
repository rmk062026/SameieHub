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

    public async Task<HousingUnit> RegisterAsync(HousingUnitDetails details)
    {
        var unit = new HousingUnit(details);

        await _repository.AddAsync(unit);

        return unit;
    }

    public Task<HousingUnit?> GetByIdAsync(int id)
    {
        return _repository.GetByIdAsync(id);
    }

    /// <summary>
    /// Returns active and inactive units. Use <see cref="HousingUnitFilter"/> to narrow the list.
    /// </summary>
    public Task<IReadOnlyList<HousingUnit>> GetAllAsync()
    {
        return _repository.GetAllAsync();
    }

    /// <exception cref="KeyNotFoundException">No housing unit has the given Id.</exception>
    public async Task ChangeAddressAsync(int id, string newAddress)
    {
        var unit = await GetExistingUnitAsync(id);

        unit.ChangeAddress(newAddress);

        await _repository.UpdateAsync(unit);
    }

    /// <exception cref="KeyNotFoundException">No housing unit has the given Id.</exception>
    /// <exception cref="ArgumentException">A value is invalid. The stored unit is not changed.</exception>
    public async Task UpdateDetailsAsync(int id, HousingUnitDetails details)
    {
        var unit = await GetExistingUnitAsync(id);

        unit.UpdateDetails(details);

        await _repository.UpdateAsync(unit);
    }

    /// <exception cref="KeyNotFoundException">No housing unit has the given Id.</exception>
    public async Task DeactivateAsync(int id)
    {
        var unit = await GetExistingUnitAsync(id);

        unit.Deactivate();

        await _repository.UpdateAsync(unit);
    }

    /// <exception cref="KeyNotFoundException">No housing unit has the given Id.</exception>
    public async Task ActivateAsync(int id)
    {
        var unit = await GetExistingUnitAsync(id);

        unit.Activate();

        await _repository.UpdateAsync(unit);
    }

    /// <summary>
    /// Permanently deletes the housing unit and everything registered on it, including owner information.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No housing unit has the given Id.</exception>
    /// <exception cref="HousingUnitInUseException">Other records refer to the housing unit. Nothing is deleted.</exception>
    public async Task DeleteAsync(int id)
    {
        var wasDeleted = await _repository.DeleteAsync(id);
        if (!wasDeleted)
        {
            throw new KeyNotFoundException($"Housing unit {id} was not found.");
        }
    }

    private async Task<HousingUnit> GetExistingUnitAsync(int id)
    {
        var unit = await _repository.GetByIdAsync(id);
        if (unit == null)
        {
            throw new KeyNotFoundException($"Housing unit {id} was not found.");
        }

        return unit;
    }
}
