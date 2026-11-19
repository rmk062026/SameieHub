using Microsoft.Extensions.Logging;

namespace SameieHub.Tests.Fakes;

/// <summary>
/// Collects everything that is logged, including exception messages, so tests can check that no user text is logged.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(_messages);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _messages;

        public CapturingLogger(List<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception) + " " + exception);
        }
    }
}
