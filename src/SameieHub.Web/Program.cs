using SameieHub.Domain;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Infrastructure.Restore;
using SameieHub.Web.Components;
using SameieHub.Web.Hosting;

// The Windows mode (--windows-host) is the way the program is started as a Windows program. Without it, the program starts
// exactly as it always has, so dotnet run, the demo scripts and the tests are not affected.
var windowsHost = WindowsHost.IsRequested(args);
var unattended = WindowsHost.IsUnattended(args);
var builder = windowsHost
    ? WindowsHost.CreateBuilder(args, AppContext.BaseDirectory)
    : WebApplication.CreateBuilder(args);

// The demo command (scripts/demo.ps1) is the only way demo data is created. It only runs against the dedicated
// demo database, so it can never fill the ordinary database or any other file by accident.
var seedDemo = args.Contains("--seed-demo");
var checkDemo = args.Contains("--check-demo");
if ((seedDemo || checkDemo) && !DemoDatabaseSeeder.IsDemoDatabasePath(builder.Configuration["Database:Path"], DemoDatabaseLocation.GetPath()))
{
    Console.Error.WriteLine($"The demo command only runs against the demo database. Set Database__Path to {DemoDatabaseLocation.GetPath()}.");
    return 1;
}

var databasePath = builder.Configuration["Database:Path"] ?? DatabaseLocation.GetDefaultPath();
WebAppSetup.AddServices(builder, databasePath, windowsHost);

// The installer asks a running program to stop in the orderly way (the same shutdown as Ctrl+C) through the pipe, instead of ending the process.
if (windowsHost && args.Contains("--stop"))
{
    var accepted = await ActivationClient.TryStopAsync(ActivationPipe.NameFor(databasePath), TimeSpan.FromSeconds(10));
    Console.WriteLine(accepted ? "SameieHub ble bedt om å avslutte." : "Fant ikke noe SameieHub-program som kjører.");

    return accepted ? 0 : 3;
}

// Told to the installer from the very start, so an update never replaces files under a program that is still starting up.
using var runningMarker = windowsHost ? RunningMarker.Create() : null;

var app = builder.Build();

InstanceLock? instanceLock = null;
if (windowsHost)
{
    var isDemo = WindowsHost.UsesDemoDatabase(databasePath) || seedDemo || checkDemo;
    var failure = isDemo
        ? StartupMessages.For(StartupFailureReason.DemoDatabaseNotAllowed)
        : SetupInProgress.IsActive() ? StartupMessages.For(StartupFailureReason.SetupInProgress) : null;
    if (failure == null)
    {
        var result = await WindowsStartup.PrepareAsync(
            databasePath,
            app.Services.GetRequiredService<IDatabaseRestore>(),
            () => app.Services.GetRequiredService<DatabaseUpgrader>().UpgradeAsync(),
            app.Logger,
            () => ActivationClient.TryActivateAsync(ActivationPipe.NameFor(databasePath), TimeSpan.FromSeconds(5)));
        if (result.ActivatedRunningInstance)
        {
            Console.WriteLine("SameieHub kjører allerede. Åpner en ny fane i nettleseren.");

            return 0;
        }

        failure = result.Failure;
        instanceLock = result.Lock;
    }

    if (failure != null)
    {
        WindowsConsole.ShowFailure(failure, unattended);

        return 1;
    }
}
else
{
    // A restore that was prepared while the program was running is carried out here, before any connection to the
    // database is opened. This is also where an interrupted restore is finished or undone.
    var restoreOutcome = app.Services.GetRequiredService<IDatabaseRestore>().ApplyPendingRestore();
    if (restoreOutcome.Kind != RestoreOutcomeKind.NothingPending)
    {
        app.Logger.LogWarning("Database restore at startup: {Result}.", restoreOutcome.Kind);
    }

    // If the state of the database cannot be guaranteed, the program stops here: it must not migrate, which would create a new, empty
    // database, and it must not start on an uncertain file. Every file is kept in the restore folder for manual recovery.
    if (restoreOutcome.Kind == RestoreOutcomeKind.Unresolved)
    {
        Console.Error.WriteLine(
            "SameieHub stopped: an interrupted database restore could not be resolved safely. No database was created and no files were deleted. "
            + "The old database and the prepared one are kept in the folder 'restore' next to the database file. "
            + "See the documentation ('Hvis gjenopprettingen står uavklart') before you start the program again.");

        return 1;
    }

    await app.Services.MigrateSameieHubDatabaseAsync();
}

if (seedDemo)
{
    return await RunDemoSeedingAsync(app.Services);
}

if (checkDemo)
{
    return await RunDemoCheckAsync(app.Services);
}

// Only the folder and file name are logged, because the full path can contain the Windows user name.
app.Logger.LogInformation(
    "Using database {FolderName}/{FileName}",
    Path.GetFileName(Path.GetDirectoryName(databasePath)),
    Path.GetFileName(databasePath));

