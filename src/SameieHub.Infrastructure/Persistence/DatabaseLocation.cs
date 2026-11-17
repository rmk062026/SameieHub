namespace SameieHub.Infrastructure.Persistence;

public static class DatabaseLocation
{
    private const string FolderName = "SameieHub";
    private const string FileName = "sameiehub.db";

    /// <summary>
    /// The database lives under the user's local AppData so that it survives rebuilds,
    /// updates and republishing. Backup and restore (M5) rely on this fixed location.
    /// </summary>
    public static string GetDefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(localAppData, FolderName, FileName);
    }
}
