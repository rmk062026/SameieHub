using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Domain;
using SameieHub.Infrastructure.Backup;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Restore;
using SameieHub.Tests.Fakes;
using SameieHub.Tests.Infrastructure;
using SameieHub.Web.Components.Pages;
using SameieHub.Web.Components.Pages.Settings;
using static SameieHub.Tests.Infrastructure.DatabaseTestData;
using static SameieHub.Tests.Infrastructure.DatabaseTestFiles;

namespace SameieHub.Tests.Web;

/// <summary>
/// The backup page with the real backup and restore engines on a temporary database. The page can only do what the engines allow,
/// so these tests also show that it cannot get around their safety rules. The ordinary AppData database is never used.
/// </summary>
public class BackupPageTests : UiTestBase, IAsyncLifetime
{
    private const string ModulePath = "./Components/Pages/Settings/BackupPage.razor.js";

    private SqliteTestDatabase _live = null!;
    private string _folder = null!;
    private string _validBackup = null!;
    private BunitJSModuleInterop _module = null!;

    public BackupPageTests()
    {
        // The engines copy and check real database files, which can take longer than a second when many tests run at once.
        DefaultWaitTimeout = TimeSpan.FromSeconds(15);
    }

    public async Task InitializeAsync()
    {
        _live = await SqliteTestDatabase.CreateAsync();
        _folder = Path.Combine(Path.GetTempPath(), "SameieHubBackupPageTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_folder);
        await FillAllModulesAsync(_live);
        _validBackup = Path.Combine(_folder, "gyldig.db");
        await new SqliteDatabaseBackup(_live.DatabasePath, _live.Clock, NullLogger<SqliteDatabaseBackup>.Instance).CreateBackupAsync(_validBackup);
        _module = JSInterop.SetupModule(ModulePath);
        _module.SetupVoid("downloadFromStream", _ => true).SetVoidResult();
    }

    public new async Task DisposeAsync()
    {
        await _live.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string TransferRoot => Path.Combine(Path.GetDirectoryName(_live.DatabasePath)!, "transfer");

    private string RestoreFolder => Path.Combine(Path.GetDirectoryName(_live.DatabasePath)!, "restore");

    private SqliteDatabaseRestore Engine { get; set; } = null!;

    /// <summary>Registers the real engines (or replacements) before the page is rendered.</summary>
    private void UseEngines(IDatabaseBackup? backup = null, string? databasePath = null, long maxUploadBytes = BackupTransfer.DefaultMaxUploadBytes)
    {
        var path = databasePath ?? _live.DatabasePath;
        backup ??= new SqliteDatabaseBackup(_live.DatabasePath, _live.Clock, NullLogger<SqliteDatabaseBackup>.Instance);
        Engine = new SqliteDatabaseRestore(path, _live.Clock, NullLogger<SqliteDatabaseRestore>.Instance);
        Services.AddSingleton(backup);
        Services.AddSingleton<IDatabaseRestore>(Engine);
        Services.AddSingleton(new BackupTransfer(_live.DatabasePath, backup, _live.Clock, NullLogger<BackupTransfer>.Instance, maxUploadBytes));
    }

    private IRenderedComponent<BackupPage> RenderPage()
    {
        var cut = Render<BackupPage>();
        cut.WaitForElement("#create-backup");

        return cut;
    }

    private async Task UploadAsync(IRenderedComponent<BackupPage> cut, string file, string? name = null)
    {
        var content = InputFileContent.CreateFromBinary(await File.ReadAllBytesAsync(file), name ?? "sikkerhetskopi.db");
        cut.FindComponent<InputFile>().UploadFiles(content);
    }

    private static int FolderCount(string root) => Directory.Exists(root) ? Directory.GetDirectories(root).Length : 0;

    // Backup

    [Fact]
    public async Task Backup_Click_HandsABackupToTheBrowserAndLeavesNoTemporaryFiles()
    {
        UseEngines();
        var cut = RenderPage();

        cut.Find("#create-backup").Click();

        cut.WaitForElement("[data-testid=backup-result]");
        var invocation = _module.VerifyInvoke("downloadFromStream");
        Assert.Equal("SameieHub-sikkerhetskopi-2026-11-18-1130.db", invocation.Arguments[0]);
        Assert.Contains("SameieHub-sikkerhetskopi-2026-11-18-1130.db", cut.Find("[data-testid=backup-result]").TextContent);
        Assert.Contains("trygt", cut.Find("[data-testid=backup-result]").TextContent);
        Assert.Equal(0, FolderCount(TransferRoot));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Backup_Failure_ShowsAPlainMessageAndLeavesNothingBehind()
    {
        UseEngines(new FailingBackup());
        var cut = RenderPage();

        cut.Find("#create-backup").Click();

        cut.WaitForAssertion(() => Assert.Contains("kunne ikke lages", cut.Find("[data-testid=backup-error]").TextContent));
        Assert.Empty(_module.Invocations);
        Assert.Equal(0, FolderCount(TransferRoot));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Backup_WhileItIsRunning_CannotBeStartedAgain()
    {
        var gate = new BlockingBackup(new SqliteDatabaseBackup(_live.DatabasePath, _live.Clock, NullLogger<SqliteDatabaseBackup>.Instance));
        UseEngines(gate);
        var cut = RenderPage();

        cut.Find("#create-backup").Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find("#create-backup").HasAttribute("disabled")));
        cut.Find("#create-backup").Click();
        gate.Release();

        cut.WaitForElement("[data-testid=backup-result]");
        Assert.Equal(1, gate.Calls);
        await Task.CompletedTask;
    }

    // Restore: choosing and checking a file

    [Fact]
    public async Task Restore_ValidFile_IsCheckedAndExplainedButNothingIsPreparedBeforeTheConfirmation()
    {
        UseEngines();
        var cut = RenderPage();

        await UploadAsync(cut, _validBackup);

        cut.WaitForElement("[data-testid=restore-check]");
        var check = cut.Find("[data-testid=restore-check]").TextContent;
        Assert.Contains("kan brukes", check);
        Assert.Contains("Boenheter", check);
        Assert.Contains("Avlesninger", check);
        Assert.Contains("27", check);
        Assert.Null(Engine.GetPending());
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
        Assert.DoesNotContain(_live.DatabasePath, cut.Markup);
        Assert.DoesNotContain(_folder, cut.Markup);
    }

    [Fact]
    public async Task Restore_CancelBeforeConfirming_DeletesTheUploadAndPreparesNothing()
    {
        UseEngines();
        var cut = RenderPage();
        await UploadAsync(cut, _validBackup);
        cut.WaitForElement("[data-testid=restore-check]");
        Assert.Equal(1, FolderCount(TransferRoot));

        cut.Find("#cancel-check").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=restore-check]")));
        Assert.Equal(0, FolderCount(TransferRoot));
        Assert.Null(Engine.GetPending());
    }

    [Fact]
    public async Task Restore_ConfirmationIsRequired_ThenTheRestoreIsPreparedAndTheRestartIsExplained()
    {
        UseEngines();
        var cut = RenderPage();
        await UploadAsync(cut, _validBackup, "min-kopi.db");
        cut.WaitForElement("[data-testid=restore-check]");

        cut.Find("#start-restore").Click();
        var dialog = cut.Find("[role=alertdialog]");
        Assert.Contains("erstattes", dialog.TextContent);
        Assert.Null(Engine.GetPending());

        cut.Find("#confirm-restore").Click();

        cut.WaitForElement("[data-testid=restore-pending]");
        var pending = cut.Find("[data-testid=restore-pending]").TextContent;
        Assert.Contains("min-kopi.db", pending);
        Assert.Contains("starte", pending);
        Assert.Contains("lukke", pending);
        Assert.NotNull(Engine.GetPending());
        Assert.Equal(0, FolderCount(TransferRoot));
    }

    [Fact]
    public async Task Restore_PreparedRestore_CanBeCancelledBeforeTheRestart()
    {
        UseEngines();
        var cut = RenderPage();
        await UploadAsync(cut, _validBackup);
        cut.WaitForElement("[data-testid=restore-check]");
        cut.Find("#start-restore").Click();
        cut.Find("#confirm-restore").Click();
        cut.WaitForElement("[data-testid=restore-pending]");

        cut.Find("#cancel-pending").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=restore-pending]")));
        Assert.Null(Engine.GetPending());
        Assert.False(File.Exists(Path.Combine(RestoreFolder, "pending.db")));
    }

