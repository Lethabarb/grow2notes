using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Keeps every span that reaches it. Added to a provider after the app's processors, it sees what an exporter would.
/// </summary>
public sealed class SpanCollector : BaseProcessor<Activity>
{
    private readonly ConcurrentQueue<Activity> spans = new();

    public IReadOnlyCollection<Activity> Spans => spans;

    public override void OnEnd(Activity data) => spans.Enqueue(data);
}

/// <summary>
/// Copies every log entry that reaches it. Added to a provider after the app's processors, it sees what an exporter
/// would. It copies because OpenTelemetry reuses a <see cref="LogRecord"/> once every processor has seen it.
/// </summary>
public sealed class LogCollector : BaseProcessor<LogRecord>
{
    private readonly ConcurrentQueue<ExportedLog> logs = new();

    public IReadOnlyCollection<ExportedLog> Logs => logs;

    public override void OnEnd(LogRecord data) => logs.Enqueue(
        new(data.CategoryName, data.Exception, data.FormattedMessage, data.Body, [.. data.Attributes ?? []]));
}

/// <summary>The parts of a <see cref="LogRecord"/> that an exporter sends.</summary>
public sealed record ExportedLog(
    string? CategoryName, Exception? Exception, string? FormattedMessage, string? Body,
    IReadOnlyList<KeyValuePair<string, object?>> Attributes);
