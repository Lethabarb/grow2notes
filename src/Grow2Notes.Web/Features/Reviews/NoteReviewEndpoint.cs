using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// <c>POST /api/participants/{participantId}/notes/{noteDate}/reviews</c> (design.md §6.4), Mark reviewed, through
/// <see cref="NoteReviews"/>: reviews the note's current version from a <see cref="NoteReviewRequest"/> and answers
/// <c>201 {reviewedAtUtc}</c>. Otherwise it answers <c>404</c> for a participant not in the organisation or a day with
/// no live note; <c>409 review.not_current</c> for a note that is not in To review at that version (§6.9); or
/// <c>422 validation.failed</c> for a field it refuses.
/// </summary>
/// <remarks>
/// It has the <see cref="Policies.Manager"/> policy of its own, which gives a worker a <c>403</c> and a request with no
/// session the session cookie's <c>401</c>. A review has no <c>Idempotency-Key</c>: its conditional update makes a
/// second try impossible, so a repeated press gets the <c>409</c>.
/// </remarks>
internal static class NoteReviewEndpoint
{
    /// <summary>
    /// Maps it in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not a GUID, or
    /// a date that is not a date written <c>yyyy-MM-dd</c> (<see cref="IsoDateRouteConstraint"/>), matches no route, so
    /// it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteReviewEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/participants/{participantId:guid}/notes/{noteDate:isodate}/reviews", PostAsync)
            .RequireAuthorization(Policies.Manager);
        return api;
    }

    // A review has no address of its own, so a 201 names no Location.
    private static async Task<Results<Created<NoteReviewed>, NotFound, ProblemHttpResult>> PostAsync(
        Guid participantId,
        DateOnly noteDate,
        NoteReviewRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        NoteReviews reviews,
        CancellationToken cancellationToken)
    {
        var result = await reviews.ReviewAsync(
            Actor.InApp(user, identityOptions.Value), participantId, noteDate, request, cancellationToken);
        if (result.ReviewedAtUtc is { } reviewedAtUtc)
        {
            return TypedResults.Created((string?)null, new NoteReviewed(reviewedAtUtc));
        }

        return result.IsNotCurrent ? ErrorCode.ReviewNotCurrent.Problem() : TypedResults.NotFound();
    }
}
