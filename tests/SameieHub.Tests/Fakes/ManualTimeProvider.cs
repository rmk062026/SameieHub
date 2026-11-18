namespace SameieHub.Tests.Fakes;

/// <summary>
/// A clock the test controls. It only moves when <see cref="Advance"/> is called.
/// </summary>
public class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTime utcNow)
    {
        _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void Advance(TimeSpan duration)
    {
        _utcNow += duration;
    }
}
