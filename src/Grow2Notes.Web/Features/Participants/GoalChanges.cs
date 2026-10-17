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
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/participants/{participantId}/goals</c>
/// and <c>PUT /api/admin/goals/{goalId}</c> call it with <see cref="Actor.InApp"/>, and Release 2's tools will call it
/// with <see cref="Channel.Mcp"/>. Each change is saved with its audit event through
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
    /// organisation's is not found (design.md §9.2), with the text that <see cref="GoalText.TryRead"/> reads from
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

        if (!GoalText.TryRead(request, out var text, out var errors))
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
    /// <paramref name="request"/> by the add's rules (<see cref="GoalText.TryRead"/>); then saves it with
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
    public async Task<GoalChangeResult> UpdateAsync(
        Actor actor, Guid goalId, StringValues ifMatch, GoalTextRequest request, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(g => g.Id == goalId, cancellationToken);
        if (goal is null)
        {
            return GoalChangeResult.NotFound;
        }

        if (IfMatch.Check(ifMatch, RowVersionToken.Of(goal)) is { } refused)
        {
            return GoalChangeResult.Refused(refused);
        }

        if (!GoalText.TryRead(request, out var text, out var errors))
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

        // EF Core reads back the version that SQL Server gave the row as it saved it, so after a save this is the new
        // ETag, and when nothing changed it is the one the If-Match matched.
        return saved.Error is { } error
            ? GoalChangeResult.Refused(error)
            : GoalChangeResult.Reworded(RowVersionToken.ETag(goal));
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
}
