using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging.Abstractions;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>
/// The pipe a second start uses to ask the running program to open the browser. Every test uses its own pipe name.
/// </summary>
public class ActivationPipeTests
{
    private static string NewPipeName() => "SameieHubTest-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void ThePipeNameFollowsTheDatabase_AndTwoDatabasesHaveTwoPipes()
    {
        var first = ActivationPipe.NameFor(Path.Combine(Path.GetTempPath(), "a", "sameiehub.db"));
        var same = ActivationPipe.NameFor(Path.Combine(Path.GetTempPath(), "a", "..", "a", "SAMEIEHUB.db"));
        var other = ActivationPipe.NameFor(Path.Combine(Path.GetTempPath(), "b", "sameiehub.db"));

        Assert.Equal(first, same);
        Assert.NotEqual(first, other);
        Assert.DoesNotContain(Path.GetTempPath(), first);
    }

    [Fact]
    public async Task ASecondStart_AsksTheRunningProgramToOpenTheBrowser_AndGetsAnAnswer()
    {
        var name = NewPipeName();
        var opened = 0;
        using var listener = ActivationListener.TryStart(name, () =>
        {
            Interlocked.Increment(ref opened);

            return Task.CompletedTask;
        }, NullLogger.Instance);

        var result = await ActivationClient.TryActivateAsync(name, TimeSpan.FromSeconds(5));

        Assert.NotNull(listener);
        Assert.True(result);
        Assert.Equal(1, opened);
    }

    [Fact]
    public async Task TwoStartsAtAlmostTheSameTime_BothGetAnAnswer()
    {
        var name = NewPipeName();
        var opened = 0;
        using var listener = ActivationListener.TryStart(name, async () =>
        {
            await Task.Delay(100);
            Interlocked.Increment(ref opened);
        }, NullLogger.Instance);

        var results = await Task.WhenAll(
            ActivationClient.TryActivateAsync(name, TimeSpan.FromSeconds(10)),
            ActivationClient.TryActivateAsync(name, TimeSpan.FromSeconds(10)),
            ActivationClient.TryActivateAsync(name, TimeSpan.FromSeconds(10)));

        Assert.All(results, Assert.True);
        Assert.Equal(3, opened);
    }

    [Theory]
    [InlineData("OPEN")]
    [InlineData("open\n")]
    [InlineData("OPEN C:\\Windows\\notepad.exe\n")]
    [InlineData("SHUTDOWN\n")]
    [InlineData("\n")]
    [InlineData("OPEN OPEN OPEN OPEN OPEN OPEN OPEN OPEN OPEN\n")]
    public async Task AnythingExceptTheOneKnownCommand_IsRefused_AndOpensNothing(string message)
    {
        var name = NewPipeName();
        var opened = 0;
        using var listener = ActivationListener.TryStart(name, () =>
        {
            Interlocked.Increment(ref opened);

            return Task.CompletedTask;
        }, NullLogger.Instance);

        var answer = await SendRawAsync(name, message);

        Assert.NotEqual("OK", answer);
        Assert.Equal(0, opened);
    }

    [Fact]
    public async Task ACommandThatIsNeverFinished_DoesNotBlockTheNextStart()
    {
        var name = NewPipeName();
        var opened = 0;
        using var listener = ActivationListener.TryStart(name, () =>
        {
            Interlocked.Increment(ref opened);

            return Task.CompletedTask;
        }, NullLogger.Instance);
        await using var silent = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await silent.ConnectAsync(5000);

        var result = await ActivationClient.TryActivateAsync(name, TimeSpan.FromSeconds(10));

        Assert.True(result);
        Assert.Equal(1, opened);
    }

    [Fact]
    public async Task WithoutARunningProgram_TheClientGivesUpQuickly()
    {
        var started = DateTime.UtcNow;

        var result = await ActivationClient.TryActivateAsync(NewPipeName(), TimeSpan.FromMilliseconds(500));

        Assert.False(result);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WhenTheProgramStopsWhileASecondStartIsConnecting_TheClientDoesNotHang()
    {
        var name = NewPipeName();
        var listener = ActivationListener.TryStart(name, async () => await Task.Delay(300), NullLogger.Instance);
        var started = DateTime.UtcNow;

        var client = ActivationClient.TryActivateAsync(name, TimeSpan.FromSeconds(3));
        listener!.Dispose();
        _ = await client;

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(8));
        Assert.False(await ActivationClient.TryActivateAsync(name, TimeSpan.FromMilliseconds(300)));
    }

    [Fact]
    public async Task AfterACrash_ANewProgramCanTakeOverThePipe()
    {
        var name = NewPipeName();
        var first = ActivationListener.TryStart(name, () => Task.CompletedTask, NullLogger.Instance);
        first!.Dispose();

        using var second = ActivationListener.TryStart(name, () => Task.CompletedTask, NullLogger.Instance);

        Assert.NotNull(second);
        Assert.True(await ActivationClient.TryActivateAsync(name, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void ThePipeCanOnlyBeUsedByTheCurrentWindowsUser()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var server = ActivationListener.CreateServerStream(NewPipeName());

        var rules = server.GetAccessControl().GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
        var user = WindowsIdentity.GetCurrent().User!;
        var broadGroups = new[]
        {
            WellKnownSidType.WorldSid,
            WellKnownSidType.BuiltinUsersSid,
            WellKnownSidType.AuthenticatedUserSid,
            WellKnownSidType.InteractiveSid,
            WellKnownSidType.NetworkSid,
            WellKnownSidType.AnonymousSid
        };

        Assert.Contains(rules, rule => rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference.Equals(user));
        Assert.DoesNotContain(rules, rule =>
            rule.AccessControlType == AccessControlType.Allow
            && broadGroups.Any(group => ((SecurityIdentifier)rule.IdentityReference).IsWellKnown(group)));
    }

    [Fact]
    public void TheOnlyCommandIsOpen_SoNoCodePathOrProgramTextCanBeSentOverThePipe()
    {
        Assert.Equal("OPEN", ActivationListener.OpenCommand);
    }

    private static async Task<string> SendRawAsync(string name, string message)
    {
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(5000);
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);
        await client.WriteAsync(bytes);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        var buffer = new byte[32];
        try
        {
            var read = await client.ReadAsync(buffer, timeout.Token);

            return System.Text.Encoding.UTF8.GetString(buffer, 0, read).Trim();
        }
        catch (OperationCanceledException)
        {
            return "TIMEOUT";
        }
    }
}