WebAppSetup.UsePipeline(app, windowsHost);

if (!windowsHost)
{
    app.Run();

    return 0;
}

return await RunWindowsHostAsync(app, instanceLock!, unattended);

static async Task<int> RunWindowsHostAsync(WebApplication app, InstanceLock instanceLock, bool unattended)
{
    using (instanceLock)
    {
        var launcher = new BrowserLauncher(app, unattended);
        var databasePath = app.Configuration["Database:Path"] ?? DatabaseLocation.GetDefaultPath();
        ActivationListener? activation = null;
        var unsafeBinding = false;
        WindowsHost.RegisterShutdown(app.Lifetime, app.Logger);
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            if (!WindowsHost.AreAllLoopback(WindowsHost.GetAddresses(app)))
            {
                // Fail closed: a setting added an address that is not 127.0.0.1, so nothing is opened and the program stops at once.
                unsafeBinding = true;
                app.Logger.LogError("The server listens on an address other than 127.0.0.1, so it is stopped.");
                WindowsConsole.ShowFailure(StartupMessages.For(StartupFailureReason.UnsafeNetworkBinding), unattended);
                app.Lifetime.StopApplication();

                return;
            }

            activation = AnnounceStart(app, launcher, databasePath);
        });
        WindowsConsole.SetTitle();
        WindowsConsole.RegisterCloseHandler(app.Lifetime, app.Logger);

        try
        {
            await app.RunAsync();
        }
        catch (Exception exception) when (exception is IOException or System.Net.Sockets.SocketException)
        {
            app.Logger.LogError("The server could not start ({ExceptionType}).", exception.GetType().Name);
            WindowsConsole.ShowFailure(StartupMessages.For(StartupFailureReason.ServerCouldNotStart), unattended);

            return 1;
        }
        finally
        {
            activation?.Dispose();
        }

        app.Logger.LogInformation("SameieHub has stopped.");

        return unsafeBinding ? 1 : 0;
    }
}

static ActivationListener? AnnounceStart(WebApplication app, BrowserLauncher launcher, string databasePath)
{
    var address = WindowsHost.GetAddress(app);
    if (address == null)
    {
        return null;
    }

    app.Logger.LogInformation("SameieHub is running on {Address}.", address);
    WindowsConsole.ShowRunningBanner();

    // From now on a second start can ask this program to open the browser again, and the installer can ask it to stop.
    var activation = ActivationListener.TryStart(
        ActivationPipe.NameFor(databasePath),
        launcher.OpenAsync,
        app.Logger,
        () =>
        {
            app.Logger.LogInformation("The program was asked to stop.");
            app.Lifetime.StopApplication();

            return Task.CompletedTask;
        });
    launcher.OpenAsync();

    return activation;
}

static async Task<int> RunDemoSeedingAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var seeder = CreateDemoSeeder(scope.ServiceProvider);

    try
    {
        var result = await seeder.SeedAsync();

        Console.WriteLine(result == DemoSeedResult.Seeded
            ? "Demo data created."
            : "The demo data is already in the database. Nothing was changed.");

        return 0;
    }
    catch (Exception exception) when (exception is DemoDatabaseNotEmptyException or DemoDatabaseIncompleteException or DemoDatabaseOutdatedException or AggregateException)
    {
        Console.Error.WriteLine(exception.Message);

        return 1;
    }
}

// Only looks at the demo database and changes nothing. Exit code 0: the demo has charging data or is empty,
// 2: an older demo without charging data, 3: partly created or other data.
static async Task<int> RunDemoCheckAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var state = await CreateDemoSeeder(scope.ServiceProvider).InspectAsync();

    switch (state)
    {
        case DemoDatabaseState.OutdatedWithoutCharging:
            Console.WriteLine(new DemoDatabaseOutdatedException().Message);

            return 2;
        case DemoDatabaseState.PartlyCreated:
            Console.WriteLine("The demo database is partly created or has been changed so it no longer matches the demo. Run the demo command 'reset' for a fresh demo.");

            return 3;
        case DemoDatabaseState.OtherData:
            Console.WriteLine("The demo database contains other data than the demo.");

            return 3;
        default:
            return 0;
    }
}

static DemoDatabaseSeeder CreateDemoSeeder(IServiceProvider provider)
{
    return new DemoDatabaseSeeder(
        provider.GetRequiredService<IHousingUnitRepository>(),
        provider.GetRequiredService<IBoardCaseRepository>(),
        provider.GetRequiredService<ITaskItemRepository>(),
        provider.GetRequiredService<IChargingFacilityRepository>(),
        provider.GetRequiredService<IEvChargerRepository>(),
        provider.GetRequiredService<IMeterRepository>(),
        provider.GetRequiredService<IMeterReadingRepository>(),
        TimeProvider.System);
}
