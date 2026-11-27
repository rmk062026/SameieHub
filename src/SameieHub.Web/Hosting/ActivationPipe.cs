using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace SameieHub.Web.Hosting;

/// <summary>
/// How a second start of the program reaches the one that is already running. The second start connects to a named pipe and says
/// <c>OPEN</c>; the running program then opens the browser itself, with a launch code it made itself. No code, path or program text
/// ever travels through the pipe, and the commands that exist are <c>OPEN</c>, <c>STOP</c> (the installer) and <c>RESTART</c> (test builds only).
/// </summary>
internal static class ActivationPipe
{
    /// <summary>One pipe per database, so the demo and the ordinary program do not mix. The name is not secret: the access control is.</summary>
    public static string NameFor(string databasePath)
    {
        var normalized = Path.GetFullPath(databasePath).ToLowerInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];

        return $"SameieHub.Activation.{hash}";
    }
}

/// <summary>
/// The running program's end of the pipe. The pipe is created with <see cref="PipeOptions.CurrentUserOnly"/>, which gives access to the
/// current Windows user only (checked in a test), so another user on the computer cannot connect. Connections are handled one at a time,
/// each with a short time limit, so a client that sends nothing cannot block the program.
/// </summary>
internal sealed class ActivationListener : IDisposable
{
    public const string OpenCommand = "OPEN";
    public const string StopCommand = "STOP";

    /// <summary>Only answered when the program offers a restart, which only the test build does. It lets the package test check a restart without a browser.</summary>
    public const string RestartCommand = "RESTART";

    private const int MaxMessageBytes = 16;
    private const int MaxServerInstances = 4;

    // Without a buffer, a write does not finish until the other side reads it, and two sides that both write first wait for each other forever.
    private const int BufferBytes = 4096;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(2);

    private readonly string _name;
    private readonly Func<Task> _onOpen;
    private readonly Func<Task>? _onStop;
    private readonly Func<Task>? _onRestart;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private Task _loop = Task.CompletedTask;

    private ActivationListener(string name, Func<Task> onOpen, Func<Task>? onStop, Func<Task>? onRestart, ILogger logger)
    {
        _name = name;
        _onOpen = onOpen;
        _onStop = onStop;
        _onRestart = onRestart;
        _logger = logger;
    }

    /// <summary>
    /// Starts listening, or returns null when the pipe cannot be created (for example because another user already holds the name).
    /// The program then runs without this convenience, and a second start shows the message that the program already runs.
    /// </summary>
    public static ActivationListener? TryStart(string name, Func<Task> onOpen, ILogger logger, Func<Task>? onStop = null, Func<Task>? onRestart = null)
    {
        NamedPipeServerStream first;
        try
        {
            first = CreateServerStream(name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("A second start cannot ask this program to open the browser ({ExceptionType}).", exception.GetType().Name);

            return null;
        }

        var listener = new ActivationListener(name, onOpen, onStop, onRestart, logger);
        listener._loop = Task.Run(() => listener.RunAsync(first));

        return listener;
    }

    internal static NamedPipeServerStream CreateServerStream(string name)
    {
        return new NamedPipeServerStream(
            name,
            PipeDirection.InOut,
            MaxServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            BufferBytes,
            BufferBytes);
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // The loop ends by cancellation. Nothing else can be done about it while shutting down.
        }

        _stop.Dispose();
    }

