namespace SameieHub.Web.Hosting;

/// <summary>
/// Held by the program for as long as it runs, so the installer (which checks this name with <c>AppMutex</c>) can see that the
/// program files are in use. It is only a signal: the lock that protects the database is <see cref="InstanceLock"/>.
/// </summary>
internal sealed class RunningMarker : IDisposable
{
    public const string DefaultName = "SameieHub.Running";

    private readonly Mutex _mutex;

    private RunningMarker(Mutex mutex)
    {
        _mutex = mutex;
    }

    /// <summary>Returns null when the mutex cannot be made. The program then runs without telling the installer, which is not worth stopping for.</summary>
    public static RunningMarker? Create(string name = DefaultName)
    {
        try
        {
            return new RunningMarker(new Mutex(initiallyOwned: false, name));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            return null;
        }
    }

    public static bool IsHeld(string name = DefaultName)
    {
        return MutexExists(name);
    }

    public void Dispose()
    {
        _mutex.Dispose();
    }

    internal static bool MutexExists(string name)
    {
        try
        {
            if (Mutex.TryOpenExisting(name, out var existing))
            {
                existing.Dispose();

                return true;
            }

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // It exists, but belongs to someone this account may not open. For the question "is it there?", that is a yes.
            return true;
        }
    }
}

/// <summary>
/// The installer (and uninstaller) holds a mutex with this name while it works. The program does not start then, so a person who
/// starts it from a shortcut in the middle of an update does not get a half-replaced program.
/// </summary>
internal static class SetupInProgress
{
    public const string DefaultName = "SameieHubSetup";

    public static bool IsActive(string name = DefaultName)
    {
        // Inno Setup makes its mutex in the session and, for another user's installation, in the global namespace.
        return RunningMarker.MutexExists(name) || RunningMarker.MutexExists("Global\\" + name);
    }
}
