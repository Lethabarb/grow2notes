using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Today;

/// <summary>
/// <c>GET /api/today</c> (design.md §6.3): the Today list (§4.2), as a <see cref="TodayList"/>, for everyone's home
/// screen.
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. The tenant query filter keeps the list to the caller's organisation
/// (§5.9 item 2), and it writes no audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class TodayEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapTodayEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/today", GetAsync);
        return api;
    }

    // The active participants only, as an archived one is not on Today (§3.7), in SQL Server's collation, as the list
    // is shown; the ID last, as the admin list breaks ties, so that two people of one name keep one order from one
    // call to the next.
    private static async Task<Ok<TodayList>> GetAsync(
        Grow2NotesDbContext db, MelbourneClock clock, CancellationToken cancellationToken)
    {
        var participants = await db.Participants
            .AsNoTracking()
            .Where(participant => participant.Status == ParticipantStatus.Active)
            .OrderBy(participant => participant.FamilyName)
            .ThenBy(participant => participant.GivenName)
            .ThenBy(participant => participant.Id)
            .Select(participant => new TodayParticipant(participant.Id, participant.GivenName, participant.FamilyName))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new TodayList(clock.Today(), participants));
    }
}