    private async Task RunAsync(NamedPipeServerStream first)
    {
        var current = first;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                NamedPipeServerStream? next = null;
                try
                {
                    await current.WaitForConnectionAsync(_stop.Token);

                    // The next pipe exists before this one is handled, so there is never a moment without a pipe to connect to.
                    next = CreateServerStream(_name);
                    await HandleAsync(current, _stop.Token);
                }
                catch (OperationCanceledException)
                {
                    next?.Dispose();

                    return;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning("A request to open the browser failed ({ExceptionType}).", exception.GetType().Name);
                    next ??= TryCreateServerStream();
                }

                current.Dispose();
                if (next == null)
                {
                    return;
                }

                current = next;
            }
        }
        finally
        {
            // Also when the listener was stopped before the loop began: the pipe must never be left behind.
            current.Dispose();
        }
    }

    private NamedPipeServerStream? TryCreateServerStream()
    {
        try
        {
            return CreateServerStream(_name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("The pipe for a second start could not be created again ({ExceptionType}).", exception.GetType().Name);

            return null;
        }
    }

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken cancellation)
    {
        var message = await ReadMessageAsync(pipe, cancellation);
        if (message == null)
        {
            return;
        }

        if (message == OpenCommand + "\n")
        {
            await RunCommandAsync(pipe, _onOpen, "open the browser", cancellation);
        }
        else if (message == StopCommand + "\n" && _onStop != null)
        {
            // The answer is sent before the program starts to stop, so the one who asked knows the request was accepted.
            await RunCommandAsync(pipe, () => Task.CompletedTask, "stop", cancellation);
            await _onStop();
        }
        else if (message == RestartCommand + "\n" && _onRestart != null)
        {
            await RunCommandAsync(pipe, _onRestart, "restart", cancellation);
        }
        else
        {
            await ReplyAsync(pipe, "ERR\n", cancellation);
        }
    }

    private async Task RunCommandAsync(NamedPipeServerStream pipe, Func<Task> action, string what, CancellationToken cancellation)
    {
        try
        {
            await action();
            await ReplyAsync(pipe, "OK\n", cancellation);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever went wrong, the one who asked must still get an answer and the program must keep running.
            _logger.LogWarning("The request to {Action} failed ({ExceptionType}).", what, exception.GetType().Name);
            await ReplyAsync(pipe, "ERR\n", cancellation);
        }
    }

    /// <summary>Reads at most one short line. Returns null when the client was too slow, and a text that cannot be the command when it was too long.</summary>
    private static async Task<string?> ReadMessageAsync(NamedPipeServerStream pipe, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(ReadTimeout);
        var buffer = new byte[MaxMessageBytes];
        var length = 0;
        try
        {
            while (length < buffer.Length)
            {
                var read = await pipe.ReadAsync(buffer.AsMemory(length), timeout.Token);
                if (read == 0)
                {
                    break;
                }

                length += read;
                if (buffer[length - 1] == (byte)'\n')
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            return null;
        }

        return Encoding.ASCII.GetString(buffer, 0, length);
    }

    /// <summary>
    /// Writes the answer, then waits (for a short time) until the client has closed its end, which it does after reading the answer.
    /// Anything more the client sent is read and thrown away. Closing at once could lose the answer, and waiting for the client to read
    /// it with a blocking call could hang forever if the client is itself waiting for the program to read.
    /// </summary>
    private static async Task ReplyAsync(NamedPipeServerStream pipe, string text, CancellationToken cancellation)
    {
        await pipe.WriteAsync(Encoding.ASCII.GetBytes(text), cancellation);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(ReadTimeout);
        var discard = new byte[64];
        try
        {
            while (await pipe.ReadAsync(discard, timeout.Token) > 0)
            {
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            // The client did not close in time. The answer has been written, so nothing more is waited for.
        }
    }
}

/// <summary>The second start's end of the pipe.</summary>
internal static class ActivationClient
{
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Asks the running program to open the browser. False means nobody answered in time, and the caller shows the "already running" message.</summary>
    public static Task<bool> TryActivateAsync(string pipeName, TimeSpan connectTimeout)
    {
        return SendAsync(pipeName, ActivationListener.OpenCommand, connectTimeout);
    }

    /// <summary>Asks the running program to shut down in the normal, orderly way. Used by the installer. False means nobody accepted it.</summary>
    public static Task<bool> TryStopAsync(string pipeName, TimeSpan connectTimeout)
    {
        return SendAsync(pipeName, ActivationListener.StopCommand, connectTimeout);
    }

    /// <summary>Asks the running program to stop and start again. Only a test build answers. False means nobody accepted it.</summary>
    public static Task<bool> TryRestartAsync(string pipeName, TimeSpan connectTimeout)
    {
        return SendAsync(pipeName, ActivationListener.RestartCommand, connectTimeout);
    }

    private static async Task<bool> SendAsync(string pipeName, string command, TimeSpan connectTimeout)
    {
        // CurrentUserOnly also makes the client check that the pipe belongs to the current user, so a pipe made by someone else is not trusted.
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync((int)connectTimeout.TotalMilliseconds);
            await client.WriteAsync(Encoding.ASCII.GetBytes(command + "\n"));

            using var timeout = new CancellationTokenSource(AnswerTimeout);
            var buffer = new byte[8];
            var read = await client.ReadAsync(buffer, timeout.Token);

            return Encoding.ASCII.GetString(buffer, 0, read) == "OK\n";
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return false;
        }
    }
}
