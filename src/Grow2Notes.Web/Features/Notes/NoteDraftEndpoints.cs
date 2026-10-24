using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET</c>, <c>PUT</c> and <c>DELETE /api/participants/{participantId}/notes/{noteDate}/draft</c> (design.md §6.3),
/// each through <see cref="NoteDrafts"/>. The <c>GET</c> gives the note form with the signed-in user's working copy, as
/// a <see cref="DraftForm"/>: a draft, or, on a submitted note, their pending edit or, with none, the template of its
/// current version; or, with no note, <c>404</c> for a date after today and, to a worker, for a day before yesterday
/// (design.md A65). The <c>PUT</c>, the autosave, saves their working copy from a <see cref="DraftSaveRequest"/>, and
/// answers <c>{savedAtUtc}</c>: <c>201</c> when it created the note, at its first save, and <c>200</c> after, also
/// with the stored time for a save that the working copy holds already, which changes nothing (§5.6), and for the save
/// that creates a pending edit; or, for a first save that may not start the note,
/// <c>422 note.participant_archived</c>, <c>422 note.future_date</c>, <c>422 note.past_date_manager_only</c> or
/// <c>409 note.lists_changed</c>; or <c>409 note.version_conflict</c>, with no extensions, for a base that the note
/// does not accept (§6.7); or <c>409 draft.taken_over</c> for any save but the first from a page other than the one
/// that saved last (§5.6); or <c>422 validation.failed</c> for a field it refuses. The <c>DELETE</c>, Cancel, removes
/// the signed-in user's own pending edit and answers <c>204</c>, or <c>404</c> when they have none, with no refusal for
/// any role (A71). <c>GET</c> and <c>PUT</c> answer <c>403 note.not_editable</c> for a submitted note that a worker did
/// not write, and both answer <c>409 note.in_progress_by_other</c>, with the author's name and start time, for another
/// user's draft, and <c>404</c> for a participant not in the organisation.
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
        api.MapDelete(route, DeleteAsync);
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

        if (read.Refusal is { } refusal)
        {
            return refusal.Problem();
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

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid participantId,
        DateOnly noteDate,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        NoteDrafts drafts,
        CancellationToken cancellationToken) =>
        await drafts.CancelAsync(Actor.InApp(user, identityOptions.Value), participantId, noteDate, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.NotFound();
}
