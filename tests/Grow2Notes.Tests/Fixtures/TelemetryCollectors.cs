using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;

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

/// <summary>
/// Copies every metric point it is given. As the exporter of a reader added to a provider, it sees what any other
/// reader's exporter would. It copies because OpenTelemetry reuses the points from one collection to the next.
/// </summary>
public sealed class MetricCollector : BaseExporter<Metric>
{
    private readonly ConcurrentQueue<ExportedMetric> points = new();

    public IReadOnlyCollection<ExportedMetric> Points => points;

    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (var metric in batch)
        {
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                List<KeyValuePair<string, object?>> tags = [];
                foreach (var tag in point.Tags)
                {
                    tags.Add(tag);
                }

                points.Enqueue(new(metric.Name, tags));
            }
        }

        return ExportResult.Success;
    }
}

/// <summary>A metric point as an exporter receives it: the metric's name and the point's attributes.</summary>
public sealed record ExportedMetric(string Name, IReadOnlyList<KeyValuePair<string, object?>> Tags);
