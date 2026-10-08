using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Azure.Monitor.OpenTelemetry.Exporter;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The canary test of design.md §9.5, its M0 part: a unique string sent in request bodies, and placed in the values
/// that SQL Server puts in its error text, appears in no log, no telemetry and no response. The app runs with telemetry
/// on, as in Azure, and everything it writes or sends goes to a sink here. Note text joins the canary in E02.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TelemetryCanaryTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    // Endpoints that only this test maps. No real endpoint forces a database failure on purpose.
    private const string TruncationPath = "/api/test-only/truncation";
    private const string DuplicateEmailPath = "/api/test-only/duplicate-email";

    private static readonly DateTime TimeUtc = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string Canary = $"canary-{Guid.NewGuid():N}";

    [Fact]
    public async Task A_canary_in_request_bodies_and_SQL_error_text_appears_in_no_log_telemetry_or_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sinks = new Sinks();
        await using var app = factory.WithWebHostBuilder(builder => RouteEverythingToSinks(builder, sinks));
        using var client = app.CreateClient();

        using var truncation = await client.PostAsJsonAsync(
            TruncationPath, new CanaryRequest(Canary), cancellationToken);
        using var duplicate = await client.PostAsJsonAsync(
            DuplicateEmailPath, new CanaryRequest($"{Canary}@example.org"), cancellationToken);
        HttpResponseMessage[] responses = [truncation, duplicate];

        // The app answered each request with a 500 problem, whose body the check below looks in too.
        var bodies = await Task.WhenAll(
            responses.Select(r => r.ReadProblemAsync(HttpStatusCode.InternalServerError)));

        // The test server answers before a request's span ends, and the exporters send on their own schedules.
        Assert.True(
            SpinWait.SpinUntil(() => sinks.Spans.Spans.Count(IsCanaryRequest) == 2, TimeSpan.FromSeconds(10)),
            "The requests' spans did not end.");
        Assert.True(app.Services.GetRequiredService<TracerProvider>().ForceFlush());
        Assert.True(app.Services.GetRequiredService<LoggerProvider>().ForceFlush());
        Assert.True(app.Services.GetRequiredService<MeterProvider>().ForceFlush());

        // SQL Server put the canary in its error text.
        Assert.Equal([2628, 2601], sinks.Failures.Select(f => f.Number));
        Assert.All(sinks.Failures, f => Assert.Contains(Canary, f.Message, StringComparison.OrdinalIgnoreCase));

        // What Application Insights would receive reached this test's recorder, so the check below looks at something.
        Assert.NotEmpty(sinks.Ingestion.Payloads);

        // Ignoring case, because the email index holds the address upper-cased.
        Assert.Empty(sinks.Texts()
            .Concat(responses.SelectMany(ResponseHeaders))
            .Concat(bodies.Select(body => (Sink: "response body", Text: (string?)body)))
            .Where(t => t.Text?.Contains(Canary, StringComparison.OrdinalIgnoreCase) == true)
            .Select(t => $"{t.Sink}: {t.Text}"));

        // Every sink received the failures, so the check above did not pass by seeing nothing.
        var handler = typeof(DatabaseFailureHandler).FullName;
        Assert.Collection(sinks.Logs.Entries.Where(e => e.Category == handler),
            e => Assert.Contains(new("SqlErrorNumber", 2628), e.Values),
            e => Assert.Contains(new("SqlErrorNumber", 2601), e.Values));
        Assert.Collection(sinks.ExportedLogs.Logs.Where(l => l.CategoryName == handler),
            l => Assert.Contains(new("SqlErrorNumber", 2628), l.Attributes),
            l => Assert.Contains(new("SqlErrorNumber", 2601), l.Attributes));
        Assert.All(sinks.Spans.Spans.Where(IsCanaryRequest), request =>
        {
            Assert.Contains(sinks.Spans.Spans, s => s.TraceId == request.TraceId
                && s.GetTagItem("db.system") is not null && s.Status == ActivityStatusCode.Error);
            Assert.Contains(sinks.Metrics.Points, p => p.Name == "http.server.request.duration"
                && p.Tags.Contains(new("http.route", request.GetTagItem("url.path")))
                && p.Tags.Contains(new("http.response.status_code", 500)));
            Assert.Contains(sinks.Ingestion.Items, i => (string?)i["name"] == "RemoteDependency"
                && (string?)i["tags"]?["ai.operation.id"] == request.TraceId.ToHexString()
                && (bool?)i["data"]?["baseData"]?["success"] == false);
        });
    }

    private static void RouteEverythingToSinks(IWebHostBuilder builder, Sinks sinks)
    {
        builder.UseTelemetrySentTo(sinks.Ingestion);

        // Beside the console and the app's other providers, with no rules of its own, so it receives what they do.
        builder.ConfigureLogging(logging => logging.AddProvider(sinks.Logs));

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new TestOnlyEndpoints(sinks.Failures));

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

    private static bool IsCanaryRequest(Activity span) =>
        span.Kind == ActivityKind.Server && span.GetTagItem("url.path") is TruncationPath or DuplicateEmailPath;

    private static IEnumerable<(string Sink, string? Text)> ResponseHeaders(HttpResponseMessage response)
    {
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            yield return ("response header", $"{header.Key}: {string.Join(", ", header.Value)}");
        }
    }

    private sealed record CanaryRequest(string Value);

    /// <summary>Every place the app writes to or sends to, each in memory.</summary>
    private sealed class Sinks
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

    /// <summary>
    /// Maps endpoints that save a request's value where SQL Server refuses it, so the value is in SQL Server's error
    /// text. They are routed ahead of the app's own routing, which then leaves the endpoint already chosen, so they run
    /// where the app's own endpoints do: inside its exception handler, which gives the failure to
    /// <see cref="DatabaseFailureHandler"/>.
    /// </summary>
    private sealed class TestOnlyEndpoints(ConcurrentQueue<SqlException> failures) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints =>
            {
                // One character too long for the column, with the value first: SQL Server's message ends with the value
                // as truncated to the column's size (error 2628).
                endpoints.MapPost(TruncationPath, (CanaryRequest request, Grow2NotesDbContext db,
                    ILookupNormalizer normalizer, CancellationToken cancellationToken) =>
                    AddAccountsAsync(db, normalizer, cancellationToken,
                        (request.Value.PadRight(Limits.DisplayName + 1, 'x'), Email: null)));

                // The unique email index refuses the second account, and its message gives the duplicate key, which is
                // the address upper-cased (error 2601).
                endpoints.MapPost(DuplicateEmailPath, (CanaryRequest request, Grow2NotesDbContext db,
                    ILookupNormalizer normalizer, CancellationToken cancellationToken) =>
                    AddAccountsAsync(db, normalizer, cancellationToken,
                        ("Canary", request.Value), ("Canary", request.Value)));
            });
        };

        /// <summary>
        /// Saves an organisation, then each account in a save of its own, so the failing one is the only statement in
        /// its command. SQL Server's error then arrives as the command runs, rather than while EF Core reads an earlier
        /// statement's results, and SqlClient's instrumentation puts its message in the command span's status
        /// description, which is where DatabaseSpanProcessor removes it from. All in one transaction, which the failure
        /// rolls back, so nothing stays in the shared database.
        /// </summary>
        private async Task AddAccountsAsync(Grow2NotesDbContext db, ILookupNormalizer normalizer,
            CancellationToken cancellationToken, params (string DisplayName, string? Email)[] accounts)
        {
            try
            {
                await db.InTransactionAsync(async ct =>
                {
                    var organisation = new Organisation { Name = "Telemetry canary test", CreatedAtUtc = TimeUtc };
                    db.Organisations.Add(organisation);
                    await db.SaveChangesAsync(ct);

                    foreach (var (displayName, email) in accounts)
                    {
                        db.Users.Add(new()
                        {
                            OrganisationId = organisation.Id,
                            DisplayName = displayName,
                            Role = UserRole.Worker,
                            Status = UserStatus.Invited,
                            InvitedAtUtc = TimeUtc,
                            Email = email,
                            NormalizedEmail = normalizer.NormalizeEmail(email),
                        });
                        await db.SaveChangesAsync(ct);
                    }
                }, cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException failure)
            {
                // Kept so the test can show the canary was in SQL Server's text; the exception goes on to the app.
                failures.Enqueue(failure);
                throw;
            }
        }
    }
}
