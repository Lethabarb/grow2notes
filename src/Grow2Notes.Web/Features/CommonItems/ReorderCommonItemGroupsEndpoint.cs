using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-item-groups/order</c> (design.md §6.6): puts the active groups of the signed-in manager's
/// organisation other than Every note in the order sent, as <see cref="CommonItemGroupChanges.ReorderAsync"/> puts
/// them, and answers <c>204</c>, whether the order changed or was already stored (D84); or
/// <c>422 config.order_mismatch</c> when the IDs are not those groups, each once, Every note's among them, or a group
/// changed while they were being reordered.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither reorders any group. It
/// takes no <c>If-Match</c>, as the set of active IDs is its check (§6.7), and answers no version: the screen reads
/// each group's next one from the list, which it reads again after every write (T07's Notes). Its path never matches
/// a group's rename, whose route takes a GUID alone.
/// </remarks>
internal static class ReorderCommonItemGroupsEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapReorderCommonItemGroupsEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/common-item-groups/order", ReorderAsync);
        return admin;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ReorderAsync(
        CommonItemGroupOrderRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemGroupChanges groups,
        CancellationToken cancellationToken)
    {
        var result = await groups.ReorderAsync(
            Actor.InApp(user, identityOptions.Value), request.GroupIds, cancellationToken);
        return result.Error is { } error ? error.Problem() : TypedResults.NoContent();
    }
}
