using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET /api/participants/{participantId}/notes/{noteDate}/draft</c> (design.md §6.3): the note form with the
/// signed-in user's working copy, as <see cref="NoteDrafts.ReadAsync"/> reads it, as a <see cref="DraftForm"/>;
/// <c>409 note.in_progress_by_other</c>, with the author's name and start time, for another user's draft; or
/// <c>404</c> for a participant not in the organisation, or a date after today with no note.
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. Opening the form writes nothing (§3.3).
/// </remarks>
internal static class NoteDraftEndpoints
{
    /// <summary>
    /// Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not
    /// a GUID, or a date that is not a date written <c>yyyy-MM-dd</c> (<see cref="IsoDateRouteConstraint"/>), matches
    /// no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteDraftEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/participants/{participantId:guid}/notes/{noteDate:isodate}/draft", GetAsync);
        return api;
    }

    private static async Task<Results<Ok<DraftForm>, NotFound, ProblemHttpResult>> GetAsync(
        Guid participantId,
        DateOnly noteDate,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        NoteDrafts drafts,
        CancellationToken cancellationToken)
    {
        var read = await drafts.ReadAsync(
            Actor.InApp(user, identityOptions.Value), participantId, noteDate, cancellationToken);
        if (read.Form is { } form)
        {
            return TypedResults.Ok(form);
        }

        return read.InProgress is { } inProgress ? inProgress.Problem() : TypedResults.NotFound();
    }
}
