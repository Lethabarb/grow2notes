using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The handler that lists an organisation's participants (design.md §6.6), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>GET /api/admin/participants</c> calls it with
/// <see cref="Actor.InApp"/>.
/// </summary>
internal sealed class ParticipantList(Grow2NotesDbContext db)
{
    /// <summary>
    /// The tenant's participants, the active ones, or with <paramref name="includeArchived"/> the archived ones too, as
    /// <see cref="AdminParticipant"/>s, sorted by family name, then given name (A5), in SQL Server's collation, as the
    /// list is shown; the client filters it but never sorts it again. The list is whole, as design.md §6.1 returns
    /// lists.
    /// </summary>
    /// <remarks>
    /// The tenant query filter keeps the query to the tenant's organisation (design.md §5.9 item 2). Nothing here uses
    /// the actor: reading writes no audit event (§5.4, A28). It is taken so that every admin handler has the same
    /// shape.
    /// </remarks>
    public async Task<IReadOnlyList<AdminParticipant>> ListAsync(
        Actor actor, bool includeArchived, CancellationToken cancellationToken)
    {
        var participants = db.Participants.AsNoTracking();
        if (!includeArchived)
        {
            participants = participants.Where(participant => participant.Status == ParticipantStatus.Active);
        }

        // The ID last, so that two people of the same name keep one order from one call to the next.
        var sorted = await participants
            .OrderBy(participant => participant.FamilyName)
            .ThenBy(participant => participant.GivenName)
            .ThenBy(participant => participant.Id)
            .ToListAsync(cancellationToken);
        return [.. sorted.Select(AdminParticipant.Of)];
    }
}
