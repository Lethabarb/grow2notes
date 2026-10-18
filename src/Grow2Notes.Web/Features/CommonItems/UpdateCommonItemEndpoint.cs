using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-items/{id}</c> (design.md §6.6): rewords a common item of the signed-in manager's
/// organisation, and moves it to the end of another active group of the organisation when its body names one, as
/// <see cref="CommonItemChanges.UpdateAsync"/> does; and answers <c>200</c> with no body and no <c>ETag</c>, the same
/// when nothing changed (D84), as §6.6 gives it none and the screen reads the item's next version from the list, which
/// it reads again after every write (T06's Notes); <c>404</c> for an item, or a chosen group, not in the organisation;
/// <c>428</c> or <c>412</c> for a missing or stale <c>If-Match</c>; <c>409 config.group_archived</c> for an item whose
/// group is archived, or an archived chosen group; or <c>422 validation.failed</c> with the text's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither changes an item. Its body
/// is <c>{text, groupId}</c>, whose <c>groupId</c> may be missing, as T06's callers send <c>{text}</c> alone (T07's
/// Notes).
/// </remarks>
internal static class UpdateCommonItemEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapUpdateCommonItemEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/common-items/{id:guid}", UpdateAsync);
        return admin;
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> UpdateAsync(
        Guid id,
        CommonItemUpdateRequest request,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemChanges items,
        CancellationToken cancellationToken)
    {
        var result = await items.UpdateAsync(
            Actor.InApp(user, identityOptions.Value), id, httpRequest.Headers.IfMatch, request, cancellationToken);
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
