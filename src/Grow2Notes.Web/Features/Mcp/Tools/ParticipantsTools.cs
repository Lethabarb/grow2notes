using System.ComponentModel;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>
/// The tools that look participants up (mcp-server.md §5.2). They are read-only and return no date of birth and nothing
/// about notes. Each reports the IDs it returned to <see cref="McpCall"/>, which <see cref="McpAuditFilter"/> writes as
/// the call's one audit row: the tools write no row and add no <c>via</c> key of their own.
/// </summary>
[McpServerToolType]
internal sealed class ParticipantsTools(ParticipantLookups lookups, McpCall call)
{
    [McpServerTool(
        Name = "find_participants",
        Title = "Find participants by name",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(FoundParticipants))]
    [Description(
        "Finds participants of this organisation by name. Every word of search must appear in the given or family " +
        "name; case is ignored. Returns at most 10, sorted by family name then given name, each with its status " +
        "(active or archived); archived participants only with includeArchived. If two people have the same name, " +
        "or you are not sure which one the person means, ask them. Never returns a date of birth or anything about " +
        "notes.")]
    public async Task<CallToolResult> FindParticipantsAsync(
        [Description(
            "Part of the participant's name: at least 2 characters and at most 100. Each word must appear in the " +
            "given or family name.")]
        string search,
        [Description("Also find archived participants.")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (TextInput.Refusal("search", "Search", search, Limits.ParticipantName) is { } refusal)
        {
            return refusal.ToResult();
        }

        if (search.Trim().Length < 2)
        {
            return ToolError.ValidationFailed("search", "Search must be at least 2 characters").ToResult();
        }

        var found = await lookups.FindAsync(search, includeArchived, cancellationToken);
        call.Returned(McpCall.ParticipantIds, [.. found.Participants.Select(participant => participant.ParticipantId)]);
        return ToolResults.Of(found);
    }

    [McpServerTool(
        Name = "get_participant",
        Title = "Get a participant and their goals",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(ParticipantWithGoals))]
    [Description(
        "One participant of this organisation with their goals: active goals in their order, then archived goals. " +
        "Use a participantId from find_participants. A later change to the participant or a goal needs its " +
        "expectedVersion. Never returns a date of birth or anything about notes.")]
    public async Task<CallToolResult> GetParticipantAsync(
        [Description("The participantId that find_participants returned.")] string participantId,
        CancellationToken cancellationToken = default)
    {
        var participant = Guid.TryParse(participantId, out var id)
            ? await lookups.GetAsync(id, cancellationToken)
            : null;
        if (participant is null)
        {
            return ToolError.NotFound(
                "No participant has that ID in this organisation. Find them with find_participants.").ToResult();
        }

        call.Returned(McpCall.ParticipantIds, [id]);
        call.Returned(McpCall.GoalIds, [.. participant.Goals.Select(goal => goal.GoalId)]);
        return ToolResults.Of(participant);
    }
}
