using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.Extensions.Primitives;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>
/// The tools that look participants up and change them and their goals (mcp-server.md §5.2). The look-ups are
/// read-only. A change checks the form of its arguments, calls the handler that the app's own endpoint calls, as
/// <see cref="Actor.InMcp"/>, and answers from a fresh look-up. None returns a date of birth or anything about
/// notes. A look-up reports the IDs it returned to <see cref="McpCall"/>, which <see cref="McpAuditFilter"/> writes as
/// the call's one audit row, and a change writes its own event through the handler: the tools add no <c>via</c> key.
/// Nothing saves between a refused handler result and the error result, so a refused change is never written.
/// </summary>
[McpServerToolType]
internal sealed class ParticipantsTools(
    ParticipantLookups lookups,
    ParticipantChanges changes,
    GoalChanges goals,
    IHttpContextAccessor httpContextAccessor,
    McpCall call)
{
    private const string DateFormat = "yyyy-MM-dd";

    private const string ParticipantNotFound =
        "No participant has that ID in this organisation. Find them with find_participants.";

    private const string ParticipantChanged =
        "This participant was changed since you looked them up. Look them up again with get_participant, then retry.";

    private const string ParticipantArchived = "This participant is archived. Restore the participant first.";

    private const string GoalNotFound = "No goal has that ID in this organisation. Find it with get_participant.";

    private const string GoalChanged =
        "This goal was changed since you looked it up. Look it up again with get_participant, then retry.";

    private const string OrderMismatch =
        "goalIds must hold every active goal of the participant exactly once. " +
        "Look them up again with get_participant, then retry.";

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
            return ErrorOf(code, ParticipantChanged).ToResult();
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

    [McpServerTool(
        Name = "add_goal",
        Title = "Add a goal",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(NewGoal))]
    [Description(
        "Adds a goal to a participant, at the end of their active goals. Returns its goalId and the expectedVersion " +
        "that a later change needs. An archived participant's goals cannot be changed: restore the participant " +
        "first. The result's helpText says what a change to a goal does to notes.")]
    public async Task<CallToolResult> AddGoalAsync(
        [Description("The participantId that find_participants or get_participant returned.")] string participantId,
        [Description("The goal's wording: at most 200 characters, on one line.")] string text,
        CancellationToken cancellationToken = default)
    {
        if (TextInput.Refusal("text", "Goal", text, Limits.ItemText) is { } refusal)
        {
            return refusal.ToResult();
        }

        if (!Guid.TryParse(participantId, out var id))
        {
            return ToolError.NotFound(ParticipantNotFound).ToResult();
        }

        var result = await goals.AddAsync(ActingUser(), id, new(text), cancellationToken);
        if (RefusalOf(result, ParticipantNotFound) is { } error)
        {
            return error.ToResult();
        }

        var goalId = result.GoalId ?? throw new UnreachableException();
        var goal = await lookups.GetGoalAsync(goalId, cancellationToken) ?? throw new UnreachableException();
        call.Returned(McpCall.GoalIds, [goalId]);
        return ToolResults.Of(new NewGoal(goalId, goal.Text, goal.ExpectedVersion, ToolResults.HelpText));
    }

    [McpServerTool(
        Name = "update_goal",
        Title = "Reword a goal",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(UpdatedGoal))]
    [Description(
        "Rewords a goal. Needs the goalId and expectedVersion that get_participant returned. A call that would " +
        "change nothing answers changed: false. A goal of an archived participant cannot be changed: restore the " +
        "participant first. The result's helpText says what a change to a goal does to notes.")]
    public async Task<CallToolResult> UpdateGoalAsync(
        [Description("The goalId that get_participant returned.")] string goalId,
        [Description("The new wording: at most 200 characters, on one line.")] string text,
        [Description("The expectedVersion that get_participant returned for this goal.")]
        string? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (TextInput.Refusal("text", "Goal", text, Limits.ItemText) is { } refusal)
        {
            return refusal.ToResult();
        }

        if (!Guid.TryParse(goalId, out var id))
        {
            return ToolError.NotFound(GoalNotFound).ToResult();
        }

        var result = await goals.UpdateAsync(
            ActingUser(), id, IfMatch.FromExpectedVersion(expectedVersion), new(text), cancellationToken);
        if (RefusalOf(result, GoalNotFound) is { } error)
        {
            return error.ToResult();
        }

        var goal = await lookups.GetGoalAsync(id, cancellationToken) ?? throw new UnreachableException();
        call.Returned(McpCall.GoalIds, [id]);
        return ToolResults.Of(new UpdatedGoal(goal.Text, result.Changed, goal.ExpectedVersion, ToolResults.HelpText));
    }

    [McpServerTool(
        Name = "reorder_goals",
        Title = "Reorder a participant's goals",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(ReorderedGoals))]
    [Description(
        "Puts a participant's active goals in a new order. goalIds must hold every active goal of the participant " +
        "exactly once, in the order wanted; an archived goal is not one of them. Needs no expectedVersion. An order " +
        "that is already stored answers changed: false. Returns the goals as get_participant lists them, every " +
        "expectedVersion current. An archived participant's goals cannot be changed: restore the participant first. " +
        "The result's helpText says what a change to a goal does to notes.")]
    public async Task<CallToolResult> ReorderGoalsAsync(
        [Description("The participantId that find_participants or get_participant returned.")] string participantId,
        [Description("The goalId of every active goal of the participant, once each, in the new order.")]
        string[] goalIds,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(participantId, out var id))
        {
            return ToolError.NotFound(ParticipantNotFound).ToResult();
        }

        // An ID that is not a GUID cannot be one of the participant's goals: Guid.Empty is none, so the handler's check
        // of the set of IDs refuses it with the rest.
        var ordered = goalIds.Select(goalId => Guid.TryParse(goalId, out var parsed) ? parsed : Guid.Empty).ToList();
        var result = await goals.ReorderAsync(ActingUser(), id, ordered, cancellationToken);
        if (RefusalOf(result, ParticipantNotFound) is { } error)
        {
            return error.ToResult();
        }

        var participant = await lookups.GetAsync(id, cancellationToken) ?? throw new UnreachableException();
        call.Returned(McpCall.ParticipantIds, [id]);
        call.Returned(McpCall.GoalIds, [.. participant.Goals.Select(goal => goal.GoalId)]);
        return ToolResults.Of(new ReorderedGoals(result.Changed, participant.Goals, ToolResults.HelpText));
    }

    [McpServerTool(
        Name = "archive_goal",
        Title = "Archive a goal",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(GoalArchiveChange))]
    [Description(
        "Archives a goal, which acts at once. Needs the goalId and expectedVersion that get_participant returned. " +
        "An archived goal answers changed: false. The result has no version: look the goal up again with " +
        "get_participant before the next change. A goal of an archived participant cannot be changed: restore the " +
        "participant first. The result's helpText says what a change to a goal does to notes.")]
    public Task<CallToolResult> ArchiveGoalAsync(
        [Description("The goalId that get_participant returned.")] string goalId,
        [Description("The expectedVersion that get_participant returned for this goal.")]
        string? expectedVersion = null,
        CancellationToken cancellationToken = default) =>
        ChangePlaceAsync(goalId, expectedVersion, goals.ArchiveAsync, cancellationToken);

    [McpServerTool(
        Name = "restore_goal",
        Title = "Restore a goal",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(GoalArchiveChange))]
    [Description(
        "Restores an archived goal, which acts at once, and puts it at the end of the participant's active goals. " +
        "Needs the goalId and expectedVersion that get_participant returned. An active goal answers changed: false. " +
        "The result has no version: look the goal up again with get_participant before the next change. A goal of " +
        "an archived participant cannot be changed: restore the participant first. The result's helpText says what " +
        "a change to a goal does to notes.")]
    public Task<CallToolResult> RestoreGoalAsync(
        [Description("The goalId that get_participant returned.")] string goalId,
        [Description("The expectedVersion that get_participant returned for this goal.")]
        string? expectedVersion = null,
        CancellationToken cancellationToken = default) =>
        ChangePlaceAsync(goalId, expectedVersion, goals.RestoreAsync, cancellationToken);

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

    // Archive and restore are the same call but for the handler method, so the tool is the same too.
    private async Task<CallToolResult> ChangePlaceAsync(
        string goalId,
        string? expectedVersion,
        Func<Actor, Guid, StringValues, CancellationToken, Task<GoalChangeResult>> change,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(goalId, out var id))
        {
            return ToolError.NotFound(GoalNotFound).ToResult();
        }

        var result = await change(ActingUser(), id, IfMatch.FromExpectedVersion(expectedVersion), cancellationToken);
        if (RefusalOf(result, GoalNotFound) is { } error)
        {
            return error.ToResult();
        }

        call.Returned(McpCall.GoalIds, [id]);
        return ToolResults.Of(new GoalArchiveChange(result.Changed, ToolResults.HelpText));
    }

    // The refusal that a goal handler gave, or null when it gave none: the row it did not find in the words of the
    // tool, then its code, then the messages of the text.
    private static ToolError? RefusalOf(GoalChangeResult result, string notFound)
    {
        if (!result.Found)
        {
            return ToolError.NotFound(notFound);
        }

        if (result.Error is { } code)
        {
            return ErrorOf(code, GoalChanged);
        }

        return result.Errors is { } errors ? ToolError.ValidationFailed(errors) : null;
    }

    // The refusal that a handler gave with a design.md §6.9 code, in words for the model; changed is what a stale
    // version says about the row the tool changes. A code that no tool here can get is a bug, and McpAuditFilter turns
    // it into unexpected.
    private static ToolError ErrorOf(ErrorCode code, string changed)
    {
        if (code == ErrorCode.PreconditionRequired)
        {
            return ToolError.PreconditionRequired;
        }

        if (code == ErrorCode.PreconditionFailed)
        {
            return new(code.Code, changed);
        }

        if (code == ErrorCode.ParticipantArchived)
        {
            return new(code.Code, ParticipantArchived);
        }

        if (code == ErrorCode.OrderMismatch)
        {
            return new(code.Code, OrderMismatch);
        }

        throw new UnreachableException($"A participant tool cannot be refused with {code.Code}.");
    }

    private Actor ActingUser() => Actor.InMcp(
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("The call has no signed-in principal."));
}
