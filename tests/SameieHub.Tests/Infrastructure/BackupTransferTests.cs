using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Domain;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Restore;
using static SameieHub.Tests.Infrastructure.DatabaseTestData;
using static SameieHub.Tests.Infrastructure.DatabaseTestFiles;

namespace SameieHub.Tests.Infrastructure;

/// <summary>
/// What the backup page needs from the server: a backup that can be handed to the browser without a public address, an
/// upload that is stored under a safe name and checked, and temporary files that never stay behind.
/// </summary>
public class BackupTransferTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;
    private string _validBackup = null!;
    private string _folder = null!;

    private string TransferRoot => Path.Combine(Path.GetDirectoryName(_database.DatabasePath)!, "transfer");

    public async Task InitializeAsync()
    {
        _database = await SqliteTestDatabase.CreateAsync();
        _folder = Path.Combine(Path.GetTempPath(), "SameieHubTransferTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_folder);
        await FillAllModulesAsync(_database);
        _validBackup = Path.Combine(_folder, "gyldig.db");
        await new SqliteDatabaseBackup(_database.DatabasePath, _database.Clock, NullLogger<SqliteDatabaseBackup>.Instance).CreateBackupAsync(_validBackup);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private BackupTransfer NewTransfer(IDatabaseBackup? backup = null, long maxUploadBytes = BackupTransfer.DefaultMaxUploadBytes)
    {
        backup ??= new SqliteDatabaseBackup(_database.DatabasePath, _database.Clock, NullLogger<SqliteDatabaseBackup>.Instance);

        return new BackupTransfer(_database.DatabasePath, backup, _database.Clock, NullLogger<BackupTransfer>.Instance, maxUploadBytes);
    }

    private static int FolderCount(string root) => Directory.Exists(root) ? Directory.GetDirectories(root).Length : 0;

    // Download

    [Fact]
    public async Task CreateDownload_GivesAValidBackupWithADatedNameAndDeletesEverythingWhenDisposed()
    {
        var download = await NewTransfer().CreateDownloadAsync();

        // The test clock is 2026-11-18 10:30 UTC, which is 11:30 in Norway.
        Assert.Equal("SameieHub-sikkerhetskopi-2026-11-18-1130.db", download.FileName);
        Assert.True(download.SizeBytes > 0);
        await using (var stream = download.OpenRead())
        {
            Assert.Equal(download.SizeBytes, stream.Length);
        }

        Assert.Equal(1, FolderCount(TransferRoot));
        await download.DisposeAsync();

        Assert.Equal(0, FolderCount(TransferRoot));
    }

    [Fact]
    public async Task CreateDownload_WhenTheBackupFails_LeavesNoTemporaryFolderAndPassesTheErrorOn()
    {
        var transfer = NewTransfer(new FailingBackup());

        var exception = await Assert.ThrowsAsync<DatabaseBackupException>(() => transfer.CreateDownloadAsync());

        Assert.Equal(DatabaseBackupFailure.WriteFailed, exception.Reason);
        Assert.Equal(0, FolderCount(TransferRoot));
    }

    // Upload

    [Fact]
    public async Task ReceiveUpload_StoresTheFileUnderASafeNameAndTellsWhatIsInIt()
    {
        await using var source = File.OpenRead(_validBackup);

        using var upload = await NewTransfer().ReceiveUploadAsync(source, @"..\..\evil\sikkerhetskopi.db");

        Assert.Equal("sikkerhetskopi.db", upload.DisplayName);
        Assert.StartsWith(TransferRoot + Path.DirectorySeparatorChar, upload.FilePath);
        Assert.Equal("upload.db", Path.GetFileName(upload.FilePath));
        Assert.Equal(BackupFileStatus.Current, upload.Inspection.Status);
        Assert.Equal(9, upload.Content!.HousingUnits);
        Assert.Equal(1, upload.Content.BoardCases);
        Assert.Equal(1, upload.Content.Tasks);
        Assert.Equal(8, upload.Content.Chargers);
        Assert.Equal(27, upload.Content.MeterReadings);
    }

    [Fact]
    public async Task ReceiveUpload_WhenDisposed_DeletesTheTemporaryFile()
    {
        await using var source = File.OpenRead(_validBackup);
        var upload = await NewTransfer().ReceiveUploadAsync(source, "x.db");
        var folder = Path.GetDirectoryName(upload.FilePath)!;

        upload.Dispose();

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task ReceiveUpload_FileLargerThanTheLimit_IsRefusedAndNothingStaysBehind()
    {
        await using var source = File.OpenRead(_validBackup);
        var transfer = NewTransfer(maxUploadBytes: 1000);

        var exception = await Assert.ThrowsAsync<BackupUploadException>(() => transfer.ReceiveUploadAsync(source, "stor.db"));

        Assert.Equal(BackupUploadFailure.TooLarge, exception.Reason);
        Assert.Equal(0, FolderCount(TransferRoot));
    }

    [Fact]
    public async Task ReceiveUpload_EmptyFile_IsRefusedAndNothingStaysBehind()
    {
        await using var empty = new MemoryStream();

        var exception = await Assert.ThrowsAsync<BackupUploadException>(() => NewTransfer().ReceiveUploadAsync(empty, "tom.db"));

        Assert.Equal(BackupUploadFailure.Empty, exception.Reason);
        Assert.Equal(0, FolderCount(TransferRoot));
    }

    [Fact]
    public async Task ReceiveUpload_UploadThatIsCutOff_LeavesNothingBehind()
    {
        await using var cutOff = new CutOffStream(await File.ReadAllBytesAsync(_validBackup));

        await Assert.ThrowsAsync<IOException>(() => NewTransfer().ReceiveUploadAsync(cutOff, "avbrutt.db"));

        Assert.Equal(0, FolderCount(TransferRoot));
    }

    [Fact]
    public async Task ReceiveUpload_FileThatIsNotADatabase_IsStoredOnlyLongEnoughToBeInspected()
    {
        await using var text = new MemoryStream("dette er ikke en database, bare tekst som er lang nok til å se ut som en fil"u8.ToArray());

        using var upload = await NewTransfer().ReceiveUploadAsync(text, "tekst.db");

        Assert.Equal(BackupFileStatus.Invalid, upload.Inspection.Status);
        Assert.Null(upload.Content);
    }

    // Old temporary files

    [Fact]
    public async Task DeleteOldTemporaryFiles_RemovesOnlyFoldersThatAreOldEnough()
    {
        var old = Directory.CreateDirectory(Path.Combine(TransferRoot, "gammel"));
        await File.WriteAllTextAsync(Path.Combine(old.FullName, "upload.db"), "x");
        old.LastWriteTimeUtc = DateTime.UtcNow.AddDays(-2);
        var recent = Directory.CreateDirectory(Path.Combine(TransferRoot, "ny"));

        NewTransfer().DeleteOldTemporaryFiles(TimeSpan.FromHours(1));

        Assert.False(Directory.Exists(old.FullName));
        Assert.True(Directory.Exists(recent.FullName));
    }

    // The summary also works for an older schema

    [Fact]
    public async Task Content_OlderDatabaseWithoutChargingTables_CountsWhatExists()
    {
        var olderFile = Path.Combine(_folder, "eldre.db");
        await using (var older = await SqliteTestDatabase.CreateUnmigratedAsync())
        {
            await older.MigrateToMigrationAsync("AddTaskItems");
            await older.Service.RegisterAsync(new HousingUnitDetails { Address = "Fiktivdalen 3" });
            await older.StopForResetAsync();
            await CopyAsync(older.DatabasePath, olderFile);
        }

        var content = BackupContentSummary.Read(olderFile);

        Assert.Equal(1, content.HousingUnits);
        Assert.Equal(0, content.BoardCases);
        Assert.Null(content.Chargers);
        Assert.Null(content.MeterReadings);
    }

    private sealed class FailingBackup : IDatabaseBackup
    {
        public Task<DatabaseBackupResult> CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
        {
            throw new DatabaseBackupException(DatabaseBackupFailure.WriteFailed, "Simulated failure.");
        }
    }

    private sealed class CutOffStream : MemoryStream
    {
        public CutOffStream(byte[] bytes)
            : base(bytes)
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Position > Length / 2)
            {
                throw new IOException("The connection was lost.");
            }

            return base.Read(buffer, offset, Math.Min(count, 512));
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position > Length / 2)
            {
                throw new IOException("The connection was lost.");
            }

            return base.ReadAsync(buffer[..Math.Min(buffer.Length, 512)], cancellationToken);
        }
    }
}
