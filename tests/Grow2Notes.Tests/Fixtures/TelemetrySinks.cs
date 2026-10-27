using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>Every place the app writes to or sends to, each in memory.</summary>
public sealed class TelemetrySinks
{
    /// <summary>What the app's logging levels let through to its logger providers, OpenTelemetry's too.</summary>
    public RecordingLoggerProvider Logs { get; } = new();

    public SpanCollector Spans { get; } = new();

    public LogCollector ExportedLogs { get; } = new();

    public MetricCollector Metrics { get; } = new();

    /// <summary>What the Azure Monitor exporters send, serialised as Application Insights receives it.</summary>
    public IngestionRecorder Ingestion { get; } = new();

    /// <summary>The errors SQL Server returned to the test-only endpoints.</summary>
    public ConcurrentQueue<SqlException> Failures { get; } = new();

    public IEnumerable<(string Sink, string? Text)> Texts()
    {
        foreach (var entry in Logs.Entries)
        {
            yield return ("log message", entry.Message);
            yield return ("log exception", entry.Exception?.ToString());
            foreach (var value in entry.Values)
            {
                yield return ("log value", Text(value));
            }
        }

        foreach (var log in ExportedLogs.Logs)
        {
            yield return ("exported log body", log.Body);
            yield return ("exported log message", log.FormattedMessage);
            yield return ("exported log exception", log.Exception?.ToString());
            foreach (var attribute in log.Attributes)
            {
                yield return ("exported log attribute", Text(attribute));
            }
        }

        foreach (var span in Spans.Spans)
        {
            yield return ("span name", span.DisplayName);
            yield return ("span status description", span.StatusDescription);
            yield return ("span trace state", span.TraceStateString);
            foreach (var tag in span.TagObjects)
            {
                yield return ("span tag", Text(tag));
            }

            foreach (var (key, value) in span.Baggage)
            {
                yield return ("span baggage", $"{key}={value}");
            }

            foreach (var spanEvent in span.Events)
            {
                yield return ("span event", spanEvent.Name);
                foreach (var tag in spanEvent.Tags)
                {
                    yield return ("span event tag", Text(tag));
                }
            }

            foreach (var tag in span.Links.SelectMany(l => l.Tags ?? []))
            {
                yield return ("span link tag", Text(tag));
            }
        }

        foreach (var tag in Metrics.Points.SelectMany(p => p.Tags))
        {
            yield return ("metric attribute", Text(tag));
        }

        foreach (var payload in Ingestion.Payloads)
        {
            yield return ("sent to Application Insights", payload);
        }
    }

    private static string Text(KeyValuePair<string, object?> pair) =>
        $"{pair.Key}={(pair.Value is IEnumerable values and not string
            ? string.Join(", ", values.Cast<object?>())
            : Convert.ToString(pair.Value, CultureInfo.InvariantCulture))}";
}

/// <summary>Sends everything an app writes or sends to a <see cref="TelemetrySinks"/>.</summary>
public static class TelemetrySinksHostBuilderExtensions
{
    public static IWebHostBuilder RouteEverythingToSinks(this IWebHostBuilder builder, TelemetrySinks sinks)
    {
        builder.UseTelemetrySentTo(sinks.Ingestion);

        // Beside the console and the app's other providers, with no rules of its own, so it receives what they do.
        builder.ConfigureLogging(logging => logging.AddProvider(sinks.Logs));

        return builder.ConfigureServices(services =>
        {
            // After the app's processors, and before the exporters that the distro adds to the built providers when
            // the host starts, so these see what any exporter would. Metrics have no processors: a reader of the
            // collector's own, beside the distro's, is given every point.
            services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(sinks.Spans));
            services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(sinks.ExportedLogs));
            services.ConfigureOpenTelemetryMeterProvider(
                metrics => metrics.AddReader(new BaseExportingMetricReader(sinks.Metrics)));

            // Every trace sampled. The default rate limit can drop the test's requests, and the exporter then sends
            // none of their spans; sampling only ever sends less than this.
            services.PostConfigureAll<AzureMonitorExporterOptions>(options =>
            {
                options.TracesPerSecond = null;
                options.SamplingRatio = 1;
            });
        });
    }
}
