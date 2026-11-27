using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

/// <summary>
/// The two named mutexes the installer and the program use to see each other: the program holds one while it runs, so an update or
/// an uninstall can see that it is in use, and the installer holds one while it works, so the program does not start in the middle of it.
/// </summary>
public class InstallerSignalsTests
{
    private static string NewName() => "SameieHubTest-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void TheNamesAreTheOnesTheInstallerScriptUses()
    {
        Assert.Equal("SameieHub.Running", RunningMarker.DefaultName);
        Assert.Equal("SameieHubSetup", SetupInProgress.DefaultName);
    }

    [Fact]
    public void WhileTheProgramRuns_TheRunningMarkerExists_AndAfterwardsItDoesNot()
    {
        var name = NewName();
        Assert.False(RunningMarker.IsHeld(name));

        using (var marker = RunningMarker.Create(name))
        {
            Assert.NotNull(marker);
            Assert.True(RunningMarker.IsHeld(name));
        }

        Assert.False(RunningMarker.IsHeld(name));
    }

    [Fact]
    public void TheProgramDoesNotStart_WhileTheInstallerHoldsItsMutex()
    {
        var name = NewName();
        Assert.False(SetupInProgress.IsActive(name));

        using var setupMutex = new Mutex(initiallyOwned: false, name);

        Assert.True(SetupInProgress.IsActive(name));
    }

    [Fact]
    public void ARunningMarkerThatCannotBeCreated_DoesNotStopTheProgram()
    {
        var name = NewName();
        using var first = RunningMarker.Create(name);

        using var second = RunningMarker.Create(name);

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    [Fact]
    public void TheSetupMessage_IsInNorwegian()
    {
        Assert.Contains("installasjon", StartupMessages.For(StartupFailureReason.SetupInProgress).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("127.0.0.1", StartupMessages.For(StartupFailureReason.UnsafeNetworkBinding).Message);
    }
}
