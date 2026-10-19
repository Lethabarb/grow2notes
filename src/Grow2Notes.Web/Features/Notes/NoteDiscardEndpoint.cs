using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>POST /api/participants/{participantId}/notes/{noteDate}/discard</c> (design.md §6.3), Discard draft, through
/// <see cref="NoteDiscard"/>: discards the participant's draft for the day, for its author or a manager, and answers
/// <c>204</c>. Otherwise it answers <c>404</c> for a participant not in the organisation or a day with no note;
/// <c>409 note.not_a_draft</c> for a submitted note or a draft already discarded (§6.7, §6.9); or a <c>403</c> problem
/// with no code for a worker who did not start the draft, as §6.9 has none for it and no screen offers it to them.
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. It reads no body. The app maps no <c>DELETE</c> on a note: a discard
/// keeps the note and its content (A29).
/// </remarks>
internal static class NoteDiscardEndpoint
{
    /// <summary>
    /// Maps it in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not a GUID, or
    /// a date that is not a date written <c>yyyy-MM-dd</c> (<see cref="IsoDateRouteConstraint"/>), matches no route, so
    /// it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteDiscardEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/participants/{participantId:guid}/notes/{noteDate:isodate}/discard", PostAsync);
        return api;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> PostAsync(
        Guid participantId,
        DateOnly noteDate,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        NoteDiscard discard,
        CancellationToken cancellationToken)
    {
        var result = await discard.DiscardAsync(
            Actor.InApp(user, identityOptions.Value), participantId, noteDate, cancellationToken);
        return result switch
        {
            NoteDiscardResult.Discarded => TypedResults.NoContent(),
            NoteDiscardResult.NotTheAuthor => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden),
            NoteDiscardResult.NotADraft => ErrorCode.NotADraft.Problem(),
            _ => TypedResults.NotFound(),
        };
    }
}
