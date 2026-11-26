using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using SameieHub.Infrastructure.Demo;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Web.Hosting;

/// <summary>
/// The settings that make SameieHub behave as a program a person starts on Windows, as opposed to the development start
/// (<c>dotnet run</c>, the demo scripts), which is left exactly as it was. The Windows mode is only used when it is asked
/// for with <c>--windows-host</c> or the environment variable <c>SAMEIEHUB_WINDOWS_HOST=1</c>.
/// </summary>
internal static class WindowsHost
{
    public const string SwitchName = "--windows-host";
    public const string UnattendedSwitchName = "--unattended";
    public const string EnvironmentVariableName = "SAMEIEHUB_WINDOWS_HOST";

    /// <summary>Only these host names are answered. Everything else, such as a page on another site that points its own name at this computer, is refused.</summary>
    public const string AllowedHosts = "127.0.0.1;localhost";

    public static bool IsRequested(string[] args)
    {
        return args.Contains(SwitchName) || Environment.GetEnvironmentVariable(EnvironmentVariableName) == "1";
    }

    public static bool IsUnattended(string[] args)
    {
        return args.Contains(UnattendedSwitchName);
    }

    /// <summary>
    /// The content root is the program's own folder, so <c>appsettings.json</c> and the web files are found no matter which
    /// folder the program was started from (a shortcut can start it anywhere). The server listens on 127.0.0.1 only, on a
    /// port the operating system chooses, so no other computer can reach it and the port can never be taken.
    /// </summary>
    public static WebApplicationBuilder CreateBuilder(string[] args, string contentRoot)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = contentRoot });

        builder.Configuration["AllowedHosts"] = AllowedHosts;
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        ConfigureLogging(builder);

        return builder;
    }

    /// <summary>The address the server actually listens on. Only known after the server has started.</summary>
    public static string? GetAddress(WebApplication app)
    {
        return app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
    }

    /// <summary>
    /// When the program stops, every database connection is closed. SQLite keeps connections in a pool, which would leave the
    /// database file locked and the WAL file unmerged after the program has ended.
    /// </summary>
    public static void RegisterShutdown(IHostApplicationLifetime lifetime, ILogger logger)
    {
        lifetime.ApplicationStopping.Register(() =>
        {
            logger.LogInformation("SameieHub is shutting down.");
            SqliteConnection.ClearAllPools();
        });
    }

    public static bool UsesDemoDatabase(string? databasePath)
    {
        return DemoDatabaseSeeder.IsDemoDatabasePath(databasePath, DemoDatabaseLocation.GetPath());
    }

    private static void ConfigureLogging(WebApplicationBuilder builder)
    {
        var databasePath = builder.Configuration["Database:Path"] ?? DatabaseLocation.GetDefaultPath();
        var logFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "logs");
        var fileSink = new FileLogSink(logFolder, TimeProvider.System);

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new SafeLoggerProvider(Console.WriteLine, TimeProvider.System));
        builder.Logging.AddProvider(new SafeLoggerProvider(fileSink.Write, TimeProvider.System));

        // The framework's own messages are limited to warnings and errors: its startup messages contain full folder names
        // (which include the Windows user name), and the database commands contain SQL. SameieHub logs its own start and stop.
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("System", LogLevel.Warning);
        builder.Logging.AddFilter("SameieHub", LogLevel.Information);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
    }
}

/// <summary>What a person sees when the program cannot start, and the small things about the console window.</summary>
internal static class WindowsConsole
{
    private const uint MessageBoxIconError = 0x10;

    public static void SetTitle()
    {
        if (OperatingSystem.IsWindows() && !Console.IsOutputRedirected)
        {
            Console.Title = "SameieHub";
        }
    }

    public static void ShowRunningBanner(string address)
    {
        Console.WriteLine();
        Console.WriteLine("SameieHub kjører.");
        Console.WriteLine($"Adresse: {address}");
        Console.WriteLine("Lukk dette vinduet eller trykk Ctrl+C for å avslutte SameieHub. Å lukke nettleseren avslutter ikke programmet.");
        Console.WriteLine("Intern testversjon uten tilgangsbeskyttelse: bruk bare fiktive data.");
        Console.WriteLine();
    }

    /// <summary>Writes the message to the window, and also shows it in a message box so it is not lost when the window closes at once.</summary>
    public static void ShowFailure(StartupFailure failure, bool unattended)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine(failure.Message);
        Console.Error.WriteLine();

        if (unattended)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            MessageBoxW(IntPtr.Zero, failure.Message, "SameieHub kunne ikke starte", MessageBoxIconError);
        }
        else if (!Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Trykk en tast for å lukke.");
            Console.ReadKey(intercept: true);
        }
    }

    public static void OpenBrowser(string address, ILogger logger)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogWarning("The browser could not be opened ({ExceptionType}). Open the address shown in the window.", exception.GetType().Name);
        }
    }

    /// <summary>
    /// Closing the console window with the X button does not give the program a normal shutdown: .NET only stops the host for
    /// Ctrl+C and termination requests, so Windows ended the program at once and the shutdown code never ran (tested). This
    /// handler stops the host properly and waits for it, within the few seconds Windows allows.
    /// </summary>
    public static void RegisterCloseHandler(IHostApplicationLifetime lifetime, ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var stopped = new ManualResetEventSlim();
        lifetime.ApplicationStopped.Register(stopped.Set);
        _closeHandler = controlType =>
        {
            if (controlType is not (CloseEvent or LogoffEvent or ShutdownEvent))
            {
                return false;
            }

            logger.LogInformation("The console window was closed or Windows is shutting down. Stopping SameieHub.");
            lifetime.StopApplication();
            stopped.Wait(TimeSpan.FromSeconds(CloseWaitSeconds));

            return true;
        };
        SetConsoleCtrlHandler(_closeHandler, true);
    }

    private const uint CloseEvent = 2;
    private const uint LogoffEvent = 5;
    private const uint ShutdownEvent = 6;
    private const int CloseWaitSeconds = 4;

    private delegate bool ConsoleControlHandler(uint controlType);

    // Kept in a field so the garbage collector cannot remove the handler while Windows still has a pointer to it.
    private static ConsoleControlHandler? _closeHandler;

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(ConsoleControlHandler handler, bool add);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr window, string text, string caption, uint type);
}
