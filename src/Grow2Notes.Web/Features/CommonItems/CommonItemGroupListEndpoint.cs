using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>GET /api/admin/common-item-groups?includeArchived=</c> (design.md §6.6): the signed-in manager's organisation's
/// common item groups with their items, as <see cref="CommonItemGroupList"/> lists them, for the Common items screen
/// (§4.9), which asks for the archived ones too and shows every group from the one list.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>. It answers no <c>ETag</c>, as each group and item in the list
/// carries its own token (D78), and writes no audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class CommonItemGroupListEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapCommonItemGroupListEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapGet("/common-item-groups", ListAsync);
        return admin;
    }

    private static async Task<Ok<IReadOnlyList<AdminCommonItemGroup>>> ListAsync(
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemGroupList groups,
        CancellationToken cancellationToken,
        bool includeArchived = false) =>
        TypedResults.Ok(await groups.ListAsync(
            Actor.InApp(user, identityOptions.Value), includeArchived, cancellationToken));
}
