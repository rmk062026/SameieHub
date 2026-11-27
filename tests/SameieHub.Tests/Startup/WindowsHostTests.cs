using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SameieHub.Infrastructure;
using SameieHub.Infrastructure.Persistence;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>
/// Starts a small web application with the same Windows host settings as the program (<see cref="WindowsHost"/>) on a
/// temporary folder. The pages themselves are not part of these tests; the real application is started by <c>StartupTests</c>.
/// </summary>
public class WindowsHostTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubTests", Guid.NewGuid().ToString());
    private readonly string _contentRoot;

    public WindowsHostTests()
    {
        _contentRoot = Path.Combine(_folder, "program");
        Directory.CreateDirectory(_contentRoot);
        DatabasePath = Path.Combine(_folder, "data", "sameiehub.db");
    }

    private string DatabasePath { get; }

    private string LogFolder => Path.Combine(_folder, "data", "logs");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheWindowsModeIsOnlyUsedWhenItIsAskedFor()
    {
        Assert.False(WindowsHost.IsRequested([], null, isPublishedWindowsBuild: false));
        Assert.False(WindowsHost.IsRequested(["--seed-demo"], null, isPublishedWindowsBuild: false));
        Assert.True(WindowsHost.IsRequested(["--windows-host"], null, isPublishedWindowsBuild: false));
        Assert.True(WindowsHost.IsRequested([], "1", isPublishedWindowsBuild: false));
    }

    [Fact]
    public void ThePublishedWindowsBuildUsesTheWindowsModeByItself_ButCanBeSwitchedOff()
    {
        Assert.True(WindowsHost.IsRequested([], null, isPublishedWindowsBuild: true));
        Assert.False(WindowsHost.IsRequested([], "0", isPublishedWindowsBuild: true));
        Assert.True(WindowsHost.IsRequested(["--windows-host"], "0", isPublishedWindowsBuild: true));
    }

    [Fact]
    public void TheDevelopmentBuildIsNotMarkedAsAPublishedWindowsBuild()
    {
        Assert.False(WindowsHost.IsPublishedWindowsBuild(typeof(Program).Assembly));
    }

    [Fact]
    public async Task TheSettingsAreReadFromTheProgramFolder_NotFromTheWorkingFolder()
    {
        await File.WriteAllTextAsync(Path.Combine(_contentRoot, "appsettings.json"), "{ \"Marker\": \"from-the-program-folder\" }");
        var otherWorkingFolder = Path.Combine(_folder, "elsewhere");
        Directory.CreateDirectory(otherWorkingFolder);
        Assert.NotEqual(Path.GetFullPath(otherWorkingFolder), Path.GetFullPath(Environment.CurrentDirectory));

        var builder = CreateBuilder();

        Assert.Equal(_contentRoot, builder.Environment.ContentRootPath.TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal("from-the-program-folder", builder.Configuration["Marker"]);
    }

    [Fact]
    public void TheShutdownWaitsAtMostAFewSeconds_SoAnOpenBrowserTabCannotKeepTheProgramRunning()
    {
        var builder = CreateBuilder();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<HostOptions>>().Value;

        Assert.True(options.ShutdownTimeout <= TimeSpan.FromSeconds(4), options.ShutdownTimeout.ToString());
    }

    [Fact]
    public async Task TheServerListensOnLoopbackOnly_OnAPortTheSystemChose()
    {
        await using var app = await StartAppAsync();

        var address = new Uri(Addresses(app).Single());

        Assert.Equal("127.0.0.1", address.Host);
        Assert.True(address.Port > 0);
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Where(endpoint => endpoint.Port == address.Port).ToList();
        Assert.NotEmpty(listeners);
        Assert.All(listeners, endpoint => Assert.Equal(IPAddress.Loopback, endpoint.Address));
    }

    [Fact]
    public async Task TheServerCannotBeReachedOnAnAddressOtherThanLoopback()
    {
        await using var app = await StartAppAsync();
        var port = new Uri(Addresses(app).Single()).Port;
        var otherAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .ToList();

        foreach (var address in otherAddresses)
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAnyAsync<Exception>(async () => await client.ConnectAsync(address, port, timeout.Token));
        }

        using var ipv6 = new TcpClient(AddressFamily.InterNetworkV6);
        if (Socket.OSSupportsIPv6)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await Assert.ThrowsAnyAsync<Exception>(async () => await ipv6.ConnectAsync(IPAddress.IPv6Loopback, port, timeout.Token));
        }
    }

    [Fact]
    public async Task ARequestToTheRightHostIsAnswered_AndForeignHostNamesAreRefused()
    {
        await using var app = await StartAppAsync();
        var address = Addresses(app).Single();
        using var client = new HttpClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(address)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(address.Replace("127.0.0.1", "localhost"))).StatusCode);
        foreach (var foreignHost in new[] { "evil.example", "evil.example:80", "127.0.0.1.evil.example", "192.168.1.20", "[::1]" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.Host = foreignHost;
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task AtShutdown_NoDatabaseFileIsLeftOpen_AndTheWalFileIsGone()
    {
        var builder = CreateBuilder();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSameieHubInfrastructure(DatabasePath);
        var app = builder.Build();
        await app.Services.MigrateSameieHubDatabaseAsync();
        WindowsHost.RegisterShutdown(app.Services.GetRequiredService<IHostApplicationLifetime>(), app.Logger);
        await app.StartAsync();
        var factory = app.Services.GetRequiredService<IDbContextFactory<SameieHubDbContext>>();
        await using (var context = await factory.CreateDbContextAsync())
        {
            Assert.Equal(0, await context.HousingUnits.CountAsync());
        }

        await app.StopAsync();
        await app.DisposeAsync();

        await using var exclusive = new FileStream(DatabasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.False(File.Exists(DatabasePath + "-wal"));
    }

    [Fact]
    public async Task TheLogFile_HasOurOwnMessages_ButNoDatabaseCommandsAndNoMessagesFromExceptions()
    {
        await using var app = await StartAppAsync();
        var logger = app.Services.GetRequiredService<ILoggerFactory>();

        logger.CreateLogger("SameieHub.Web").LogInformation("Our own message");
        logger.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command")
            .LogInformation("INSERT INTO HousingUnits (OwnerName) VALUES ('Fiktiv Eier')");
        logger.CreateLogger("Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost")
            .LogError(new InvalidOperationException("Fiktiv Eier fiktiv@example.test 555 00 000"), "Unhandled exception in circuit");

        var text = ReadAllLogText();
        Assert.Contains("Our own message", text);
        Assert.Contains("Unhandled exception in circuit", text);
        Assert.Contains(nameof(InvalidOperationException), text);
        Assert.DoesNotContain("Fiktiv Eier", text);
        Assert.DoesNotContain("fiktiv@example.test", text);
        Assert.DoesNotContain("INSERT INTO", text);
        Assert.DoesNotContain(_folder, text);
    }

    [Fact]
    public async Task TheLogFolder_KeepsOnlyTheNewestFiles_AndEachFileHasASizeLimit()
    {
        Directory.CreateDirectory(LogFolder);
        for (var day = 1; day <= 20; day++)
        {
            await File.WriteAllTextAsync(Path.Combine(LogFolder, $"sameiehub-2026-10-{day:00}.log"), "old");
        }

        var time = new FixedTimeProvider(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
        var sink = new FileLogSink(LogFolder, time, maxBytesPerFile: 300, maxFiles: 5);
        for (var number = 0; number < 100; number++)
        {
            sink.Write($"line {number} with some text to fill the file");
        }

        var files = Directory.GetFiles(LogFolder, "sameiehub-*.log");
        Assert.True(files.Length <= 5);
        Assert.Contains(files, file => file.EndsWith("sameiehub-2026-11-26.log"));
        Assert.All(files, file => Assert.True(new FileInfo(file).Length < 300 + 200, file));
        Assert.DoesNotContain(files, file => file.EndsWith("sameiehub-2026-10-01.log"));
    }

    [Fact]
    public void ALogLineIsOneLine_EvenWhenTheMessageHasLineBreaks()
    {
        var lines = new List<string>();
        var provider = new SafeLoggerProvider(lines.Add, new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        provider.CreateLogger("SameieHub.Test").LogWarning("first\r\nsecond\nthird");

        Assert.Single(lines);
        Assert.DoesNotContain('\n', lines[0].TrimEnd());
        Assert.DoesNotContain('\r', lines[0].TrimEnd());
    }

    private WebApplicationBuilder CreateBuilder()
    {
        return WindowsHost.CreateBuilder([$"--Database:Path={DatabasePath}"], _contentRoot);
    }

    private async Task<WebApplication> StartAppAsync()
    {
        var builder = CreateBuilder();
        var app = builder.Build();
        app.MapGet("/", () => "ok");
        await app.StartAsync();

        return app;
    }

    private static List<string> Addresses(WebApplication app)
    {
        return app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.ToList();
    }

    private string ReadAllLogText()
    {
        return string.Concat(Directory.GetFiles(LogFolder, "*.log").Select(File.ReadAllText));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
