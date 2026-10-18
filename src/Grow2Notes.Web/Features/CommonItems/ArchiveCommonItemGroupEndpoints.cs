using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-item-groups/{groupId}/archive</c> and <c>/restore</c> (design.md §6.6): archive or restore
/// a common item group of the signed-in manager's organisation, as <see cref="CommonItemGroupChanges.ArchiveAsync"/>
/// and <see cref="CommonItemGroupChanges.RestoreAsync"/> do, never deleting one (A2, A29). Each answers <c>204</c>,
/// whether it changed the group or found it so already (D84); <c>404</c> for a group not in the organisation;
/// <c>428</c> or <c>412</c> for a missing or stale <c>If-Match</c>; or <c>422 config.every_note_fixed</c> for Every
/// note.
/// </summary>
/// <remarks>
/// They are mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither changes any group. They
/// take no body, and answer no version: the screen reads each group's next one from the list, which it reads again
/// after every write (T07's Notes).
/// </remarks>
internal static class ArchiveCommonItemGroupEndpoints
{
    /// <summary>
    /// Maps both in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that is not
    /// a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapArchiveCommonItemGroupEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapPost("/common-item-groups/{groupId:guid}/archive", ArchiveAsync);
        admin.MapPost("/common-item-groups/{groupId:guid}/restore", RestoreAsync);
        return admin;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ArchiveAsync(
        Guid groupId,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemGroupChanges groups,
        CancellationToken cancellationToken) =>
        Answer(await groups.ArchiveAsync(
            Actor.InApp(user, identityOptions.Value), groupId, httpRequest.Headers.IfMatch, cancellationToken));

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> RestoreAsync(
        Guid groupId,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemGroupChanges groups,
        CancellationToken cancellationToken) =>
        Answer(await groups.RestoreAsync(
            Actor.InApp(user, identityOptions.Value), groupId, httpRequest.Headers.IfMatch, cancellationToken));

    private static Results<NoContent, NotFound, ProblemHttpResult> Answer(CommonItemChangeResult result)
    {
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        return result.Error is { } error ? error.Problem() : TypedResults.NoContent();
    }
}
