using Microsoft.Extensions.Logging;
using SameieHub.Domain;
using SameieHub.Infrastructure.Restore;

namespace SameieHub.Infrastructure.Backup;

/// <summary>
/// What the backup page needs from the server. A backup is made in a private temporary folder and handed to the browser as a stream, so
/// there is never a web address to a database file. An upload is stored under a fixed name in its own random folder (the name the
/// browser sends is only shown, never used as a path) and checked before it is used. Every temporary folder is deleted when it is
/// disposed, and old ones are swept away.
/// </summary>
public sealed class BackupTransfer
{
    public const long DefaultMaxUploadBytes = 200L * 1024 * 1024;

    private const string FolderName = "transfer";
    private const string UploadedFileName = "upload.db";
    private const int MaxDisplayNameLength = 80;

    private readonly string _root;
    private readonly IDatabaseBackup _backup;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BackupTransfer> _logger;

    public BackupTransfer(string databasePath, IDatabaseBackup backup, TimeProvider timeProvider, ILogger<BackupTransfer> logger, long maxUploadBytes = DefaultMaxUploadBytes)
    {
        _root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, FolderName);
        _backup = backup;
        _timeProvider = timeProvider;
        _logger = logger;
        MaxUploadBytes = maxUploadBytes;
    }

    /// <summary>The largest backup file that is accepted.</summary>
    public long MaxUploadBytes { get; }

    /// <summary>Makes a backup in a private temporary folder. Dispose the result when the browser has received it.</summary>
    /// <exception cref="DatabaseBackupException">The backup was not made (nothing is left behind).</exception>
    public async Task<BackupDownload> CreateDownloadAsync(CancellationToken cancellationToken = default)
    {
        var folder = NewFolder();
        var fileName = $"SameieHub-sikkerhetskopi-{NorwegianTime.FromUtc(_timeProvider.GetUtcNow().UtcDateTime):yyyy-MM-dd-HHmm}.db";
        try
        {
            var result = await _backup.CreateBackupAsync(Path.Combine(folder, fileName), cancellationToken);

            return new BackupDownload(fileName, result.FilePath, result.SizeBytes, folder, _logger);
        }
        catch
        {
            DeleteFolder(folder);

            throw;
        }
    }

    /// <summary>
    /// Copies an upload to a temporary file and inspects it. The caller gets the checked file and must dispose it, also when it is not used.
    /// </summary>
    /// <param name="displayName">The name the browser gave the file. It is only shown to the person, never used as a path.</param>
    /// <exception cref="BackupUploadException">The file is empty or larger than <see cref="MaxUploadBytes"/>.</exception>
    public async Task<UploadedBackup> ReceiveUploadAsync(Stream source, string? displayName, CancellationToken cancellationToken = default)
    {
        var folder = NewFolder();
        var file = Path.Combine(folder, UploadedFileName);
        try
        {
            var size = await CopyWithLimitAsync(source, file, cancellationToken);
            var inspection = BackupFileInspector.Inspect(file);
            var content = inspection.Status is BackupFileStatus.Current or BackupFileStatus.Older ? BackupContentSummary.Read(file) : null;

            return new UploadedBackup(SafeDisplayName(displayName), file, size, inspection, content, folder, _logger);
        }
        catch
        {
            DeleteFolder(folder);

            throw;
        }
    }

    /// <summary>Deletes temporary folders left behind by an interrupted upload or download.</summary>
    public void DeleteOldTemporaryFiles(TimeSpan olderThan)
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        foreach (var folder in Directory.GetDirectories(_root))
        {
            if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(folder) > olderThan)
            {
                DeleteFolder(folder);
            }
        }
    }

    private async Task<long> CopyWithLimitAsync(Stream source, string file, CancellationToken cancellationToken)
    {
        long total = 0;
        var buffer = new byte[81920];
        await using var target = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxUploadBytes)
            {
                throw new BackupUploadException(BackupUploadFailure.TooLarge, "The file is larger than the limit.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            throw new BackupUploadException(BackupUploadFailure.Empty, "The file is empty.");
        }

        return total;
    }

    private string NewFolder()
    {
        var folder = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        return folder;
    }

    private static string SafeDisplayName(string? name)
    {
        // Both kinds of separator are removed: a browser on Windows sends only the file name, but nothing is trusted.
        var fileName = (name ?? string.Empty).Split('\\', '/').Last();
        var cleaned = new string(fileName.Where(character => !char.IsControl(character) && Path.GetInvalidFileNameChars().All(invalid => invalid != character)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            return "sikkerhetskopi.db";
        }

        return cleaned.Length > MaxDisplayNameLength ? cleaned[..MaxDisplayNameLength] : cleaned;
    }

    internal static void DeleteFolder(string folder, ILogger? logger = null)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning("A temporary backup folder could not be deleted ({InnerError}).", exception.GetType().Name);
        }
    }
}

/// <summary>A finished backup waiting to be handed to the browser. Disposing deletes the temporary folder.</summary>
public sealed class BackupDownload : IAsyncDisposable
{
    private readonly string _filePath;
    private readonly string _folder;
    private readonly ILogger _logger;

    internal BackupDownload(string fileName, string filePath, long sizeBytes, string folder, ILogger logger)
    {
        FileName = fileName;
        SizeBytes = sizeBytes;
        _filePath = filePath;
        _folder = folder;
        _logger = logger;
    }

    /// <summary>A name with date and time that tells the person what the file is.</summary>
    public string FileName { get; }

    public long SizeBytes { get; }

    public Stream OpenRead()
    {
        return new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public ValueTask DisposeAsync()
    {
        BackupTransfer.DeleteFolder(_folder, _logger);

        return ValueTask.CompletedTask;
    }
}

/// <summary>An uploaded file in a temporary folder, with the result of inspecting it. Disposing deletes it.</summary>
public sealed class UploadedBackup : IDisposable
{
    private readonly string _folder;
    private readonly ILogger _logger;

    internal UploadedBackup(string displayName, string filePath, long sizeBytes, BackupInspection inspection, BackupContentSummary? content, string folder, ILogger logger)
    {
        DisplayName = displayName;
        FilePath = filePath;
        SizeBytes = sizeBytes;
        Inspection = inspection;
        Content = content;
        _folder = folder;
        _logger = logger;
    }

    /// <summary>The name the browser sent, cleaned so it is safe to show. It is never used as a path.</summary>
    public string DisplayName { get; }

    /// <summary>The temporary file. It is the only path the restore ever gets from an upload.</summary>
    public string FilePath { get; }

    public long SizeBytes { get; }

    public BackupInspection Inspection { get; }

    /// <summary>What the backup holds, or null if it is not a usable SameieHub backup.</summary>
    public BackupContentSummary? Content { get; }

    public void Dispose()
    {
        BackupTransfer.DeleteFolder(_folder, _logger);
    }
}

public enum BackupUploadFailure
{
    TooLarge,
    Empty
}

public class BackupUploadException : Exception
{
    public BackupUploadException(BackupUploadFailure reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public BackupUploadFailure Reason { get; }
}
