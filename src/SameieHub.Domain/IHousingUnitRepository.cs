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
}
