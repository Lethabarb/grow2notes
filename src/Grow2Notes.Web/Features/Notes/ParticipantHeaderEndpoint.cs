using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET /api/participants/{participantId}</c> (design.md §6.3): a participant of the signed-in user's organisation,
/// archived or not, as a <see cref="ParticipantHeader"/>, for the note form's identity heading (§4.3); or <c>404</c>
/// for an ID not in the organisation, another organisation's included (§9.2).
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. The tenant query filter keeps it to the caller's organisation (§5.9
/// item 2), and it writes no audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class ParticipantHeaderEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not
    /// a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapParticipantHeaderEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/participants/{participantId:guid}", GetAsync);
        return api;
    }

    private static async Task<Results<Ok<ParticipantHeader>, NotFound>> GetAsync(
        Guid participantId, Grow2NotesDbContext db, CancellationToken cancellationToken)
    {
        var header = await db.Participants
            .AsNoTracking()
            .Where(participant => participant.Id == participantId)
            .Select(participant => new ParticipantHeader(
                participant.Id,
                participant.GivenName,
                participant.FamilyName,
                participant.Status.ToString(),
                db.Notes
                    .Where(n => n.ParticipantId == participant.Id && n.Status == NoteStatus.Submitted)
                    .Min(n => (DateOnly?)n.NoteDate)))
            .SingleOrDefaultAsync(cancellationToken);
        return header is null ? TypedResults.NotFound() : TypedResults.Ok(header);
    }
}
