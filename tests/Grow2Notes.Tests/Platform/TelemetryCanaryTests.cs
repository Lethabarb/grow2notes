using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The canary test of design.md §9.5, its M0 part: a unique string sent in request bodies, and placed in the values
/// that SQL Server puts in its error text, appears in no log, no telemetry and no response. Since S01.02.02 it is also
/// a participant's given and family names, which the app's own endpoint adds, saving them with their audit event, and
/// since T05 the text of a goal that the app's own endpoint adds to them, saved with its own, and since T08 the Guided
/// notes of their note's first save, which starts the note with its own, and since T09 the reason it flags the note
/// with, and the Guided notes and reason it submits the note with, which writes its own; and, since T24, the note's
/// text and flag reason in no audit entry's Details. The app runs with telemetry on, as in Azure, and everything it
/// writes or sends goes to a sink here.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TelemetryCanaryTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Endpoints that only this test maps. No real endpoint forces a database failure on purpose.
    private const string TruncationPath = "/api/test-only/truncation";
    private const string DuplicateEmailPath = "/api/test-only/duplicate-email";

    // The app's own endpoints that add a participant, and a goal to one, and save and submit their note, as each is
    // routed.
    private const string ParticipantsPath = "/api/admin/participants";
    private const string GoalsRoute = "/api/admin/participants/{participantId:guid}/goals";
    private const string DraftRoute = "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/draft";
    private const string VersionsRoute = "/api/participants/{participantId:guid}/notes/{noteDate:isodate}/versions";

    private static readonly DateTime TimeUtc = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string Canary = $"canary-{Guid.NewGuid():N}";

    // The note's text and its flag's reason, each its own, so a failure names the field that leaked. Both hold Canary,
    // so the checks of the logs, telemetry and responses cover them.
    private static readonly string NarrativeCanary = $"{Canary}-narrative";

    private static readonly string FlagReasonCanary = $"{Canary}-flag-reason";

    [Fact]
    public async Task A_canary_in_request_bodies_and_SQL_error_text_appears_in_no_log_telemetry_or_response()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sinks = new TelemetrySinks();
        await using var app = factory.WithWebHostBuilder(builder => builder
            .RouteEverythingToSinks(sinks)
            .ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new TestOnlyEndpoints(sinks.Failures))));
        using var client = app.CreateClient();

        using var truncation = await client.PostAsJsonAsync(
            TruncationPath, new CanaryRequest(Canary), cancellationToken);
        using var duplicate = await client.PostAsJsonAsync(
            DuplicateEmailPath, new CanaryRequest($"{Canary}@example.org"), cancellationToken);
        HttpResponseMessage[] failures = [truncation, duplicate];

        // As a manager adds one, over HTTPS with a token, as the /api group needs, then a goal of theirs, then opens
        // their note for today and starts it, flagged with a reason, as the form's first save does, with the template's
        // lists' version, then submits it, as Submit does, with a key.
        using var manager = app.CreateHttpsClient().SignInAs(sqlServer.Seeded.A.ManagerId);
        using var added = await manager.PostWithTokenAsync(
            ParticipantsPath,
            await manager.FetchTokenAsync(),
            new { givenName = Canary, familyName = Canary, dateOfBirth = "1987-03-07" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var addedBody = await added.Content.ReadAsStringAsync(cancellationToken);
        using var addedJson = JsonDocument.Parse(addedBody);
        var participantId = addedJson.RootElement.GetProperty("id").GetGuid();
        var goalsPath = $"{ParticipantsPath}/{participantId}/goals";
        using var goalAdded = await manager.PostWithTokenAsync(
            goalsPath, await manager.FetchTokenAsync(), new { text = Canary });
        Assert.Equal(HttpStatusCode.Created, goalAdded.StatusCode);
        var today = app.Services.GetRequiredService<MelbourneClock>().Today();
        var draftPath = string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{today:yyyy-MM-dd}/draft");
        var form = await manager.GetFromJsonAsync<JsonElement>(draftPath, cancellationToken);
        using var noteStarted = await manager.PutWithTokenAsync(
            draftPath,
            await manager.FetchTokenAsync(),
            new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion = form.GetProperty("listsVersion").GetString(),
                narrative = NarrativeCanary,
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = true,
                flagReason = FlagReasonCanary,
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, noteStarted.StatusCode);
        var versionsPath = string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{today:yyyy-MM-dd}/versions");
        using var submit = new HttpRequestMessage(HttpMethod.Post, versionsPath)
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 0,
                narrative = NarrativeCanary,
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = true,
                flagReason = FlagReasonCanary,
            }),
        };
        submit.Headers.Add(AntiforgeryTokens.Header, await manager.FetchTokenAsync());
        submit.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        using var noteSubmitted = await manager.SendAsync(submit, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, noteSubmitted.StatusCode);

        // The note's own entries, which the check asks for by type, so it does not pass by reading nothing. The shared
        // canary is not looked for here: participant.created and goal.created record the names and the goal's text as
        // their new values, by design (A28).
        var noteEntries = await AuditEntriesAboutAsync(app, participantId);
        Assert.Contains(AuditEventTypes.Note.DraftStarted, noteEntries.Select(entry => entry.EventType));
        Assert.Contains(AuditEventTypes.Note.Submitted, noteEntries.Select(entry => entry.EventType));
        Assert.DoesNotContain(noteEntries, entry => HoldsCanary(entry, NarrativeCanary));
        Assert.DoesNotContain(noteEntries, entry => HoldsCanary(entry, FlagReasonCanary));

        // The app answered each failing request with a 500 problem, whose body the check below looks in too, as it does
        // in the added participant's and goal's, and the started and submitted note's.
        var bodies = (await Task.WhenAll(
                failures.Select(r => r.ReadProblemAsync(HttpStatusCode.InternalServerError))))
            .Append(addedBody)
            .Append(await goalAdded.Content.ReadAsStringAsync(cancellationToken))
            .Append(await noteStarted.Content.ReadAsStringAsync(cancellationToken))
            .Append(await noteSubmitted.Content.ReadAsStringAsync(cancellationToken));
        HttpResponseMessage[] responses = [.. failures, added, goalAdded, noteStarted, noteSubmitted];
        (string Method, string Path, string Route)[] saves =
        [
            ("POST", ParticipantsPath, ParticipantsPath),
            ("POST", goalsPath, GoalsRoute),
            ("PUT", draftPath, DraftRoute),
            ("POST", versionsPath, VersionsRoute),
        ];

        // The test server answers before a request's span ends, and the exporters send on their own schedules.
        Assert.True(
            SpinWait.SpinUntil(
                () => sinks.Spans.Spans.Count(IsFailingRequest) == 2
                    && saves.All(save => sinks.Spans.Spans.Any(span => IsRequestTo(span, save.Method, save.Path))),
                TimeSpan.FromSeconds(10)),
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
        Assert.All(sinks.Spans.Spans.Where(IsFailingRequest), request =>
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

        // And the requests that added the participant, whose body held the names, the goal, whose body held its text,
        // and the note's save and submit, whose bodies held its Guided notes and its flag's reason.
        foreach (var (method, path, route) in saves)
        {
            var add = sinks.Spans.Spans.Single(span => IsRequestTo(span, method, path));
            Assert.Contains(sinks.Metrics.Points, p => p.Name == "http.server.request.duration"
                && p.Tags.Contains(new("http.route", route))
                && p.Tags.Contains(new("http.response.status_code", 201)));
            Assert.Contains(sinks.Ingestion.Items, i => (string?)i["name"] == "Request"
                && (string?)i["tags"]?["ai.operation.id"] == add.TraceId.ToHexString());
        }
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private static async Task<List<AuditEntry>> AuditEntriesAboutAsync(
        WebApplicationFactory<Program> app, Guid participantId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<AuditEntry>($"""
                SELECT [EventType], [Details]
                FROM [AuditEvent]
                WHERE [ParticipantId] = {participantId}
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static bool HoldsCanary(AuditEntry entry, string canary) =>
        entry.Details?.Contains(canary, StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsFailingRequest(Activity span) =>
        span.Kind == ActivityKind.Server && span.GetTagItem("url.path") is TruncationPath or DuplicateEmailPath;

    // By method too, as the note's form is read and saved at one path.
    private static bool IsRequestTo(Activity span, string method, string path) =>
        span.Kind == ActivityKind.Server
        && span.GetTagItem("http.request.method") as string == method
        && span.GetTagItem("url.path") as string == path;

    private static IEnumerable<(string Sink, string? Text)> ResponseHeaders(HttpResponseMessage response)
    {
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            yield return ("response header", $"{header.Key}: {string.Join(", ", header.Value)}");
        }
    }

    private sealed record CanaryRequest(string Value);

    private sealed record AuditEntry(string EventType, string? Details);

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
                        (request.Value.PadRight(Limits.DisplayName + 1, 'x'), Email: null)))
                    .AllowAnonymous();

                // The unique email index refuses the second account, and its message gives the duplicate key, which is
                // the address upper-cased (error 2601).
                endpoints.MapPost(DuplicateEmailPath, (CanaryRequest request, Grow2NotesDbContext db,
                    ILookupNormalizer normalizer, CancellationToken cancellationToken) =>
                    AddAccountsAsync(db, normalizer, cancellationToken,
                        ("Canary", request.Value), ("Canary", request.Value)))
                    .AllowAnonymous();
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
