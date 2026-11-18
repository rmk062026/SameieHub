namespace SameieHub.Domain;

public interface IBoardCaseRepository
{
    /// <summary>
    /// Stores a new board case. When the task completes, <see cref="BoardCase.Id"/> has been assigned.
    /// </summary>
    Task AddAsync(BoardCase boardCase);

    Task<BoardCase?> GetByIdAsync(int id);

    Task<IReadOnlyList<BoardCase>> GetAllAsync();

    Task UpdateAsync(BoardCase boardCase);

    /// <summary>
    /// Permanently deletes the board case. Returns false when no case has the given Id.
    /// </summary>
    /// <exception cref="BoardCaseInUseException">Other records, such as tasks, refer to the case. Nothing is deleted.</exception>
    Task<bool> DeleteAsync(int id);
}
