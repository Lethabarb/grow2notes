using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET</c> and <c>PUT /api/participants/{participantId}/notes/{noteDate}/draft</c> (design.md §6.3), each through
/// <see cref="NoteDrafts"/>. The <c>GET</c> gives the note form with the signed-in user's working copy, as a
/// <see cref="DraftForm"/>; or, with no note, <c>404</c> for a date after today and, to a worker, for a day before
/// yesterday (design.md A65). The <c>PUT</c>, the autosave, saves their working copy from a
/// <see cref="DraftSaveRequest"/>, and answers <c>{savedAtUtc}</c>: <c>201</c> when it created the note, at its first
/// save, and <c>200</c> after; or, for a first save that may not start the note,
/// <c>422 note.future_date</c>, <c>422 note.past_date_manager_only</c>, <c>422 note.participant_archived</c> or
/// <c>409 note.lists_changed</c>; or <c>409 note.version_conflict</c> for a submitted note, on which the caller has no
/// working copy (§6.7); or <c>422 validation.failed</c> for a field it refuses. Both answer
/// <c>409 note.in_progress_by_other</c>, with the author's name and start time, for another user's draft, and
/// <c>404</c> for a participant not in the organisation.
/// </summary>
/// <remarks>
/// They have no policy of their own, so the fallback policy admits any signed-in Worker or Manager, and a request with
/// no session gets the session cookie's <c>401</c>. Opening the form writes nothing (§3.3).
/// </remarks>
internal static class NoteDraftEndpoints
{
    /// <summary>
    /// Maps both in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not a GUID,
    /// or a date that is not a date written <c>yyyy-MM-dd</c> (<see cref="IsoDateRouteConstraint"/>), matches no route,
    /// so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteDraftEndpoints(this RouteGroupBuilder api)
    {
        const string route = "/participants/{participantId:guid}/notes/{noteDate:isodate}/draft";
        api.MapGet(route, GetAsync);
        api.MapPut(route, PutAsync);
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

    // The note's address is the draft's own, so a 201 names no Location.
    private static async Task<Results<Created<DraftSaved>, Ok<DraftSaved>, NotFound, ProblemHttpResult>> PutAsync(
        Guid participantId,
        DateOnly noteDate,
        DraftSaveRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        NoteDrafts drafts,
        CancellationToken cancellationToken)
    {
        var result = await drafts.SaveAsync(
            Actor.InApp(user, identityOptions.Value), participantId, noteDate, request, cancellationToken);
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
