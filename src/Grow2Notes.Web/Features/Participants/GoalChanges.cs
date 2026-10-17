using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The handler that changes a participant's goals (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/participants/{participantId}/goals</c>
/// calls it with <see cref="Actor.InApp"/>, and Release 2's tools will call it with <see cref="Channel.Mcp"/>. Each
/// change is saved with its audit event through <see cref="ConfigurationChanges"/>, in the tenant's organisation.
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
}
