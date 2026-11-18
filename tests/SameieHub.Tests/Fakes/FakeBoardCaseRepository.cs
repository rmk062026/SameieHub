using SameieHub.Domain;

namespace SameieHub.Tests.Fakes;

public class FakeBoardCaseRepository : IBoardCaseRepository
{
    private readonly Dictionary<int, BoardCase> _boardCases = [];
    private int _nextId = 1;

    /// <summary>Simulates that other records, such as tasks, refer to the board cases.</summary>
    public bool IsDeletionBlocked { get; set; }

    public Task AddAsync(BoardCase boardCase)
    {
        boardCase.AssignId(_nextId);
        _boardCases[boardCase.Id] = boardCase;
        _nextId++;

        return Task.CompletedTask;
    }

    public Task<BoardCase?> GetByIdAsync(int id)
    {
        _boardCases.TryGetValue(id, out var boardCase);

        return Task.FromResult(boardCase);
    }

    public Task<IReadOnlyList<BoardCase>> GetAllAsync()
    {
        IReadOnlyList<BoardCase> boardCases = _boardCases.Values.ToList();

        return Task.FromResult(boardCases);
    }

    public Task UpdateAsync(BoardCase boardCase)
    {
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(int id)
    {
        if (IsDeletionBlocked)
        {
            throw new BoardCaseInUseException(id);
        }

        return Task.FromResult(_boardCases.Remove(id));
    }
}
