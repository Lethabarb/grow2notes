using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>PUT /api/admin/participants/{id}</c> (design.md §6.6): corrects a participant of the signed-in manager's
/// organisation, as <see cref="ParticipantChanges.UpdateAsync"/> corrects them, and answers <c>200</c> with no body,
/// whether a value changed or not (D84); <c>404</c> for an ID not in the organisation; <c>428</c> or <c>412</c> for a
/// missing or stale <c>If-Match</c>; or <c>422 validation.failed</c> with each refused field's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither changes anyone. It answers
/// no version: the screen reads the participant's next one from the list (D78, participants.md).
/// </remarks>
internal static class UpdateParticipantEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns. An ID that
    /// is not a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapUpdateParticipantEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/participants/{id:guid}", UpdateAsync);
        return admin;
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> UpdateAsync(
        Guid id,
        ParticipantDetailsRequest request,
        HttpRequest httpRequest,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        ParticipantChanges participants,
        CancellationToken cancellationToken)
    {
        var result = await participants.UpdateAsync(
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
