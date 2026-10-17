using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>POST /api/admin/participants/{id}/archive</c> and <c>/restore</c> (design.md §6.6): archive or restore a
/// participant of the signed-in manager's organisation, as <see cref="ParticipantChanges.ArchiveAsync"/> and
/// <see cref="ParticipantChanges.RestoreAsync"/> do, never deleting one (A29). Each answers <c>204</c>, whether it
/// changed the participant or found them so already (D84); <c>404</c> for an ID not in the organisation; or
/// <c>428</c> or <c>412</c> for a missing or stale <c>If-Match</c>.
/// </summary>
/// <remarks>
/// They are mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither changes anyone. They take
/// no body, and answer no version: the screen reads the participant's next one from the list (D78, participants.md).
/// </remarks>
internal static class ArchiveParticipantEndpoints
{
    /// <summary>
    /// Maps both in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that is not
    /// a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapArchiveParticipantEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapPost("/participants/{id:guid}/archive", ArchiveAsync);
        admin.MapPost("/participants/{id:guid}/restore", RestoreAsync);
        return admin;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ArchiveAsync(
        Guid id,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        ParticipantChanges participants,
        CancellationToken cancellationToken) =>
        Answer(await participants.ArchiveAsync(
            Actor.InApp(user, identityOptions.Value), id, httpRequest.Headers.IfMatch, cancellationToken));

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> RestoreAsync(
        Guid id,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        ParticipantChanges participants,
        CancellationToken cancellationToken) =>
        Answer(await participants.RestoreAsync(
            Actor.InApp(user, identityOptions.Value), id, httpRequest.Headers.IfMatch, cancellationToken));

    private static Results<NoContent, NotFound, ProblemHttpResult> Answer(ParticipantChangeResult result)
    {
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        return result.Error is { } error ? error.Problem() : TypedResults.NoContent();
    }
}
