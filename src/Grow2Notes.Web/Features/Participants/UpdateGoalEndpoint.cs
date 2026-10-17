using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>PUT /api/admin/goals/{goalId}</c> (design.md §6.6): rewords a goal of the signed-in manager's organisation, as
/// <see cref="GoalChanges.UpdateAsync"/> rewords it, and answers <c>200</c> with no body and the goal's <c>ETag</c>,
/// the same one when the text was already stored (D84, S01.02.06's Notes); <c>404</c> for a goal not in the
/// organisation; <c>428</c> or <c>412</c> for a missing or stale <c>If-Match</c>; or <c>422 validation.failed</c> with
/// the text's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither rewords a goal. The screen
/// reads the goal's next version from the list, which it reads again after every write (T05's Notes).
/// </remarks>
internal static class UpdateGoalEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapUpdateGoalEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/goals/{goalId:guid}", UpdateAsync);
        return admin;
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> UpdateAsync(
        Guid goalId,
        GoalTextRequest request,
        HttpRequest httpRequest,
        HttpResponse response,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        GoalChanges goals,
        CancellationToken cancellationToken)
    {
        var result = await goals.UpdateAsync(
            Actor.InApp(user, identityOptions.Value), goalId, httpRequest.Headers.IfMatch, request, cancellationToken);
        if (!result.Found)
        {
            return TypedResults.NotFound();
        }

        if (result.Error is { } error)
        {
            return error.Problem();
        }

        if (result.Errors is { } errors)
        {
            return ValidationProblems.Fields(errors);
        }

        response.Headers.ETag = result.ETag;
        return TypedResults.Ok();
    }
}
