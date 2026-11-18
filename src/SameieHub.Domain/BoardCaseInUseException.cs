namespace SameieHub.Domain;

/// <summary>
/// A board case cannot be deleted because other records, such as tasks, refer to it.
/// </summary>
public class BoardCaseInUseException : Exception
{
    public BoardCaseInUseException(int boardCaseId, Exception? innerException = null)
        : base($"Board case {boardCaseId} is used by other records and cannot be deleted.", innerException)
    {
    }
}
