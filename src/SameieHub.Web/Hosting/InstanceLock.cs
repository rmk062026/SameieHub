namespace SameieHub.Web.Hosting;

/// <summary>
/// Makes sure only one program instance runs against a database. The lock is an exclusive open of a file next to the
/// database, held for as long as the program runs. Windows releases it when the program ends, also after a crash, so a
/// file that is left behind never blocks the next start. The file is never deleted: deleting it while another instance
/// holds it could let two instances each get their own lock.
/// </summary>
internal sealed class InstanceLock : IDisposable
{
    private const int SharingViolation = 32;
    private const int LockViolation = 33;

    private FileStream? _stream;

    private InstanceLock(FileStream stream)
    {
        _stream = stream;
    }

    /// <summary>The lock file for a database, in the same folder: <c>sameiehub.db</c> gives <c>sameiehub.lock</c>.</summary>
    public static string PathFor(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);

        return Path.Combine(Path.GetDirectoryName(fullPath)!, Path.GetFileNameWithoutExtension(fullPath) + ".lock");
    }

    /// <summary>Returns the lock, or null when another instance holds it. Other problems (no permission, no disk) are thrown.</summary>
    public static InstanceLock? TryAcquire(string lockFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(lockFilePath))!);

        try
        {
            return new InstanceLock(new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException exception) when (IsHeldByAnotherProcess(exception))
        {
            return null;
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
    }

    private static bool IsHeldByAnotherProcess(IOException exception)
    {
        var windowsErrorCode = exception.HResult & 0xFFFF;

        return windowsErrorCode is SharingViolation or LockViolation;
    }
}
