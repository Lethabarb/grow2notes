using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>POST /api/participants/{participantId}/notes/{noteDate}/versions</c> (design.md §6.3), Submit, through
/// <see cref="NoteVersions"/>: saves version 1 of the signed-in user's own draft from a
/// <see cref="NoteVersionRequest"/> and its <c>Idempotency-Key</c> header, and answers
/// <c>{versionNumber, createdAtUtc}</c>, <c>201</c>, or <c>200</c> for a retry with a key that a version of the note
/// already has, which changes nothing. Otherwise it answers
/// <c>404</c> for a participant not in the organisation or no live note; <c>409 note.in_progress_by_other</c>, with
/// the author's name and start time, for another user's draft; <c>409 note.version_conflict</c> for a submitted note;
/// or <c>422 validation.failed</c> for a field it refuses, or under <c>idempotencyKey</c> for a key that a version of
/// another note has.
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. A request whose <c>Idempotency-Key</c> is missing or is not a GUID
/// gets the framework's <c>400</c>, before the body is validated or the endpoint runs.
/// </remarks>
internal static class NoteVersionEndpoints
{
    /// <summary>The header that names a version save, so that a retry of it finds the version (§5.3).</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Maps it in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not a GUID, or
    /// a date that is not a date written <c>yyyy-MM-dd</c> (<see cref="IsoDateRouteConstraint"/>), matches no route, so
    /// it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteVersionEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/participants/{participantId:guid}/notes/{noteDate:isodate}/versions", PostAsync);
        return api;
    }

    // A version's own address is a manager's, from T18, so a 201 names no Location.
    private static async Task<Results<Created<NoteVersionSaved>, Ok<NoteVersionSaved>, NotFound, ProblemHttpResult>>
        PostAsync(
            Guid participantId,
            DateOnly noteDate,
            [FromHeader(Name = IdempotencyKeyHeader)] Guid idempotencyKey,
            NoteVersionRequest request,
            ClaimsPrincipal user,
            IOptions<IdentityOptions> identityOptions,
            NoteVersions versions,
            CancellationToken cancellationToken)
    {
        var result = await versions.SaveAsync(
            Actor.InApp(user, identityOptions.Value), participantId, noteDate, idempotencyKey, request,
            cancellationToken);
        if (result.Saved is { } saved)
        {
            return result.Created ? TypedResults.Created((string?)null, saved) : TypedResults.Ok(saved);
        }

        if (result.InProgress is { } inProgress)
        {
            return inProgress.Problem();
        }

        if (result.Refusal is { } refusal)
        {
            return refusal.Problem();
        }

        return result.Errors is { } errors ? ValidationProblems.Fields(errors) : TypedResults.NotFound();
    }
}
