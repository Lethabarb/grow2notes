using System.Diagnostics;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The canary of the MCP tools (mcp-server.md §6.3 rule 3, §10 items 2 and 11, S07.02.07), the M0 canary of
/// <c>TelemetryCanaryTests</c> extended to assistant calls: the app runs with its telemetry on, as in Azure, with
/// everything it logs, traces, measures and sends going to in-memory sinks at the app's own log levels, and every tool
/// is called with its valid arguments and with each of its error cases. No note's text is in any response, log or
/// telemetry, and none of the names, goal wording, searches or the manager's email that the tools take or return is in
/// a log or telemetry. A tool with no row in <see cref="ToolMatrix"/> has no arguments to call it with, so it fails the
/// first test.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class McpCanaryTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Revision = "2025-11-25";

    private const string DraftNarrativeSql = "SELECT COUNT(*) AS [Value] FROM [NoteDraft] WHERE [Narrative] = @text";

    private const string DraftFlagReasonSql = "SELECT COUNT(*) AS [Value] FROM [NoteDraft] WHERE [FlagReason] = @text";

    private const string VersionNarrativeSql =
        "SELECT COUNT(*) AS [Value] FROM [NoteVersion] WHERE [Narrative] = @text";

    private const string VersionFlagReasonSql =
        "SELECT COUNT(*) AS [Value] FROM [NoteVersion] WHERE [FlagReason] = @text";

    private const string ReviewCommentSql = "SELECT COUNT(*) AS [Value] FROM [NoteReview] WHERE [Comment] = @text";

    private static readonly DateTime StartedAtUtc = new(2026, 10, 20, 1, 2, 3, DateTimeKind.Utc);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_note_text_reaches_any_tool_response_error_log_or_telemetry()
    {
        var sinks = new TelemetrySinks();
        await using var flow = await AssistantFlow.StartAsync(
            factory, sqlServer, "McpCanary", probes: false, telemetry: sinks);
        var scene = await ToolScene.CreateAsync(flow);
        var canary = $"canary-{Guid.NewGuid():N}";
        var notes = await AddNoteTextsAsync(flow, scene, canary);
        foreach (var note in notes)
        {
            Assert.Equal(1, await StoredAsync(flow, note));
        }

        var responses = await CallEveryToolAsync(flow, scene, sinks);

        // Each text starts with the canary, which alone would find them; each is looked for too, so a failure names it.
        var leaks = notes.Select(note => note.Text).Prepend(canary)
            .SelectMany(text => responses.Concat(sinks.Texts()).Where(t => Holds(t.Text, text)).Select(Describe));
        Assert.Empty(leaks);

        // The check above looked at something: the tools answered, and the telemetry reached the exporters.
        Assert.Contains(responses, response => Holds(response.Text, scene.FamilyName));
        Assert.NotEmpty(sinks.Ingestion.Payloads);
    }

    [Fact]
    public async Task No_name_email_goal_wording_or_search_reaches_logs_or_telemetry()
    {
        var sinks = new TelemetrySinks();
        await using var flow = await AssistantFlow.StartAsync(
            factory, sqlServer, "McpCanary", probes: false, telemetry: sinks);
        var scene = await ToolScene.CreateAsync(flow);
        var email = Assert.IsType<string>((await flow.UserAsync(scene.Organisation.ManagerId)).Email);

        var responses = await CallEveryToolAsync(flow, scene, sinks);

        // A search that is a letter or two is in almost every text, so it can say nothing about a leak. The family
        // name is also sent as includeArchived, wrongly typed, which is why that argument is read too.
        var searches = ToolMatrix.Rows
            .SelectMany(row => CallsOf(row, scene))
            .SelectMany(arguments => arguments)
            .Where(argument => argument.Key is "search" or "includeArchived")
            .Select(argument => argument.Value?.ToString())
            .OfType<string>()
            .Where(search => search.Length >= 4)
            .Distinct()
            .ToList();
        Assert.Contains(scene.FamilyName, searches);
        string[] secrets =
        [
            scene.GivenName,
            scene.FamilyName,
            .. scene.Goals.Select(goal => goal.Text),
            email,
            .. searches,
        ];

        // Ignoring case, as the M0 canary does, so no text is missed because of how it is written.
        var texts = sinks.Texts().ToList();
        Assert.Empty(secrets.SelectMany(secret => texts.Where(t => Holds(t.Text, secret)).Select(Describe)));

        // The names and the goal wording are in what the tools answered, so the check above did not pass by seeing
        // nothing, and the telemetry that it looked at is what Application Insights would receive.
        foreach (var returned in new[] { scene.GivenName, scene.FamilyName }.Concat(scene.Goals.Select(g => g.Text)))
        {
            Assert.Contains(responses, response => Holds(response.Text, returned));
        }

        Assert.NotEmpty(sinks.Ingestion.Payloads);
        Assert.NotEmpty(texts);
    }

    private static bool Holds(string? text, string value) =>
        text?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private static string Describe((string Sink, string? Text) found) => $"{found.Sink}: {found.Text}";

    /// <summary>
    /// Adds a draft and a submitted note with a review, each text with a unique value that starts with
    /// <paramref name="canary"/>, for the scene's participant, as the manager: the five texts that only a note holds.
    /// </summary>
    private static async Task<IReadOnlyList<NoteText>> AddNoteTextsAsync(
        AssistantFlow flow, ToolScene scene, string canary)
    {
        NoteText draftNarrative = new(DraftNarrativeSql, $"{canary}-draft-narrative");
        NoteText draftFlagReason = new(DraftFlagReasonSql, $"{canary}-draft-flag-reason");
        NoteText versionNarrative = new(VersionNarrativeSql, $"{canary}-version-narrative");
        NoteText versionFlagReason = new(VersionFlagReasonSql, $"{canary}-version-flag-reason");
        NoteText reviewComment = new(ReviewCommentSql, $"{canary}-review-comment");

        var services = flow.App.Services;
        var organisation = scene.Organisation;
        var participantId = scene.Participant.Id;

        // Two days, as a participant has one live note a day.
        await services.AddDraftAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 10, 21),
                organisation.ManagerId,
                StartedAtUtc,
                Narrative: draftNarrative.Text,
                IsFlagged: true,
                FlagReason: draftFlagReason.Text));
        var submittedId = await services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                new DateOnly(2026, 10, 20),
                organisation.ManagerId,
                StartedAtUtc,
                StartedAtUtc.AddMinutes(5),
                Narrative: versionNarrative.Text,
                IsFlagged: true,
                FlagReason: versionFlagReason.Text));
        await services.AddReviewAsync(
            organisation,
            submittedId,
            versionNumber: 1,
            organisation.ManagerId,
            StartedAtUtc.AddMinutes(10),
            reviewComment.Text);
        return [draftNarrative, draftFlagReason, versionNarrative, versionFlagReason, reviewComment];
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private static async Task<int> StoredAsync(AssistantFlow flow, NoteText note)
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database
            .SqlQueryRaw<int>(note.CountSql, new SqlParameter("text", note.Text))
            .SingleAsync(Ct);
    }

    /// <summary>The arguments of every call of a tool: the valid ones, then those of each error case.</summary>
    private static IEnumerable<JsonObject> CallsOf(ToolRow row, ToolScene scene) =>
        row.ErrorCases.Select(errorCase => errorCase.Arguments(scene)).Prepend(row.ValidArguments(scene));

    /// <summary>
    /// Connects an assistant as the scene's manager and calls every tool the app lists, raw, with its valid arguments
    /// and then with each of its error cases; and gives the bodies and headers of the answers, once the telemetry that
    /// the calls made has been exported, so that a check of it cannot pass by looking too soon.
    /// </summary>
    private static async Task<IReadOnlyList<(string Sink, string? Text)>> CallEveryToolAsync(
        AssistantFlow flow, ToolScene scene, TelemetrySinks sinks)
    {
        var token = await scene.ConnectAsync(ToolCaller.Manager);
        await using var client = await flow.McpClientAsync(token, Revision);
        var listed = (await client.ListToolsAsync(cancellationToken: Ct)).Select(tool => tool.Name).ToList();
        var withoutRow = listed.Except(ToolMatrix.Rows.Select(row => row.Name)).ToList();
        Assert.True(
            withoutRow.Count == 0,
            $"No argument generator: add a ToolMatrix row for {string.Join(", ", withoutRow)}.");

        var answers = new List<(string Sink, string? Text)>();
        var calls = 0;
        foreach (var row in ToolMatrix.Rows)
        {
            foreach (var arguments in CallsOf(row, scene))
            {
                using var response = await flow.PostMcpAsync(token, body: AssistantFlow.ToolCall(row.Name, arguments));
                answers.Add(("response body", await response.Content.ReadAsStringAsync(Ct)));
                answers.AddRange(response.Headers.Concat(response.Content.Headers).Select(
                    header => ("response header", (string?)$"{header.Key}: {string.Join(", ", header.Value)}")));
                calls++;
            }
        }

        // The test server answers before a request's span ends, and the exporters send on their own schedules.
        Assert.True(
            SpinWait.SpinUntil(
                () => sinks.Spans.Spans.Count(span => span.Kind == ActivityKind.Server
                    && span.GetTagItem("url.path") as string == "/mcp") >= calls,
                TimeSpan.FromSeconds(10)),
            "The calls' spans did not end.");
        Assert.True(flow.App.Services.GetRequiredService<TracerProvider>().ForceFlush());
        Assert.True(flow.App.Services.GetRequiredService<LoggerProvider>().ForceFlush());
        Assert.True(flow.App.Services.GetRequiredService<MeterProvider>().ForceFlush());
        return answers;
    }

    /// <summary>A text that only a note holds, and the query that counts the rows storing it as <c>@text</c>.</summary>
    private sealed record NoteText(string CountSql, string Text);
}
