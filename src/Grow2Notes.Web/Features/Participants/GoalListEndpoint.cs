using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>GET /api/admin/participants/{participantId}/goals?includeArchived=</c> (design.md §6.6): the goals of a
/// participant of the signed-in manager's organisation, as <see cref="GoalList"/> lists them, for the Goals section of
/// their page (§4.8), which asks for the archived ones too; or <c>404</c> for an ID not in the organisation.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>. It answers no <c>ETag</c>, as each goal in the list carries
/// its own token (D78), and writes no audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class GoalListEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapGoalListEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapGet("/participants/{participantId:guid}/goals", ListAsync);
        return admin;
    }

    private static async Task<Results<Ok<IReadOnlyList<AdminGoal>>, NotFound>> ListAsync(
        Guid participantId,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        GoalList goals,
        CancellationToken cancellationToken,
        bool includeArchived = false)
    {
        var list = await goals.ListAsync(
            Actor.InApp(user, identityOptions.Value), participantId, includeArchived, cancellationToken);
        return list is null ? TypedResults.NotFound() : TypedResults.Ok(list);
    }
}
