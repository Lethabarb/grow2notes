using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-item-groups/{groupId}</c> (design.md §6.6): renames a group of the signed-in manager's
/// organisation, as <see cref="CommonItemGroupChanges.RenameAsync"/> renames it, and answers <c>200</c> with no body
/// and no <c>ETag</c>, the same when the name was already stored (D84), as the item <c>PUT</c> does, since the screen
/// reads the group's next version from the list, which it reads again after every write (T07's Notes); <c>404</c> for
/// a group not in the organisation; <c>428</c> or <c>412</c> for a missing or stale <c>If-Match</c>;
/// <c>422 config.every_note_fixed</c> for Every note; or <c>422 validation.failed</c> with the name's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither renames a group.
/// </remarks>
internal static class RenameCommonItemGroupEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapRenameCommonItemGroupEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/common-item-groups/{groupId:guid}", RenameAsync);
        return admin;
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> RenameAsync(
        Guid groupId,
        CommonItemGroupNameRequest request,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemGroupChanges groups,
        CancellationToken cancellationToken)
    {
        var result = await groups.RenameAsync(
            Actor.InApp(user, identityOptions.Value), groupId, httpRequest.Headers.IfMatch, request, cancellationToken);
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        if (result.Error is { } error)
        {
            return error.Problem();
        }

        return result.Errors is { } errors ? ValidationProblems.Fields(errors) : TypedResults.Ok();
    }
}
