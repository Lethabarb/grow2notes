using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.DataProtection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>Where a confirming tool stands: what <see cref="ToolConfirmation.Read"/> found in the call.</summary>
internal enum ConfirmationStep
{
    /// <summary>The person has not been asked yet: show the preview, or the form.</summary>
    Ask,

    /// <summary>The person said yes: act.</summary>
    Confirmed,

    /// <summary>The person answered, and did not say yes: change nothing.</summary>
    Declined,

    /// <summary>The confirmation does not belong to this call, or has lapsed: change nothing.</summary>
    StateRefused,
}

/// <summary>The step of a call, and the version of the row that the person was asked about, once confirmed.</summary>
internal readonly record struct ConfirmationAnswer(ConfirmationStep Step, string? ExpectedVersion);

/// <summary>
/// The confirmation that stands before a change to a person's access (mcp-server.md §5.3, design.md §13, A136). A
/// client that can show a form is asked by one; any other is given the preview and calls again with
/// <c>confirm: true</c> once the person has said yes in the conversation. Nothing is stored: what the form asked is
/// sealed in the <c>requestState</c> that the client sends back with the answer, so any instance can read it.
/// </summary>
/// <remarks>
/// The form is used only when the request is a 2026-07-28 one (<see cref="McpServer.IsMrtrSupported"/>) whose own
/// <c>_meta</c> declares form elicitation. That is read from the request, not from <c>McpServer.ClientCapabilities</c>,
/// which a stateless server does not have. A client that declares a blank <c>elicitation</c> counts: the SDK reads it
/// as the form. <c>McpServer.ElicitAsync</c> is not used, as it keeps a continuation in memory.
/// <para>
/// The state holds who was asked, in which organisation, for which tool and arguments (as a hash, so that a changed
/// email or reason invalidates it), the version of the row, and when. It is checked before the answer is read. It is
/// not single use: a replay within its lifetime meets the row, and its version.
/// </para>
/// <para>
/// The consent is the client's answer, so a client that lets the model write that answer defeats the form. That is the
/// protocol's trust boundary, which design.md §13 (MA6) accepts.
/// </para>
/// </remarks>
internal sealed class ToolConfirmation(IDataProtectionProvider dataProtection, TimeProvider time, ITenantContext tenant)
{
    /// <summary>How long a form can be answered, from when it was asked; exactly this long is still live.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Purpose = "Grow2Notes.Mcp.ToolConfirmation";
    private const string ConfirmField = "confirm";
    private const string VersionField = "expectedVersion";

    private readonly IDataProtector protector = dataProtection.CreateProtector(Purpose);

    /// <summary>The refusal of a state that has lapsed, is not readable, or is for another call.</summary>
    public static ToolError StateRefused { get; } = new(
        ErrorCode.PreconditionFailed.Code,
        "This confirmation has expired or does not match this call. Nothing was changed. "
        + "Call the tool again to ask the person afresh.");

    /// <summary>
    /// Where the call stands. A form call that sends no state is new. One that does is checked first; only then is the
    /// answer read, and only an accepted form whose box is JSON <c>true</c> confirms. On the preview path
    /// <paramref name="confirm"/> confirms, with <paramref name="expectedVersion"/>; the form path ignores both.
    /// </summary>
    public ConfirmationAnswer Read(
        RequestContext<CallToolRequestParams> context, Actor actor, bool confirm, string? expectedVersion)
    {
        if (!SupportsForm(context))
        {
            return confirm ? new(ConfirmationStep.Confirmed, expectedVersion) : new(ConfirmationStep.Ask, null);
        }

        var parameters = context.Params!;
        if (parameters.RequestState is not { } token)
        {
            return new(ConfirmationStep.Ask, null);
        }

        var state = ProtectedJson.Open<ConfirmationState>(protector, token);
        if (state is null || !IsFor(state, parameters, actor))
        {
            return new(ConfirmationStep.StateRefused, null);
        }

        return IsYes(parameters)
            ? new(ConfirmationStep.Confirmed, state.ExpectedVersion)
            : new(ConfirmationStep.Declined, null);
    }

    /// <summary>
    /// Asks the person to confirm. A client that shows forms is sent <paramref name="message"/> and one box titled
    /// <paramref name="yesTitle"/>, with the state of this call and <paramref name="version"/>; that is an
    /// <see cref="InputRequiredException"/>, which <see cref="McpAuditFilter"/> lets through after it has written the
    /// call's row. Any other client is given <paramref name="preview"/>.
    /// </summary>
    public CallToolResult Ask(
        RequestContext<CallToolRequestParams> context,
        Actor actor,
        string version,
        string message,
        string yesTitle,
        CallToolResult preview)
    {
        if (!SupportsForm(context))
        {
            return preview;
        }

        var parameters = context.Params!;
        var state = new ConfirmationState(
            actor.UserId, tenant.OrganisationId, parameters.Name, ArgumentsHash(parameters), version, time.GetUtcNow());
        var form = new ElicitRequestParams
        {
            Mode = "form",
            Message = message,
            RequestedSchema = new()
            {
                Properties = { [ConfirmField] = new ElicitRequestParams.BooleanSchema { Title = yesTitle } },
                Required = [ConfirmField],
            },
        };
        throw new InputRequiredException(
            new Dictionary<string, InputRequest> { [ConfirmField] = InputRequest.ForElicitation(form) },
            ProtectedJson.Seal(protector, state));
    }

    private static bool SupportsForm(RequestContext<CallToolRequestParams> context) =>
        context.Server.IsMrtrSupported
        && context.JsonRpcMessage.Context?.ClientCapabilities?.Elicitation?.Form is not null;

    private static bool IsYes(CallToolRequestParams parameters)
    {
        try
        {
            return parameters.InputResponses is { } responses
                && responses.TryGetValue(ConfirmField, out var response)
                && response.Deserialize(InputResponse.ElicitResultJsonTypeInfo) is
                    { Action: "accept", Content: { } content }
                && content.TryGetValue(ConfirmField, out var answer)
                && answer.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool IsFor(ConfirmationState state, CallToolRequestParams parameters, Actor actor) =>
        time.GetUtcNow() - state.AskedAt <= Lifetime
        && state.UserId == actor.UserId
        && state.OrganisationId == tenant.OrganisationId
        && state.Tool == parameters.Name
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(state.ArgumentsHash), Encoding.UTF8.GetBytes(ArgumentsHash(parameters)));

    /// <summary>
    /// The SHA-256 of the arguments that decide what the call does: every one but <c>confirm</c> and
    /// <c>expectedVersion</c>, and but those that are null or absent (SDK clients differ on sending them), by name in
    /// ordinal order. A string counts as its text and any other value as its JSON.
    /// </summary>
    private static string ArgumentsHash(CallToolRequestParams parameters)
    {
        var pairs = new JsonArray();
        IEnumerable<KeyValuePair<string, JsonElement>> arguments = parameters.Arguments?.AsEnumerable() ?? [];
        foreach (var (name, value) in arguments.OrderBy(argument => argument.Key, StringComparer.Ordinal))
        {
            if (name is ConfirmField or VersionField
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            JsonNode? node = value.ValueKind == JsonValueKind.String
                ? JsonValue.Create(value.GetString())
                : JsonNode.Parse(value.GetRawText());
            pairs.Add(new JsonArray(JsonValue.Create(name), node));
        }

        return ProtectedJson.Hash(pairs.ToJsonString());
    }

    private sealed record ConfirmationState(
        Guid UserId,
        Guid OrganisationId,
        string Tool,
        string ArgumentsHash,
        string ExpectedVersion,
        DateTimeOffset AskedAt);
}
