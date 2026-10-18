using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-item-groups</c> (design.md §6.6): adds a group, with no items, at the end of the
/// signed-in manager's organisation's groups, as <see cref="CommonItemGroupChanges.AddAsync"/> adds it, and answers
/// <c>201</c> with its new ID in the body and no <c>Location</c>, as Add participant does (D79), as the screen moves
/// focus to the new group's New item field (T07's Notes); or <c>422 validation.failed</c> with the name's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither adds a group. It has no
/// idempotency key (§6.6), so the SPA never sends it again by itself.
/// </remarks>
internal static class AddCommonItemGroupEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapAddCommonItemGroupEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPost("/common-item-groups", AddAsync);
        return admin;
    }

    private static async Task<Results<Created<AddedCommonItemGroup>, ProblemHttpResult>> AddAsync(
        CommonItemGroupNameRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        CommonItemGroupChanges groups,
        CancellationToken cancellationToken)
    {
        var result = await groups.AddAsync(Actor.InApp(user, identityOptions.Value), request, cancellationToken);
        return result.Errors is { } errors
            ? ValidationProblems.Fields(errors)
            : TypedResults.Created((string?)null, new AddedCommonItemGroup(result.Id));
    }
}
