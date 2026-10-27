using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>POST /api/admin/participants/{participantId}/goals</c> (design.md §6.6): adds a goal at the end of the goals
/// of a participant of the signed-in manager's organisation, as <see cref="GoalChanges.AddAsync"/> adds it, and answers
/// <c>201</c> with no body and no <c>Location</c>, as the screen reads the list again after every write (T05's Notes);
/// <c>404</c> for a participant not in the organisation; <c>422 participant.archived</c> for an archived one (D93); or
/// <c>422 validation.failed</c> with the text's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither adds a goal. It has no
/// idempotency key (§6.6), so the SPA never sends it again by itself.
/// </remarks>
internal static class AddGoalEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapAddGoalEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPost("/participants/{participantId:guid}/goals", AddAsync);
        return admin;
    }

    private static async Task<Results<Created, NotFound, ProblemHttpResult>> AddAsync(
        Guid participantId,
        GoalTextRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        GoalChanges goals,
        CancellationToken cancellationToken)
    {
        var result = await goals.AddAsync(
            Actor.InApp(user, identityOptions.Value), participantId, request, cancellationToken);
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
