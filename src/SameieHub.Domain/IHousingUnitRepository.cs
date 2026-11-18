namespace SameieHub.Domain;

public interface IHousingUnitRepository
{
    /// <summary>
    /// Stores a new housing unit. When the task completes, <see cref="HousingUnit.Id"/> has been assigned.
    /// </summary>
    Task AddAsync(HousingUnit unit);

    Task<HousingUnit?> GetByIdAsync(int id);

    Task<IReadOnlyList<HousingUnit>> GetAllAsync();

    Task UpdateAsync(HousingUnit unit);

    /// <summary>
    /// Permanently deletes the housing unit. Returns false when no unit has the given Id.
    /// </summary>
    /// <exception cref="HousingUnitInUseException">Other records refer to the housing unit. Nothing is deleted.</exception>
    Task<bool> DeleteAsync(int id);
}
