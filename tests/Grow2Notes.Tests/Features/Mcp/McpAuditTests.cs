using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The audit of an MCP tool call (mcp-server.md §9, S07.02.03) and the rule for an exception that no tool handled
/// (§5.5, S07.02.01): through the real <c>/mcp</c>, with a token from the real flow, and the stand-in tools of
/// <see cref="ProbeTools"/>. Each call writes exactly one row, which is read back in SQL. Each case has an app of its
/// own.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class McpAuditTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>, IAsyncLifetime
{
    private const string Revision = "2025-11-25";
    private const string AssistantAddress = "203.0.113.7";

    private AssistantFlow flow = null!;
    private Guid managerId;
    private HttpClient browser = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        flow = await AssistantFlow.StartAsync(factory, sqlServer, "McpAudit");
        managerId = await flow.AddUserAsync(UserRole.Manager);
        browser = await flow.SignInAsync(managerId);
    }

    public async ValueTask DisposeAsync()
    {
        browser.Dispose();
        await flow.DisposeAsync();
    }

    [Fact]
    public async Task An_exception_in_a_tool_answers_unexpected_rolls_back_writes_mcp_refused_and_logs_no_detail()
    {
        var tokens = await flow.ConnectAsync(browser);
        var canary = Guid.NewGuid().ToString("N");
        var promptsBefore = await GuidePromptsAsync();
        var lastEventId = await LastEventIdAsync();

        await using var client = await flow.McpClientAsync(tokens.AccessToken, Revision);
        var result = await client.CallToolAsync(
            "probe_fail", new Dictionary<string, object?> { ["canary"] = canary }, cancellationToken: Ct);

        // §5.5's error, in the structured content and as the text.
        Assert.True(result.IsError);
        var text = JsonDocument.Parse(Assert.Single(result.Content.OfType<TextContentBlock>()).Text).RootElement;
        Assert.True(JsonElement.DeepEquals(Assert.NotNull(result.StructuredContent), text));
        var error = text.GetProperty("error");
        Assert.Equal("unexpected", error.GetProperty("code").GetString());
        Assert.Equal("Something went wrong. Nothing was changed.", error.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, error.GetProperty("fields").ValueKind);

        // The change was rolled back with its own event, and the refusal was kept.
        Assert.Equal(promptsBefore, await GuidePromptsAsync());
        var row = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(managerId, row.ActorUserId);
        AssertRefused(row, "probe_fail", "unexpected");

        // One line, with the tool and the exception's type, and nothing that the exception said.
        var logged = Assert.Single(flow.Logs.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Category.EndsWith("McpAuditFilter", StringComparison.Ordinal));
        Assert.Null(logged.Exception);
        Assert.Contains("probe_fail", logged.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, logged.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(flow.Logs.Entries, entry => entry.Holds(canary));
    }

    [Fact]
    public async Task A_look_up_writes_mcp_read_with_the_ids_returned_a_count_the_client_and_the_tool()
    {
        var tokens = await flow.ConnectAsync(browser);
        var first = await flow.AddParticipantAsync(flow.OrganisationId);
        await using var client = await flow.McpClientAsync(tokens.AccessToken, Revision, AssistantAddress);

        var lastEventId = await LastEventIdAsync();
        ProbeTools.ReadWhoAmI(await client.CallToolAsync("probe_whoami", cancellationToken: Ct));

        // One participant: it is the row's participant.
        var row = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Equal(managerId, row.ActorUserId);
        Assert.Equal(first, row.ParticipantId);
        Assert.Equal(AssistantAddress, row.IpAddress);
        AssertRead(row, "probe_whoami", first);

        // Two: the row names none, and the details list both.
        var second = await flow.AddParticipantAsync(flow.OrganisationId);
        lastEventId = await LastEventIdAsync();
        ProbeTools.ReadWhoAmI(await client.CallToolAsync("probe_whoami", cancellationToken: Ct));

        row = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Null(row.ParticipantId);
        AssertRead(row, "probe_whoami", first, second);
    }

    [Fact]
    public async Task A_change_writes_the_app_event_with_via_client_and_tool_and_no_mcp_read()
    {
        var tokens = await flow.ConnectAsync(browser);
        var lastEventId = await LastEventIdAsync();

        await using var client = await flow.McpClientAsync(tokens.AccessToken, Revision);
        var result = await client.CallToolAsync(
            "probe_set_guide_prompts",
            new Dictionary<string, object?> { ["text"] = "Ask what went well." },
            cancellationToken: Ct);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("Ask what went well.", await GuidePromptsAsync());
        var row = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(AuditEventTypes.GuidePrompts.Updated, row.EventType);
        Assert.Equal(managerId, row.ActorUserId);

        // The app's own values stay in the details, and the call's three keys are added to them.
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details))!.AsObject();
        Assert.NotNull(details["text"]);
        Assert.Equal("mcp", details["via"]!.GetValue<string>());
        Assert.Equal(AssistantFlow.ClientId, details["client"]!.GetValue<string>());
        Assert.Equal("probe_set_guide_prompts", details["tool"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_tool_error_writes_mcp_refused_with_the_client_the_tool_and_the_code()
    {
        var tokens = await flow.ConnectAsync(browser);
        var lastEventId = await LastEventIdAsync();

        await using var client = await flow.McpClientAsync(tokens.AccessToken, Revision);
        var result = await client.CallToolAsync("probe_refuse", cancellationToken: Ct);

        Assert.True(result.IsError);
        var row = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(managerId, row.ActorUserId);
        Assert.Null(row.ParticipantId);
        AssertRefused(row, "probe_refuse", "not_found");
    }

    [Fact]
    public async Task A_call_aborted_after_the_tool_read_still_writes_mcp_read()
    {
        var tokens = await flow.ConnectAsync(browser);
        var participant = await flow.AddParticipantAsync(flow.OrganisationId);
        var lastEventId = await LastEventIdAsync();

        await using (var client = await flow.McpClientAsync(tokens.AccessToken, Revision))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await Record.ExceptionAsync(async () =>
                await client.CallToolAsync("probe_abort", cancellationToken: timeout.Token));
        }

        // The server finishes the call after the client has seen the connection drop, so the row is awaited.
        List<AuditRow> rows = [];
        for (var attempt = 0; attempt < 100 && rows.Count == 0; attempt++)
        {
            rows = await EventsAfterAsync(lastEventId);
            if (rows.Count == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
            }
        }

        var row = Assert.Single(rows);
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Equal(participant, row.ParticipantId);
        AssertRead(row, "probe_abort", participant);
    }

    // An mcp.refused row: the code, the client and the tool, alone.
    private static void AssertRefused(AuditRow row, string tool, string code)
    {
        Assert.Equal(AuditEventTypes.Mcp.Refused, row.EventType);
        var expected = new JsonObject
        {
            ["code"] = code,
            ["via"] = "mcp",
            ["client"] = AssistantFlow.ClientId,
            ["tool"] = tool,
        };
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details));
        Assert.True(JsonNode.DeepEquals(expected, details), $"Details: expected {expected}, written {details}.");
    }

    // An mcp.read row: the participants listed in any order, their count, and the client and the tool.
    private static void AssertRead(AuditRow row, string tool, params Guid[] participants)
    {
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details))!.AsObject();
        Assert.Equal(
            participants.Select(id => id.ToString("D")).Order(StringComparer.Ordinal),
            details["participantIds"]!.AsArray().Select(id => id!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Equal(participants.Length, details["count"]!.GetValue<int>());
        Assert.Equal("mcp", details["via"]!.GetValue<string>());
        Assert.Equal(AssistantFlow.ClientId, details["client"]!.GetValue<string>());
        Assert.Equal(tool, details["tool"]!.GetValue<string>());
        Assert.Equal(5, details.Count);
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private async Task<string> GuidePromptsAsync()
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .Where(organisation => organisation.Id == flow.OrganisationId)
            .Select(organisation => organisation.GuidePrompts)
            .SingleAsync(Ct);
    }

    // The ID of the last event written on the app's database. Each case has a database of its own.
    private async Task<long> LastEventIdAsync()
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT COALESCE(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(Ct);
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    private async Task<List<AuditRow>> EventsAfterAsync(long eventId)
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.SqlQuery<AuditRow>($"""
            SELECT [EventType], [ActorUserId], [ParticipantId], [Details], [IpAddress]
            FROM [AuditEvent]
            WHERE [Id] > {eventId}
            ORDER BY [Id]
            """).ToListAsync(Ct);
    }

    private sealed record AuditRow(
        string EventType, Guid? ActorUserId, Guid? ParticipantId, string? Details, string? IpAddress);
}
