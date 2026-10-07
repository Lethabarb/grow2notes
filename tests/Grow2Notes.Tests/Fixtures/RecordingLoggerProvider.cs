using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Records every log entry that reaches it. It has no rules of its own, so in an app it receives exactly what the
/// app's logging levels let through to the console and to telemetry.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<RecordedLog> entries = new();

    public IReadOnlyCollection<RecordedLog> Entries => entries;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<RecordedLog> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new(category, logLevel, formatter(state, exception), exception,
                state is IReadOnlyList<KeyValuePair<string, object?>> values ? [.. values] : []));
    }
}

/// <summary>A log entry as a logger provider receives it, with the values the message was formatted from.</summary>
public sealed record RecordedLog(
    string Category, LogLevel Level, string Message, Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> Values);
