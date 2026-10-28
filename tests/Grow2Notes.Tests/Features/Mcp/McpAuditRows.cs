using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// Reading back the audit rows that an MCP call wrote (mcp-server.md §9) and asserting what they hold, for the tests
/// that call <c>/mcp</c>: they read the app's database in SQL, as it is after the call.
/// </summary>
internal static class McpAuditRows
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The ID of the last event written on the app's database. Each case has a database of its own.
    public static async Task<long> LastEventIdAsync(AssistantFlow flow)
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT COALESCE(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(Ct);
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen.
    public static async Task<List<AuditRow>> EventsAfterAsync(AssistantFlow flow, long eventId)
    {
        await using var scope = flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.SqlQuery<AuditRow>($"""
            SELECT [EventType], [ActorUserId], [ParticipantId], [Details], [IpAddress]
            FROM [AuditEvent]
            WHERE [Id] > {eventId}
            ORDER BY [Id]
            """).ToListAsync(Ct);
    }

    // The IDs of the users of these addresses, in one read of AspNetUsers, for a look-up that returns addresses and no
    // ID (list_users) and whose audit row holds the IDs. Each address must be a user's.
    public static async Task<Guid[]> UserIdsByEmailAsync(AssistantFlow flow, IReadOnlyCollection<string> emails)
    {
        string[] addresses = [.. emails];
        await using var scope = flow.App.Services.CreateAsyncScope();
        var ids = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users.AsNoTracking()
            .Where(user => addresses.Contains(user.Email!))
            .Select(user => user.Id)
            .ToArrayAsync(Ct);
        Assert.Equal(addresses.Length, ids.Length);
        return ids;
    }

    // An mcp.refused row: the code, the client and the tool, alone.
    public static void AssertRefused(AuditRow row, string tool, string code)
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

    // The event that a change tool's save wrote, as the app writes it, by the person who called: the event type, the
    // actor, and the way, the client and the tool that the call added to the details.
    public static void AssertChanged(AuditRow row, string eventType, string tool, Guid actorId)
    {
        Assert.Equal(eventType, row.EventType);
        Assert.Equal(actorId, row.ActorUserId);
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details))!.AsObject();
        Assert.Equal("mcp", details["via"]!.GetValue<string>());
        Assert.Equal(AssistantFlow.ClientId, details["client"]!.GetValue<string>());
        Assert.Equal(tool, details["tool"]!.GetValue<string>());
    }

    // An mcp.read row: the participants listed in any order, their count, and the client and the tool.
    public static void AssertRead(AuditRow row, string tool, params Guid[] participants) =>
        AssertRead(row, AssistantFlow.ClientId, tool, ("participantIds", participants));

    // An mcp.read row holding exactly the keys given, each with its IDs in any order, the sum of their counts, and the
    // client and the tool: no other key.
    public static void AssertRead(
        AuditRow row, string client, string tool, params (string Key, Guid[] Ids)[] returned)
    {
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details))!.AsObject();
        foreach (var (key, ids) in returned)
        {
            Assert.Equal(
                ids.Select(id => id.ToString("D")).Order(StringComparer.Ordinal),
                details[key]!.AsArray().Select(id => id!.GetValue<string>()).Order(StringComparer.Ordinal));
        }

        Assert.Equal(returned.Sum(entry => entry.Ids.Length), details["count"]!.GetValue<int>());
        Assert.Equal("mcp", details["via"]!.GetValue<string>());
        Assert.Equal(client, details["client"]!.GetValue<string>());
        Assert.Equal(tool, details["tool"]!.GetValue<string>());
        Assert.Equal(returned.Length + 4, details.Count);
    }
}

/// <summary>An audit row as the tests read it.</summary>
internal sealed record AuditRow(
    string EventType, Guid? ActorUserId, Guid? ParticipantId, string? Details, string? IpAddress);
