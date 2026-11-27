using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Tests.Fakes;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>
/// The one place that can stop the program from the web interface. It stops only in Windows mode, only once, and only after the
/// message has had time to reach the browser.
/// </summary>
public class ShutdownServiceTests
{
    private static readonly TimeSpan MessageDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly FakeHostLifetime _lifetime = new();

    private ShutdownService Create(bool canShutDown = true, bool canRestart = true, TimeSpan? delay = null)
    {
        return new ShutdownService(_lifetime, NullLogger<ShutdownService>.Instance, canShutDown, canRestart, delay ?? TimeSpan.Zero);
    }

    [Fact]
    public async Task ARequest_StopsTheProgram_AndTellsTheBrowsersFirst()
    {
        var service = Create();
        var told = 0;
        service.Requested += () => told++;

        var accepted = service.Request(restart: false);

        Assert.True(accepted);
        Assert.True(service.IsRequested);
        Assert.False(service.RestartRequested);
        Assert.Equal(1, told);
        Assert.True(await _lifetime.WaitForStopAsync(Wait));
    }

    [Fact]
    public async Task TheProgramIsNotStopped_BeforeTheMessageHasHadTimeToReachTheBrowser()
    {
        var service = Create(delay: MessageDelay);

        service.Request(restart: false);

        Assert.True(service.IsRequested);
        Assert.Equal(0, _lifetime.StopCalls);
        Assert.True(await _lifetime.WaitForStopAsync(Wait));
        Assert.Equal(1, _lifetime.StopCalls);
    }

    [Fact]
    public async Task ARequestInDevelopmentMode_DoesNothing()
    {
        var service = Create(canShutDown: false, canRestart: false);
        var told = 0;
        service.Requested += () => told++;

        Assert.False(service.CanShutDown);
        Assert.False(service.Request(restart: false));
        Assert.False(service.Request(restart: true));

        Assert.False(service.IsRequested);
        Assert.Equal(0, told);
        Assert.False(await _lifetime.WaitForStopAsync(TimeSpan.FromMilliseconds(300)));
        Assert.Equal(0, _lifetime.StopCalls);
    }

    [Fact]
    public async Task ARestartIsRefused_WhenTheProgramCannotStartItself_AndNothingIsStopped()
    {
        var service = Create(canRestart: false);

        Assert.False(service.CanRestart);
        Assert.False(service.Request(restart: true));

        Assert.False(service.IsRequested);
        Assert.False(await _lifetime.WaitForStopAsync(TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public async Task ARestartRequest_IsRememberedSoTheProgramStartsAgainAfterItHasStopped()
    {
        var service = Create();

        Assert.True(service.Request(restart: true));

        Assert.True(service.RestartRequested);
        Assert.True(await _lifetime.WaitForStopAsync(Wait));
    }

    [Fact]
    public async Task RequestingManyTimes_StopsOnlyOnce_AndTheFirstRequestDecidesIfItIsARestart()
    {
        var service = Create(delay: TimeSpan.FromMilliseconds(50));
        var told = 0;
        service.Requested += () => Interlocked.Increment(ref told);

        var results = Enumerable.Range(0, 10)
            .Select(index => Task.Run(() => service.Request(restart: index == 0)))
            .ToList();
        await Task.WhenAll(results);
        await _lifetime.WaitForStopAsync(Wait);
        await Task.Delay(200);

        Assert.All(results, result => Assert.True(result.Result));
        Assert.Equal(1, told);
        Assert.Equal(1, _lifetime.StopCalls);
    }

    [Fact]
    public async Task AFailingListener_DoesNotKeepTheProgramFromStopping()
    {
        var service = Create();
        service.Requested += () => throw new InvalidOperationException("The page is gone.");

        Assert.Throws<InvalidOperationException>(() => service.Request(restart: false));

        Assert.True(await _lifetime.WaitForStopAsync(Wait));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void OnlyTheWindowsStart_RegistersAServiceThatMayStopTheProgram(bool windowsHost, bool expectedCanShutDown)
    {
        var builder = WebApplication.CreateBuilder();
        WebAppSetup.AddServices(builder, Path.Combine(Path.GetTempPath(), "SameieHubTests-unused", "sameiehub.db"), windowsHost);
        using var app = builder.Build();

        var service = app.Services.GetRequiredService<ShutdownService>();

        Assert.Equal(expectedCanShutDown, service.CanShutDown);
        Assert.False(service.CanRestart, "The test host is not SameieHub.exe, so it cannot start itself again.");
        Assert.Equal(expectedCanShutDown, service.Request(restart: false));
        if (!expectedCanShutDown)
        {
            Assert.False(service.IsRequested);
        }
    }
}
