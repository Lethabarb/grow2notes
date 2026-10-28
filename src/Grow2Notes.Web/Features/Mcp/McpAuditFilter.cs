using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The one call-tool filter (mcp-server.md §9): it begins the call's <see cref="McpCall"/>, turns an exception that no
/// tool handled into the <c>unexpected</c> result, and writes the call's one audit row before the result goes back, so
/// that no tool can forget to audit. It is registered whether or not the access is on, as the audit rows call it.
/// </summary>
/// <remarks>
/// The row depends on the outcome: an error result writes <c>mcp.refused</c>; a call that wrote an app event of its own
/// has that event, which carries <c>via</c>, <c>client</c> and <c>tool</c>, and no more; any other call writes
/// <c>mcp.read</c>. The write is not cancellable: an assistant that disconnects after a look-up cannot undo the record
/// of what was disclosed. It comes before the result, so a call that cannot be audited returns no data (the SDK
/// answers its own generic error in its place).
/// <para>
/// A tool that asks the person to confirm (<see cref="ToolConfirmation"/>) ends in an
/// <see cref="InputRequiredException"/> instead of a result. That call changed nothing and disclosed what the tool
/// reported, so it is audited as any other call that returns, and the exception goes on to the SDK, which sends the
/// form.
/// </para>
/// </remarks>
internal sealed partial class McpAuditFilter(
    McpCall call,
    IAuditWriter audit,
    ITenantContext tenant,
    Grow2NotesDbContext db,
    ILogger<McpAuditFilter> logger)
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Wrap(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        (context, cancellationToken) => context.Services!.GetRequiredService<McpAuditFilter>()
            .CallAsync(context, next, cancellationToken);

    /// <summary>
    /// Writes the audit row of a call whose outcome is <paramref name="result"/>, as the signed-in
    /// <paramref name="actor"/> in the tenant: <c>mcp.refused</c> for an error, else <c>mcp.read</c> unless the call
    /// has already written an event. The call must have begun (<see cref="McpCall.Begin"/>).
    /// </summary>
    public Task WriteAsync(Actor actor, CallToolResult result) => WriteOutcomeAsync(actor, ToolError.CodeOf(result));

    /// <summary>
    /// Writes the row of a call that ended in <paramref name="refusedCode"/> if it was refused, else as a call that
    /// returned what it reported.
    /// </summary>
    private async Task WriteOutcomeAsync(Actor actor, string? refusedCode)
    {
        if (refusedCode is not null)
        {
            // The refused change may have left entities in the tracker, which this save would write.
            db.ChangeTracker.Clear();
            await WriteAsync(actor, AuditEventTypes.Mcp.Refused, participantId: null, new() { ["code"] = refusedCode });
            return;
        }

        if (call.EventWritten)
        {
            return;
        }

        var details = new JsonObject();
        var count = 0;
        foreach (var (key, ids) in call.ReturnedIds)
        {
            details[key] = new JsonArray([.. ids.Select(id => (JsonNode?)JsonValue.Create(id))]);
            count += ids.Count;
        }

        details["count"] = count;
        Guid? participantId = call.ReturnedIds.TryGetValue(McpCall.ParticipantIds, out var participants)
            && participants.Count == 1
                ? participants.Single()
                : null;
        await WriteAsync(actor, AuditEventTypes.Mcp.Read, participantId, details);
    }

    private async ValueTask<CallToolResult> CallAsync(
        RequestContext<CallToolRequestParams> context,
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        CancellationToken cancellationToken)
    {
        var principal = context.User ?? throw new InvalidOperationException("The call has no signed-in principal.");
        var tool = context.Params!.Name;
        call.Begin(
            principal.FindFirst(Claims.ClientId)?.Value
                ?? throw new InvalidOperationException("The access token has no client ID."),
            tool);

        CallToolResult result;
        try
        {
            result = await next(context, cancellationToken);
        }
        catch (InputRequiredException)
        {
            // Asking the person is not a failure: it is the call's one success row, and the SDK sends the form.
            await WriteOutcomeAsync(Actor.InMcp(principal), refusedCode: null);
            throw;
        }
        catch (Exception exception) when (
            exception is not McpProtocolException
            && !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            // Without the exception: every logger provider would write its message, which can hold what failed.
            LogUnexpected(tool, exception.GetType().FullName, DatabaseFailure.Find(exception)?.SqlErrorNumber);
            result = ToolError.Unexpected.ToResult();
        }

        await WriteAsync(Actor.InMcp(principal), result);
        return result;
    }

    private Task WriteAsync(Actor actor, string eventType, Guid? participantId, JsonObject details) =>
        audit.WriteAsync(
            tenant.OrganisationId,
            actor.UserId,
            eventType,
            entityType: null,
            entityId: null,
            participantId,
            details,
            CancellationToken.None);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "MCP tool {Tool} failed unexpectedly: {ExceptionType}, SQL error {SqlErrorNumber}")]
    private partial void LogUnexpected(string tool, string? exceptionType, int? sqlErrorNumber);
}