    [Fact]
    public async Task Restore_ARestoreThatIsWaiting_IsShownWhenThePageIsOpenedAgain()
    {
        UseEngines();
        await Engine.PrepareAsync(_validBackup);

        var cut = RenderPage();

        Assert.Contains("gyldig.db", cut.Find("[data-testid=restore-pending]").TextContent);
    }

    [Fact]
    public async Task Restore_Section_IsClosedByDefault_ButTheSummaryTellsWhatIsInside_AndTheBackupWarningIsAlwaysVisible()
    {
        UseEngines();
        var cut = RenderPage();

        var details = cut.Find("[data-testid=restore-section] details");
        Assert.False(details.HasAttribute("open"));
        Assert.Contains("Gjenopprett fra sikkerhetskopi", details.QuerySelector("summary")!.TextContent);
        Assert.Contains("Erstatter alle dataene", details.QuerySelector("summary")!.TextContent);
        Assert.Contains("ikke kryptert", cut.Markup);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Restore_Section_OpensByItselfWhenThereIsAnErrorOrAFileToConfirm()
    {
        UseEngines();
        var cut = RenderPage();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hei", "regneark.xlsx"));
        cut.WaitForElement("[data-testid=restore-error]");
        Assert.True(cut.Find("[data-testid=restore-section] details").HasAttribute("open"));

        await UploadAsync(cut, _validBackup);
        cut.WaitForElement("[data-testid=restore-check]");
        Assert.True(cut.Find("[data-testid=restore-section] details").HasAttribute("open"));
    }

    [Fact]
    public async Task Restore_ARestoreThatIsWaiting_IsShownAboveTheBackupSection_AndTheSectionForChoosingAFileIsGone()
    {
        UseEngines();
        await Engine.PrepareAsync(_validBackup);

        var cut = RenderPage();

        var markup = cut.Markup;
        Assert.True(markup.IndexOf("restore-pending", StringComparison.Ordinal) < markup.IndexOf("create-backup", StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid=restore-section]"));
        Assert.Contains("Lukk SameieHub", cut.Find("[data-testid=restore-pending]").TextContent);
    }

    [Fact]
    public async Task Restart_InDevelopment_KeepsTheManualInstruction_AndHasNoButtonThatCouldStopTheServer()
    {
        UseEngines();
        await Engine.PrepareAsync(_validBackup);

        var cut = RenderPage();

        var pending = cut.Find("[data-testid=restore-pending]").TextContent;
        Assert.Contains("Lukk SameieHub", pending);
        Assert.Empty(cut.FindAll("#restart-now"));
        Assert.NotNull(cut.Find("#cancel-pending"));
    }

    [Fact]
    public async Task Restart_InWindowsMode_OffersExitAndRestart_AndSaysThatTheRestoreHasNotHappenedYet()
    {
        UseShutdown(canShutDown: true, canRestart: true);
        UseEngines();
        await Engine.PrepareAsync(_validBackup);

        var cut = RenderPage();

        var pending = cut.Find("[data-testid=restore-pending]");
        Assert.Contains("ennå ikke gjennomført", pending.TextContent);
        Assert.Contains("Start-menyen", pending.TextContent);
        Assert.Equal("Avslutt og start på nytt", cut.Find("#restart-now").TextContent.Trim());
        Assert.Equal("button", cut.Find("#restart-now").GetAttribute("type"));
        Assert.NotNull(cut.Find("#cancel-pending"));
        Assert.False(Shutdown.IsRequested);
    }

    [Fact]
    public async Task Restart_Click_AsksTheProgramToRestart_AndDoesNotApplyTheRestoreItself()
    {
        UseShutdown(canShutDown: true, canRestart: true);
        JSInterop.SetupModule("./Components/Layout/ShutdownNotice.razor.js").SetupVoid("markStopped");
        UseEngines();
        await Engine.PrepareAsync(_validBackup);
        var cut = RenderPage();

        cut.Find("#restart-now").Click();

        Assert.True(Shutdown.IsRequested);
        Assert.True(Shutdown.RestartRequested);
        Assert.NotNull(Engine.GetPending());
        Assert.Null(Engine.GetLastOutcome());
    }

    [Fact]
    public async Task Restart_WhenTheProgramCannotStartItself_PointsToTheMenuButtonAndTheStartMenu()
    {
        UseShutdown(canShutDown: true, canRestart: false);
        UseEngines();
        await Engine.PrepareAsync(_validBackup);

        var cut = RenderPage();

        var pending = cut.Find("[data-testid=restore-pending]").TextContent;
        Assert.Empty(cut.FindAll("#restart-now"));
        Assert.Contains("Avslutt SameieHub", pending);
        Assert.Contains("Start-menyen", pending);
    }

    [Fact]
    public async Task Restart_CancellingTheWaitingRestore_StillWorksInWindowsMode_AndTheRestartButtonGoesAway()
    {
        UseShutdown(canShutDown: true, canRestart: true);
        UseEngines();
        await Engine.PrepareAsync(_validBackup);
        var cut = RenderPage();

        cut.Find("#cancel-pending").Click();

        Assert.Null(Engine.GetPending());
        Assert.Empty(cut.FindAll("[data-testid=restore-pending]"));
        Assert.Empty(cut.FindAll("#restart-now"));
        Assert.False(Shutdown.IsRequested);
    }

    [Fact]
    public void Restart_WithNothingWaiting_HasNoRestartButton_AndNoRestoreIsEverStartedByTheButtons()
    {
        UseShutdown(canShutDown: true, canRestart: true);
        UseEngines();

        var cut = RenderPage();

        Assert.Empty(cut.FindAll("#restart-now"));
        Assert.Empty(cut.FindAll("[data-testid=restore-pending]"));
    }

    [Theory]
    [InlineData("text", "ikke en gyldig sikkerhetskopi")]
    [InlineData("other", "ikke fra SameieHub")]
    [InlineData("newer", "nyere versjon")]
    public async Task Restore_FileThatCannotBeUsed_IsRefusedWithAPlainMessageAndNothingStaysBehind(string kind, string expected)
    {
        var file = Path.Combine(_folder, "valgt.db");
        switch (kind)
        {
            case "text":
                await File.WriteAllTextAsync(file, "dette er ikke en database, bare tekst som er lang nok til å se ut som en fil");
                break;
            case "other":
                await ExecuteAsync(file, "CREATE TABLE Kunder (Id INTEGER PRIMARY KEY)");
                break;
            default:
                await CopyAsync(_validBackup, file);
                await ExecuteAsync(file, "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29990101000000_FraEnNyereVersjon', '99.0.0')");
                break;
        }

        UseEngines();
        var cut = RenderPage();

        await UploadAsync(cut, file);

        cut.WaitForAssertion(() => Assert.Contains(expected, cut.Find("[data-testid=restore-error]").TextContent));
        Assert.Empty(cut.FindAll("#start-restore"));
        Assert.Equal(0, FolderCount(TransferRoot));
        Assert.Null(Engine.GetPending());
    }

    [Fact]
    public async Task Restore_FileThatIsTooLarge_IsRefusedAndNothingStaysBehind()
    {
        UseEngines(maxUploadBytes: 1000);
        var cut = RenderPage();

        await UploadAsync(cut, _validBackup);

        cut.WaitForAssertion(() => Assert.Contains("for stor", cut.Find("[data-testid=restore-error]").TextContent));
        Assert.Equal(0, FolderCount(TransferRoot));
    }

    [Fact]
    public async Task Restore_FileWithTheWrongType_IsNotEvenRead()
    {
        UseEngines();
        var cut = RenderPage();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hei", "regneark.xlsx"));

        cut.WaitForAssertion(() => Assert.Contains(".db", cut.Find("[data-testid=restore-error]").TextContent));
        Assert.Equal(0, FolderCount(TransferRoot));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Restore_InTheDemo_IsRefusedByTheEngineAndTheDemoFolderIsNotTouched()
    {
        var demoPath = DemoDatabaseLocation.GetPath();
        var demoRestoreFolder = Path.Combine(Path.GetDirectoryName(demoPath)!, "restore");
        UseEngines(databasePath: demoPath);
        var cut = RenderPage();
        await UploadAsync(cut, _validBackup);
        cut.WaitForElement("[data-testid=restore-check]");
        cut.Find("#start-restore").Click();

        cut.Find("#confirm-restore").Click();

        cut.WaitForAssertion(() => Assert.Contains("demo", cut.Find("[data-testid=restore-error]").TextContent, StringComparison.OrdinalIgnoreCase));
        Assert.False(Directory.Exists(demoRestoreFolder));
        Assert.Empty(cut.FindAll("[data-testid=restore-pending]"));
    }

    // The result after the restart

    [Theory]
    [InlineData(RestoreOutcomeKind.Applied, "gjennomført")]
    [InlineData(RestoreOutcomeKind.AppliedAfterRecovery, "gjennomført")]
    [InlineData(RestoreOutcomeKind.RolledBack, "ikke gjennomført")]
    [InlineData(RestoreOutcomeKind.Rejected, "ikke gjennomført")]
    public async Task Result_AfterTheRestart_IsShownOnThePageAndTheDashboardUntilItIsDismissed(RestoreOutcomeKind kind, string expected)
    {
        var restore = new FakeDatabaseRestore { LastOutcome = new RestoreOutcome(kind, Clock.GetUtcNow().UtcDateTime, "before-restore-20261118103000") };
        Services.AddSingleton<IDatabaseRestore>(restore);
        UseEnginesExceptRestore();

        var dashboard = Render<Dashboard>();
        dashboard.WaitForElement("[data-testid=restore-result]");
        Assert.Contains(expected, dashboard.Find("[data-testid=restore-result]").TextContent);

        var page = RenderPage();
        Assert.Contains(expected, page.Find("[data-testid=restore-result]").TextContent);

        page.Find("#dismiss-result").Click();

        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-testid=restore-result]")));
        Assert.Null(restore.LastOutcome);
        await Task.CompletedTask;
    }

    private void UseEnginesExceptRestore()
    {
        var backup = new SqliteDatabaseBackup(_live.DatabasePath, _live.Clock, NullLogger<SqliteDatabaseBackup>.Instance);
        Services.AddSingleton<IDatabaseBackup>(backup);
        Services.AddSingleton(new BackupTransfer(_live.DatabasePath, backup, _live.Clock, NullLogger<BackupTransfer>.Instance));
    }

    private sealed class FailingBackup : IDatabaseBackup
    {
        public Task<DatabaseBackupResult> CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default) =>
            throw new DatabaseBackupException(DatabaseBackupFailure.WriteFailed, "Simulated failure.");
    }

    private sealed class BlockingBackup : IDatabaseBackup
    {
        private readonly IDatabaseBackup _inner;
        private readonly TaskCompletionSource _gate = new();

        public BlockingBackup(IDatabaseBackup inner)
        {
            _inner = inner;
        }

        public int Calls { get; private set; }

        public void Release() => _gate.TrySetResult();

        public async Task<DatabaseBackupResult> CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
        {
            Calls++;
            await _gate.Task;

            return await _inner.CreateBackupAsync(destinationPath, cancellationToken);
        }
    }
}
