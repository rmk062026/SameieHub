namespace SameieHub.Web.Hosting;

/// <summary>
/// The only way to stop the program from the web interface. It exists in every start, but only the Windows mode lets it act: in the
/// development start (<c>dotnet run</c>, the demo, the tests) a request does nothing, so the server cannot be stopped by accident.
/// </summary>
/// <remarks>
/// There is deliberately no web address that stops the program. A request arrives over the page's own Blazor connection, which the
/// access guard has already admitted, so only a browser with a valid session can ask. The program is stopped after a short delay,
/// so the message that says what is happening reaches every open page first.
/// </remarks>
internal sealed class ShutdownService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ShutdownService> _logger;
    private readonly bool _canRestart;
    private readonly TimeSpan _messageDelay;
    private readonly object _gate = new();
    private bool _requested;
    private bool _restartRequested;

    public ShutdownService(IHostApplicationLifetime lifetime, ILogger<ShutdownService> logger, bool canShutDown, bool canRestart, TimeSpan messageDelay)
    {
        _lifetime = lifetime;
        _logger = logger;
        CanShutDown = canShutDown;
        _canRestart = canRestart;
        _messageDelay = messageDelay;
    }

    /// <summary>True in the Windows mode only. When false, the interface shows no way to stop the program.</summary>
    public bool CanShutDown { get; }

    /// <summary>True when the program can also start itself again after it has stopped.</summary>
    public bool CanRestart => CanShutDown && _canRestart;

    public bool IsRequested
    {
        get
        {
            lock (_gate)
            {
                return _requested;
            }
        }
    }

    /// <summary>True when the program is stopping in order to start again. Read after the program has stopped.</summary>
    public bool RestartRequested
    {
        get
        {
            lock (_gate)
            {
                return _restartRequested;
            }
        }
    }

    /// <summary>Raised once, when the first request is accepted. Every open page listens, so they all show the message.</summary>
    public event Action? Requested;

    /// <summary>
    /// Asks the program to stop (and, with <paramref name="restart"/>, to start again afterwards). Returns false when this start does
    /// not allow it, and nothing happens. A request that comes after the first one is accepted and ignored: the program stops once.
    /// </summary>
    public bool Request(bool restart)
    {
        if (!CanShutDown || (restart && !CanRestart))
        {
            return false;
        }

        lock (_gate)
        {
            if (_requested)
            {
                return true;
            }

            _requested = true;
            _restartRequested = restart;
        }

        _logger.LogInformation(restart ? "SameieHub was asked to restart." : "SameieHub was asked to stop.");

        // The stop is scheduled before the pages are told, so a page that fails to handle the message cannot keep the program running.
        _ = Task.Run(StopAfterDelayAsync);
        Requested?.Invoke();

        return true;
    }

    private async Task StopAfterDelayAsync()
    {
        await Task.Delay(_messageDelay);
        _lifetime.StopApplication();
    }
}
