using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-item-groups/{groupId}/items</c> (design.md §6.6): adds an item at the end of a group
/// of the signed-in manager's organisation, as <see cref="CommonItemChanges.AddAsync"/> adds it, and answers
/// <c>201</c> with no body and no <c>Location</c>, as the goals' add does, the screen reading the list again after
/// every write (T06's Notes); <c>404</c> for a group not in the organisation; <c>409 config.group_archived</c> for an
/// archived group; or <c>422 validation.failed</c> with the text's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither adds an item. It has no
/// idempotency key (§6.6), so the SPA never sends it again by itself.
/// </remarks>
internal static class AddCommonItemEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapAddCommonItemEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPost("/common-item-groups/{groupId:guid}/items", AddAsync);
        return admin;
    }

    private static async Task<Results<Created, NotFound, ProblemHttpResult>> AddAsync(
        Guid groupId,
        CommonItemTextRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemChanges items,
        CancellationToken cancellationToken)
    {
        var result = await items.AddAsync(
            Actor.InApp(user, identityOptions.Value), groupId, request, cancellationToken);
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        if (result.Error is { } error)
        {
            return error.Problem();
        }

        return result.Errors is { } errors ? ValidationProblems.Fields(errors) : TypedResults.Created();
    }
}
