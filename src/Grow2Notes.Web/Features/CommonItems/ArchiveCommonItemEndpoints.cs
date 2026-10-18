using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-items/{id}/archive</c> and <c>/restore</c> (design.md §6.6): archive or restore a common
/// item of the signed-in manager's organisation, as <see cref="CommonItemChanges.ArchiveAsync"/> and
/// <see cref="CommonItemChanges.RestoreAsync"/> do, never deleting one (A2, A29). Each answers <c>204</c>, whether it
/// changed the item or found it so already (D84); <c>404</c> for an item not in the organisation; or <c>428</c> or
/// <c>412</c> for a missing or stale <c>If-Match</c>.
/// </summary>
/// <remarks>
/// They are mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither changes any item. They
/// take no body, and answer no version: the screen reads each item's next one from the list, which it reads again
/// after every write (T06's Notes).
/// </remarks>
internal static class ArchiveCommonItemEndpoints
{
    /// <summary>
    /// Maps both in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that is not
    /// a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapArchiveCommonItemEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapPost("/common-items/{id:guid}/archive", ArchiveAsync);
        admin.MapPost("/common-items/{id:guid}/restore", RestoreAsync);
        return admin;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ArchiveAsync(
        Guid id,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemChanges items,
        CancellationToken cancellationToken) =>
        Answer(await items.ArchiveAsync(
            Actor.InApp(user, identityOptions.Value), id, httpRequest.Headers.IfMatch, cancellationToken));

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> RestoreAsync(
        Guid id,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemChanges items,
        CancellationToken cancellationToken) =>
        Answer(await items.RestoreAsync(
            Actor.InApp(user, identityOptions.Value), id, httpRequest.Headers.IfMatch, cancellationToken));

    private static Results<NoContent, NotFound, ProblemHttpResult> Answer(CommonItemChangeResult result)
    {
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        return result.Error is { } error ? error.Problem() : TypedResults.NoContent();
    }
}
