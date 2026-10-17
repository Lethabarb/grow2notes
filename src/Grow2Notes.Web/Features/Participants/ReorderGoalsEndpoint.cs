using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>PUT /api/admin/participants/{participantId}/goals/order</c> (design.md §6.6): puts the active goals of a
/// participant of the signed-in manager's organisation in the order sent, as <see cref="GoalChanges.ReorderAsync"/>
/// puts them, and answers <c>204</c>, whether the order changed or was already stored (D84); <c>404</c> for a
/// participant not in the organisation; or <c>422 config.order_mismatch</c> when the IDs are not the active goals,
/// each once, or a goal changed while they were being reordered.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither reorders any goal. It
/// takes no <c>If-Match</c>, as the set of active IDs is its check (§6.7), and answers no version: the screen reads
/// each goal's next one from the list, which it reads again after every write (T05's Notes).
/// </remarks>
internal static class ReorderGoalsEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapReorderGoalsEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/participants/{participantId:guid}/goals/order", ReorderAsync);
        return admin;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ReorderAsync(
        Guid participantId,
        GoalOrderRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        GoalChanges goals,
        CancellationToken cancellationToken)
    {
        var result = await goals.ReorderAsync(
            Actor.InApp(user, identityOptions.Value), participantId, request.GoalIds, cancellationToken);
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        return result.Error is { } error ? error.Problem() : TypedResults.NoContent();
    }
}
