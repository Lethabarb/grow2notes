using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>
/// The tools that look participants up and change them (mcp-server.md §5.2). The look-ups are read-only. A change
/// checks the form of its arguments, calls the handler that the app's own endpoint calls, as <see cref="Actor.InMcp"/>,
/// and answers from a fresh look-up. None returns a date of birth or anything about notes. A look-up reports the IDs it
/// returned to <see cref="McpCall"/>, which <see cref="McpAuditFilter"/> writes as the call's one audit row, and a
/// change writes its own event through the handler: the tools add no <c>via</c> key. Nothing saves between a refused
/// handler result and the error result, so a refused change is never written.
/// </summary>
[McpServerToolType]
internal sealed class ParticipantsTools(
    ParticipantLookups lookups,
    ParticipantChanges changes,
    IHttpContextAccessor httpContextAccessor,
    McpCall call)
{
    private const string DateFormat = "yyyy-MM-dd";

    private const string ParticipantNotFound =
        "No participant has that ID in this organisation. Find them with find_participants.";

    private const string ParticipantChanged =
        "This participant was changed since you looked them up. Look them up again with get_participant, then retry.";

    private const string ParticipantArchived = "This participant is archived. Restore the participant first.";

    private const string NoFieldToChange = "Send at least one of givenName, familyName and dateOfBirth.";

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
            return ToolError.NotFound(ParticipantNotFound).ToResult();
        }

        call.Returned(McpCall.ParticipantIds, [id]);
        call.Returned(McpCall.GoalIds, [.. participant.Goals.Select(goal => goal.GoalId)]);
        return ToolResults.Of(participant);
    }

    [McpServerTool(
        Name = "add_participant",
        Title = "Add a participant",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(NewParticipant))]
    [Description(
        "Adds an active participant to this organisation. Returns their participantId and the expectedVersion that a " +
        "later change needs. Never returns a date of birth.")]
    public async Task<CallToolResult> AddParticipantAsync(
        [Description("The given name: at most 100 characters.")] string givenName,
        [Description("The family name: at most 100 characters.")] string familyName,
        [Description("The date of birth, written YYYY-MM-DD and before today.")] string dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        if (ReadDetails(givenName, familyName, dateOfBirth, out var born) is { } refusal)
        {
            return refusal.ToResult();
        }

        var added = await changes.AddAsync(ActingUser(), new(givenName, familyName, born), cancellationToken);
        if (added.Errors is { } errors)
        {
            return ToolError.ValidationFailed(errors).ToResult();
        }

        var participant = await lookups.GetAsync(added.Id, cancellationToken) ?? throw new UnreachableException();
        call.Returned(McpCall.ParticipantIds, [added.Id]);
        return ToolResults.Of(new NewParticipant(
            added.Id, participant.GivenName, participant.FamilyName, participant.Status, participant.ExpectedVersion));
    }

    [McpServerTool(
        Name = "update_participant",
        Title = "Correct a participant's name or date of birth",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(UpdatedParticipant))]
    [Description(
        "Corrects a participant's given name, family name or date of birth: send only the fields to change. " +
        "Needs the participantId and expectedVersion that get_participant returned. A call that would change " +
        "nothing answers changed: false. An archived participant cannot be changed: restore the participant " +
        "first. Never returns a date of birth.")]
    public async Task<CallToolResult> UpdateParticipantAsync(
        [Description("The participantId that find_participants or get_participant returned.")] string participantId,
        [Description("The expectedVersion that get_participant returned for this participant.")]
        string? expectedVersion = null,
        [Description("The new given name: at most 100 characters.")] string? givenName = null,
        [Description("The new family name: at most 100 characters.")] string? familyName = null,
        [Description("The new date of birth, written YYYY-MM-DD and before today.")] string? dateOfBirth = null,
        CancellationToken cancellationToken = default)
    {
        if (givenName is null && familyName is null && dateOfBirth is null)
        {
            return new ToolError(ErrorCode.ValidationFailed.Code, NoFieldToChange).ToResult();
        }

        if (ReadDetails(givenName, familyName, dateOfBirth, out var born) is { } refusal)
        {
            return refusal.ToResult();
        }

        if (!Guid.TryParse(participantId, out var id))
        {
            return ToolError.NotFound(ParticipantNotFound).ToResult();
        }

        var result = await changes.UpdateAsync(
            ActingUser(),
            id,
            IfMatch.FromExpectedVersion(expectedVersion),
            new(givenName, familyName, born),
            cancellationToken);
        if (!result.Found)
        {
            return ToolError.NotFound(ParticipantNotFound).ToResult();
        }

        if (result.Error is { } code)
        {
            return ErrorOf(code).ToResult();
        }

        if (result.Errors is { } errors)
        {
            return ToolError.ValidationFailed(errors).ToResult();
        }

        var participant = await lookups.GetAsync(id, cancellationToken) ?? throw new UnreachableException();
        call.Returned(McpCall.ParticipantIds, [id]);
        return ToolResults.Of(new UpdatedParticipant(
            participant.GivenName,
            participant.FamilyName,
            participant.Status,
            result.Changed,
            participant.ExpectedVersion));
    }

    // The form of the details that were sent, before any row is read: each name's length and characters, and the date's
    // exact form. A field left out (null) is not checked. A blank date is refused here, never passed on as left out,
    // because a field left out keeps its stored value. The rest of the rules are the handler's, in the app's words.
    private static ToolError? ReadDetails(
        string? givenName, string? familyName, string? dateOfBirth, out DateOnly? born)
    {
        born = null;
        if (givenName is not null
            && TextInput.Refusal("givenName", "Given name", givenName, Limits.ParticipantName) is { } givenRefusal)
        {
            return givenRefusal;
        }

        if (familyName is not null
            && TextInput.Refusal("familyName", "Family name", familyName, Limits.ParticipantName) is { } familyRefusal)
        {
            return familyRefusal;
        }

        if (dateOfBirth is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dateOfBirth))
        {
            return ToolError.ValidationFailed("dateOfBirth", ParticipantDetails.EnterDateOfBirth);
        }

        if (!DateOnly.TryParseExact(
            dateOfBirth, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return ToolError.ValidationFailed("dateOfBirth", ParticipantDetails.DateOfBirthRealDate);
        }

        born = parsed;
        return null;
    }

    // The refusal that a handler gave with a design.md §6.9 code, in words for the model. A code that no participant
    // tool can get is a bug, and McpAuditFilter turns it into unexpected.
    private static ToolError ErrorOf(ErrorCode code)
    {
        if (code == ErrorCode.PreconditionRequired)
        {
            return ToolError.PreconditionRequired;
        }

        if (code == ErrorCode.PreconditionFailed)
        {
            return new(code.Code, ParticipantChanged);
        }

        if (code == ErrorCode.ParticipantArchived)
        {
            return new(code.Code, ParticipantArchived);
        }

        throw new UnreachableException($"A participant tool cannot be refused with {code.Code}.");
    }

    private Actor ActingUser() => Actor.InMcp(
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("The call has no signed-in principal."));
}
