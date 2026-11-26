using System.Text;
using Microsoft.Extensions.Logging;

namespace SameieHub.Web.Hosting;

/// <summary>
/// A small logger for the Windows host. Each entry is one line: time, level, category and message. An exception is
/// written as its type name only, because the message of an exception from a library can contain data from a form or
/// the database. The same lines go to the console window and to the log file.
/// </summary>
internal sealed class SafeLoggerProvider : ILoggerProvider
{
    private readonly Action<string> _write;
    private readonly TimeProvider _timeProvider;

    public SafeLoggerProvider(Action<string> write, TimeProvider timeProvider)
    {
        _write = write;
        _timeProvider = timeProvider;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SafeLogger(categoryName, _write, _timeProvider);
    }

    public void Dispose()
    {
    }

    private sealed class SafeLogger : ILogger
    {
        private readonly string _category;
        private readonly Action<string> _write;
        private readonly TimeProvider _timeProvider;

        public SafeLogger(string category, Action<string> write, TimeProvider timeProvider)
        {
            _category = category;
            _write = write;
            _timeProvider = timeProvider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = new StringBuilder()
                .Append(_timeProvider.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))
                .Append(" [").Append(logLevel).Append("] ")
                .Append(_category).Append(": ")
                .Append(OneLine(formatter(state, null)));

            if (exception != null)
            {
                line.Append(" | exception: ").Append(TypeNames(exception));
            }

            _write(line.ToString());
        }

        private static string OneLine(string text)
        {
            return text.Replace("\r", " ").Replace("\n", " ");
        }

        private static string TypeNames(Exception exception)
        {
            var names = new List<string>();
            for (var current = exception; current != null && names.Count < 5; current = current.InnerException)
            {
                names.Add(current.GetType().Name);
            }

            return string.Join(" > ", names);
        }
    }
}

/// <summary>
/// Writes lines to one file per day (<c>sameiehub-yyyy-MM-dd.log</c>). Each file stops growing at a size limit and only the
/// newest files are kept, so the folder cannot grow without limit.
/// </summary>
internal sealed class FileLogSink
{
    private const string FilePrefix = "sameiehub-";
    private const string FileExtension = ".log";

    private readonly string _folder;
    private readonly TimeProvider _timeProvider;
    private readonly long _maxBytesPerFile;
    private readonly int _maxFiles;
    private readonly object _lock = new();
    private string? _currentFile;
    private bool _isFull;

    public FileLogSink(string folder, TimeProvider timeProvider, long maxBytesPerFile = 1_000_000, int maxFiles = 14)
    {
        _folder = folder;
        _timeProvider = timeProvider;
        _maxBytesPerFile = maxBytesPerFile;
        _maxFiles = maxFiles;
    }

    public void Write(string line)
    {
        lock (_lock)
        {
            // A problem with the log must never stop the program or hide the real error, so it is only skipped.
            try
            {
                WriteLine(line);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private void WriteLine(string line)
    {
        var file = Path.Combine(_folder, $"{FilePrefix}{_timeProvider.GetUtcNow().UtcDateTime:yyyy-MM-dd}{FileExtension}");
        if (file != _currentFile)
        {
            Directory.CreateDirectory(_folder);
            _currentFile = file;
            _isFull = false;
            DeleteOldFiles(file);
        }

        if (_isFull)
        {
            return;
        }

        var size = File.Exists(file) ? new FileInfo(file).Length : 0;
        if (size >= _maxBytesPerFile)
        {
            _isFull = true;
            File.AppendAllText(file, "The log file is full. Nothing more is logged today." + Environment.NewLine);

            return;
        }

        File.AppendAllText(file, line + Environment.NewLine);
    }

    private void DeleteOldFiles(string currentFile)
    {
        var oldFiles = Directory.GetFiles(_folder, FilePrefix + "*" + FileExtension)
            .Where(file => file != currentFile)
            .OrderByDescending(file => file, StringComparer.Ordinal)
            .Skip(_maxFiles - 1);

        foreach (var file in oldFiles)
        {
            File.Delete(file);
        }
    }
}
