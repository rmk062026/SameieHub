using Microsoft.Extensions.Hosting;

namespace SameieHub.Tests.Fakes;

/// <summary>Remembers that the program was told to stop, instead of stopping the test run.</summary>
public sealed class FakeHostLifetime : IHostApplicationLifetime
{
    private readonly TaskCompletionSource _stopRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _stopCalls;

    public int StopCalls => Volatile.Read(ref _stopCalls);

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => CancellationToken.None;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication()
    {
        Interlocked.Increment(ref _stopCalls);
        _stopRequested.TrySetResult();
    }

    /// <summary>Waits until StopApplication has been called. Returns false when it was not called in time.</summary>
    public async Task<bool> WaitForStopAsync(TimeSpan timeout)
    {
        return await Task.WhenAny(_stopRequested.Task, Task.Delay(timeout)) == _stopRequested.Task;
    }
}
