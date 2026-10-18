using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The handler that changes a participant's goals (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/participants/{participantId}/goals</c>,
/// <c>PUT /api/admin/goals/{goalId}</c>, <c>PUT /api/admin/participants/{participantId}/goals/order</c> and
/// <c>POST /api/admin/goals/{goalId}/archive</c> and <c>/restore</c> call it with <see cref="Actor.InApp"/>, and
/// Release 2's tools will call it with <see cref="Channel.Mcp"/>. Each change is saved with its audit event through
/// <see cref="ConfigurationChanges"/>, in the tenant's organisation.
/// </summary>
internal sealed class GoalChanges(
    Grow2NotesDbContext db,
    ITenantContext tenant,
    ConfigurationChanges changes,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Adds a goal to a participant of the tenant's organisation, found through the tenant filter, so that another
    /// organisation's is not found (design.md §9.2), with the text that <see cref="ItemText.Goal"/> reads from
    /// <paramref name="request"/>, at the end of the participant's goals (<see cref="ListOrder.End"/>), and saves it
    /// with <c>goal.created</c>, whose entity is the new goal and whose participant is theirs, and whose
    /// <c>Details</c> hold its values as <c>new</c> alone (S01.02.05's Notes); or, when the text is refused, adds
    /// nothing and writes no event. An archived participant's goals are added to as an active one's are (D86).
    /// </summary>
    public async Task<GoalChangeResult> AddAsync(
        Actor actor, Guid participantId, GoalTextRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return GoalChangeResult.NotFound;
        }

        if (!ItemText.Goal.TryRead(request.Text, out var text, out var errors))
        {
            return GoalChangeResult.Invalid(errors);
        }

        // Two adds at once can read the same end and give their goals the same SortOrder, which the list's order
        // breaks by CreatedAtUtc, then Id (ListOrder).
        var activeGoals = await db.Goals.AsNoTracking()
            .Where(goal => goal.ParticipantId == participantId && goal.ArchivedAtUtc == null)
            .ToListAsync(cancellationToken);
        var goal = new Goal
        {
            OrganisationId = tenant.OrganisationId,
            ParticipantId = participantId,
            Text = text,
            SortOrder = ListOrder.End(activeGoals),
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Goals.Add(goal);

        // A new row has no version that could have changed since it was loaded, so the save is done or it throws.
        await changes.SaveAsync(
            actor,
            AuditEventTypes.Goal.Created,
            AuditEntityTypes.Goal,
            goal.Id,
            participantId,
            before: null,
            Values(goal),
            cancellationToken: cancellationToken);
        return GoalChangeResult.Saved;
    }

    /// <summary>
    /// Rewords a goal of the tenant's organisation (S01.02.06): loads it through the tenant filter, so another
    /// organisation's is not found; checks <paramref name="ifMatch"/> against its version; reads the text from
    /// <paramref name="request"/> by the add's rules (<see cref="ItemText.Goal"/>); then saves it with
    /// <c>goal.updated</c>, whose <c>Details</c> hold the old and new <c>text</c>, and whose participant is the goal's;
    /// or, when the text is already stored, saves nothing and writes no event, and answers as a save does (D84). An
    /// archived goal is reworded too, as design.md §6.6 names no refusal, though the screen offers Edit on active goals
    /// alone (T05's Notes).
    /// </summary>
    /// <param name="actor">Who rewords it.</param>
    /// <param name="goalId">The goal.</param>
    /// <param name="ifMatch">
    /// The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them, or Release 2's
    /// <c>expectedVersion</c> through <see cref="IfMatch.ETag"/>.
    /// </param>
    /// <param name="request">The text as sent.</param>
    /// <param name="cancellationToken">Cancels the reword.</param>
    /// <returns>
    /// The goal's <c>ETag</c> once the text is stored; or the code that <see cref="IfMatch.Check"/> gives, or
    /// <see cref="ErrorCode.PreconditionFailed"/> when the goal changed after it was loaded, or the text's message,
    /// with nothing written.
    /// </returns>
    public Task<GoalChangeResult> UpdateAsync(
        Actor actor, Guid goalId, StringValues ifMatch, GoalTextRequest request, CancellationToken cancellationToken) =>
        ChangeAsync(
            goalId,
            ifMatch,
            async goal =>
            {
                if (!ItemText.Goal.TryRead(request.Text, out var text, out var errors))
                {
                    return GoalChangeResult.Invalid(errors);
                }

                var before = TextValues(goal);
                goal.Text = text;
                var saved = await changes.SaveAsync(
                    actor,
                    AuditEventTypes.Goal.Updated,
                    AuditEntityTypes.Goal,
                    goal.Id,
                    goal.ParticipantId,
                    before,
                    TextValues(goal),
                    cancellationToken: cancellationToken);

                // EF Core reads back the version that SQL Server gave the row as it saved it, so after a save this is
                // the new ETag, and when nothing changed it is the one the If-Match matched.
                return saved.Error is { } error
                    ? GoalChangeResult.Refused(error)
                    : GoalChangeResult.Reworded(RowVersionToken.ETag(goal));
            },
            cancellationToken);

    /// <summary>
    /// Archives a goal of the tenant's organisation (design.md §3.7, S01.02.08), once <paramref name="ifMatch"/> is its
    /// version, as <see cref="UpdateAsync"/> checks it: sets <c>ArchivedAtUtc</c> through
    /// <see cref="Archiving.Archive"/>, which leaves its <c>SortOrder</c>, and saves it with <c>goal.archived</c>. A
    /// goal archived already keeps the time it was archived at, so nothing is saved and no event written, and the
    /// answer is as a save's (D84).
    /// </summary>
    /// <returns>
    /// <see cref="GoalChangeResult.Saved"/>, whether the goal changed or not; or the code that
    /// <see cref="IfMatch.Check"/> gives, or <see cref="ErrorCode.PreconditionFailed"/> when the goal changed after it
    /// was loaded, with nothing written.
    /// </returns>
    public Task<GoalChangeResult> ArchiveAsync(
        Actor actor, Guid goalId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            goalId,
            ifMatch,
            goal =>
            {
                var before = PlaceValues(goal);
                Archiving.Archive(goal, timeProvider);
                return SavePlaceAsync(actor, AuditEventTypes.Goal.Archived, goal, before, cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Restores a goal of the tenant's organisation (design.md §3.7, S01.02.08), once <paramref name="ifMatch"/> is its
    /// version, as <see cref="UpdateAsync"/> checks it: puts it at the end of its participant's active goals through
    /// <see cref="Archiving.Restore"/> (A2), and saves it with <c>goal.restored</c>. An active goal stays where it is,
    /// so nothing is saved and no event written, and the answer is as a save's (D84).
    /// </summary>
    /// <returns>As <see cref="ArchiveAsync"/>'s.</returns>
    public Task<GoalChangeResult> RestoreAsync(
        Actor actor, Guid goalId, StringValues ifMatch, CancellationToken cancellationToken) =>
        ChangeAsync(
            goalId,
            ifMatch,
            async goal =>
            {
                var before = PlaceValues(goal);
                var activeGoals = await db.Goals.AsNoTracking()
                    .Where(g => g.ParticipantId == goal.ParticipantId && g.ArchivedAtUtc == null)
                    .ToListAsync(cancellationToken);
                Archiving.Restore(goal, activeGoals);
                return await SavePlaceAsync(actor, AuditEventTypes.Goal.Restored, goal, before, cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Puts the active goals of a participant of the tenant's organisation, found through the tenant filter, so that
    /// another organisation's is not found, in the order of <paramref name="goalIds"/> (S01.02.07): reads them in the
    /// list's order (<see cref="GoalList.InListOrder"/>), the archived ones left out, so that
    /// <see cref="ListOrder.Reorder"/> refuses an archived ID as one it was not given; then saves the new order with
    /// <c>goal.reordered</c>, which names no goal as its entity, whose participant is theirs, and whose <c>Details</c>
    /// hold the old and new order; or, for the order already stored, saves nothing and writes no event (D84). An
    /// archived participant's goals are reordered as an active one's are (D86).
    /// </summary>
    /// <param name="actor">Who reorders them.</param>
    /// <param name="participantId">The participant.</param>
    /// <param name="goalIds">The IDs of the participant's active goals, in their new order.</param>
    /// <param name="cancellationToken">Cancels the reorder.</param>
    /// <returns>
    /// <see cref="GoalChangeResult.Saved"/>, whether the order changed or not; or
    /// <see cref="ErrorCode.OrderMismatch"/>, with nothing written, when the IDs are not the active goals, each once,
    /// or when a goal changed after the list was read, which the save finds as
    /// <see cref="ErrorCode.PreconditionFailed"/>: a reorder sends no <c>If-Match</c>, and the screen reads the list
    /// again for either (list-editor.md <i>Request errors</i>), so both are the one code.
    /// </returns>
    public async Task<GoalChangeResult> ReorderAsync(
        Actor actor, Guid participantId, IReadOnlyList<Guid> goalIds, CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return GoalChangeResult.NotFound;
        }

        // Tracked, so that the save writes the SortOrder that each moved goal is given, checking the version it was
        // read at.
        var activeGoals = await GoalList.InListOrder(
                db.Goals.Where(goal => goal.ParticipantId == participantId && goal.ArchivedAtUtc == null))
            .ToListAsync(cancellationToken);
        var reordered = ListOrder.Reorder(activeGoals, goalIds);
        if (reordered.Error is { } refused)
        {
            return GoalChangeResult.Refused(refused);
        }

        var saved = await changes.SaveAsync(
            actor,
            AuditEventTypes.Goal.Reordered,
            AuditEntityTypes.Goal,
            entityId: null,
            participantId,
            reordered.Before,
            reordered.After,
            cancellationToken: cancellationToken);
        return saved.Error is null ? GoalChangeResult.Saved : GoalChangeResult.Refused(ErrorCode.OrderMismatch);
    }

    /// <summary>
    /// Loads the goal through the tenant filter, so that another organisation's is not found (design.md §9.2), and
    /// checks <paramref name="ifMatch"/> against its version before <paramref name="change"/> changes and saves it.
    /// </summary>
    private async Task<GoalChangeResult> ChangeAsync(
        Guid goalId,
        StringValues ifMatch,
        Func<Goal, Task<GoalChangeResult>> change,
        CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(g => g.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return GoalChangeResult.NotFound;
        }

        return IfMatch.Check(ifMatch, RowVersionToken.Of(goal)) is { } refused
            ? GoalChangeResult.Refused(refused)
            : await change(goal);
    }

    // An archive or a restore of a goal, with its event, whose participant is the goal's and whose Details hold its
    // place before and after, the values that changed alone (S01.02.08's Notes); or nothing when neither changed.
    private async Task<GoalChangeResult> SavePlaceAsync(
        Actor actor, string eventType, Goal goal, JsonObject before, CancellationToken cancellationToken)
    {
        var saved = await changes.SaveAsync(
            actor,
            eventType,
            AuditEntityTypes.Goal,
            goal.Id,
            goal.ParticipantId,
            before,
            PlaceValues(goal),
            cancellationToken: cancellationToken);
        return saved.Error is { } error ? GoalChangeResult.Refused(error) : GoalChangeResult.Saved;
    }

    /// <summary>
    /// A goal's values as an audit event records them, named and written as design.md §6.6's list gives them, never
    /// <c>id</c> or <c>rowVersion</c> (<see cref="ChangeDetails"/>): <c>archivedAtUtc</c> as stored, or JSON
    /// <c>null</c>.
    /// </summary>
    private static JsonObject Values(Goal goal) => new()
    {
        ["text"] = goal.Text,
        ["sortOrder"] = goal.SortOrder,
        ["archivedAtUtc"] = goal.ArchivedAtUtc,
    };

    /// <summary>
    /// The one value that a reword changes, as <c>goal.updated</c> records it, named as <see cref="Values"/> names it.
    /// </summary>
    private static JsonObject TextValues(Goal goal) => new() { ["text"] = goal.Text };

    /// <summary>
    /// The goal's place in its participant's list, the values that archiving and restoring change, as their events
    /// record them, named as <see cref="Values"/> names them.
    /// </summary>
    private static JsonObject PlaceValues(Goal goal) => new()
    {
        ["sortOrder"] = goal.SortOrder,
        ["archivedAtUtc"] = goal.ArchivedAtUtc,
    };
}
