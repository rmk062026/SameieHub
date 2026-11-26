using SameieHub.Web.Hosting;

namespace SameieHub.Tests.Startup;

public class InstanceLockTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SameieHubTests", Guid.NewGuid().ToString());

    public InstanceLockTests()
    {
        Directory.CreateDirectory(_folder);
    }

    private string LockFile => Path.Combine(_folder, "sameiehub.lock");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void TheLockFileLivesNextToTheDatabaseAndIsNamedAfterIt()
    {
        Assert.Equal(Path.Combine(_folder, "sameiehub.lock"), InstanceLock.PathFor(Path.Combine(_folder, "sameiehub.db")));
        Assert.Equal(Path.Combine(_folder, "sameiehub-demo.lock"), InstanceLock.PathFor(Path.Combine(_folder, "sameiehub-demo.db")));
    }

    [Fact]
    public void TheFirstStartGetsTheLock_AndASecondStartAtTheSameTimeDoesNot()
    {
        using var first = InstanceLock.TryAcquire(LockFile);
        using var second = InstanceLock.TryAcquire(LockFile);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void AfterANormalShutdownTheLockCanBeTakenAgain_AndTheFileIsNotDeleted()
    {
        var first = InstanceLock.TryAcquire(LockFile);
        Assert.NotNull(first);
        first.Dispose();

        Assert.True(File.Exists(LockFile));
        using var second = InstanceLock.TryAcquire(LockFile);

        Assert.NotNull(second);
    }

    [Fact]
    public void ALeftOverLockFileFromACrashedProgramDoesNotBlockTheNextStart()
    {
        File.WriteAllText(LockFile, "left behind");

        using var instanceLock = InstanceLock.TryAcquire(LockFile);

        Assert.NotNull(instanceLock);
    }

    [Fact]
    public void TheLockFolderIsCreatedWhenItIsMissing()
    {
        var nested = Path.Combine(_folder, "new", "sameiehub.lock");

        using var instanceLock = InstanceLock.TryAcquire(nested);

        Assert.NotNull(instanceLock);
    }

    [Fact]
    public void DisposingTheLockTwiceIsHarmless()
    {
        var instanceLock = InstanceLock.TryAcquire(LockFile);
        Assert.NotNull(instanceLock);

        instanceLock.Dispose();
        instanceLock.Dispose();

        using var again = InstanceLock.TryAcquire(LockFile);
        Assert.NotNull(again);
    }
}
