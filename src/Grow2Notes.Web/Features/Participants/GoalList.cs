using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The handler that lists a participant's goals (design.md §6.6), as a handler class that takes the <see cref="Actor"/>
/// (mcp-server.md §3.5, convention 1): <c>GET /api/admin/participants/{participantId}/goals</c> calls it with
/// <see cref="Actor.InApp"/>.
/// </summary>
internal sealed class GoalList(Grow2NotesDbContext db)
{
    /// <summary>
    /// The goals of a participant of the tenant's organisation, the active ones, or with
    /// <paramref name="includeArchived"/> the archived ones too, as <see cref="AdminGoal"/>s in the list's order; or
    /// <see langword="null"/> when no participant of that ID is in the organisation, another organisation's included
    /// (design.md §9.2). An archived participant's goals are listed as an active one's are (D86).
    /// </summary>
    /// <remarks>
    /// The list's order is <c>ORDER BY SortOrder, CreatedAtUtc, Id</c>, the order in which the reorder reads the goals
    /// (<see cref="Platform.Configuration.ListOrder"/>), as two adds at once can give two goals the same
    /// <c>SortOrder</c>; the client never sorts the active goals again. The tenant query filter keeps both queries to
    /// the tenant's organisation (design.md §5.9 item 2). Nothing here uses the actor: reading writes no audit event
    /// (§5.4, A28). It is taken so that every admin handler has the same shape.
    /// </remarks>
    public async Task<IReadOnlyList<AdminGoal>?> ListAsync(
        Actor actor, Guid participantId, bool includeArchived, CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return null;
        }

        var goals = db.Goals.AsNoTracking().Where(goal => goal.ParticipantId == participantId);
        if (!includeArchived)
        {
            goals = goals.Where(goal => goal.ArchivedAtUtc == null);
        }

        var ordered = await InListOrder(goals).ToListAsync(cancellationToken);
        return [.. ordered.Select(AdminGoal.Of)];
    }

    /// <summary>
    /// <paramref name="goals"/> in the list's order, <c>ORDER BY SortOrder, CreatedAtUtc, Id</c>, in which the list
    /// shows them and the reorder reads them (<see cref="GoalChanges.ReorderAsync"/>), so that the order the screen
    /// sends is checked against the one it showed.
    /// </summary>
    public static IQueryable<Goal> InListOrder(IQueryable<Goal> goals) =>
        goals.OrderBy(goal => goal.SortOrder).ThenBy(goal => goal.CreatedAtUtc).ThenBy(goal => goal.Id);
}
