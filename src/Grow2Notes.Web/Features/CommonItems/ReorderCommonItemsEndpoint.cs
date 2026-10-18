using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-item-groups/{groupId}/items/order</c> (design.md §6.6): puts the active items of a group of
/// the signed-in manager's organisation in the order sent, as <see cref="CommonItemChanges.ReorderAsync"/> puts them,
/// and answers <c>204</c>, whether the order changed or was already stored (D84); <c>404</c> for a group not in the
/// organisation; or <c>422 config.order_mismatch</c> when the IDs are not the group's active items, each once, or an
/// item changed while they were being reordered.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither reorders any item. It
/// takes no <c>If-Match</c>, as the set of active IDs is its check (§6.7), and answers no version: the screen reads
/// each item's next one from the list, which it reads again after every write (T06's Notes).
/// </remarks>
internal static class ReorderCommonItemsEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapReorderCommonItemsEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/common-item-groups/{groupId:guid}/items/order", ReorderAsync);
        return admin;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ReorderAsync(
        Guid groupId,
        CommonItemOrderRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemChanges items,
        CancellationToken cancellationToken)
    {
        var result = await items.ReorderAsync(
            Actor.InApp(user, identityOptions.Value), groupId, request.CommonItemIds, cancellationToken);
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        return result.Error is { } error ? error.Problem() : TypedResults.NoContent();
    }
}
