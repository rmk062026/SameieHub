using System.Diagnostics;
using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>How a restarted program is started, and how it waits for the one that stopped. The processes themselves are tested in the package test.</summary>
public class RestartLaunchTests
{
    [Fact]
    public void TheNewProgramGetsTheSameArguments_AndTheNumberOfTheProgramThatIsStopping()
    {
        var arguments = RestartLaunch.BuildArguments(["--Database:Path=C:\\test\\sameiehub.db", "--unattended"], 4242);

        Assert.Equal(["--Database:Path=C:\\test\\sameiehub.db", "--unattended", "--restarted-after=4242"], arguments);
    }

    [Fact]
    public void AnEarlierRestartMarkIsReplaced_SoTheArgumentsNeverPileUp()
    {
        var arguments = RestartLaunch.BuildArguments(["--restarted-after=1", "--unattended"], 2);

        Assert.Equal(["--unattended", "--restarted-after=2"], arguments);
    }

    [Theory]
    [InlineData("--restarted-after=4242", 4242)]
    [InlineData("--restarted-after=1", 1)]
    public void TheProcessNumberIsReadFromTheArguments(string argument, int expected)
    {
        Assert.Equal(expected, RestartLaunch.GetPreviousProcessId(["--unattended", argument]));
    }

    [Theory]
    [InlineData("--restarted-after=")]
    [InlineData("--restarted-after=abc")]
    [InlineData("--restarted-after=0")]
    [InlineData("--restarted-after=-5")]
    [InlineData("--restarted-after=99999999999")]
    [InlineData("--restarted-after=12 34")]
    public void ANumberThatCannotBeAProcess_IsIgnored(string argument)
    {
        Assert.Null(RestartLaunch.GetPreviousProcessId([argument]));
    }

    [Fact]
    public void WithoutTheMark_ThisIsAnOrdinaryStart()
    {
        Assert.Null(RestartLaunch.GetPreviousProcessId(["--unattended"]));
        Assert.Null(RestartLaunch.GetPreviousProcessId([]));
    }

    [Theory]
    [InlineData("C:\\Users\\test\\AppData\\Local\\Programs\\SameieHub\\SameieHub.exe", true)]
    [InlineData("c:\\x\\sameiehub.EXE", true)]
    [InlineData("C:\\Program Files\\dotnet\\dotnet.exe", false)]
    [InlineData("C:\\x\\testhost.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyTheProgramFileItselfCanStartAgain_NotTheDotnetToolOrTheTestHost(string? processPath, bool expected)
    {
        Assert.Equal(expected, RestartLaunch.CanStartItself(processPath));
    }

    [Fact]
    public void WaitingForAProcessThatDoesNotExist_ReturnsAtOnce()
    {
        var started = Stopwatch.StartNew();

        var gone = RestartLaunch.WaitForExit(int.MaxValue - 1, TimeSpan.FromSeconds(10));

        Assert.True(gone);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void WaitingForAProcessThatIsStillRunning_GivesUpAfterTheTimeLimit()
    {
        var gone = RestartLaunch.WaitForExit(Environment.ProcessId, TimeSpan.FromMilliseconds(300));

        Assert.False(gone);
    }

    [Fact]
    public void WaitingForAProcessThatEnds_ReturnsWhenItHasEnded()
    {
        using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 2 127.0.0.1 >nul") { CreateNoWindow = true, UseShellExecute = false })!;

        var gone = RestartLaunch.WaitForExit(child.Id, TimeSpan.FromSeconds(20));

        Assert.True(gone);
        Assert.True(child.HasExited);
    }

    [Fact]
    public void AProgramThatCannotBeStarted_ReportsFailureInsteadOfThrowing()
    {
        var started = RestartLaunch.TryStart("C:\\finnes\\ikke\\SameieHub.exe", ["--unattended"], Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        Assert.False(started);
    }
}
