using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>POST /api/admin/participants</c> (design.md §6.6): adds a participant to the signed-in manager's organisation,
/// as <see cref="ParticipantChanges.AddAsync"/> adds them, and answers <c>201</c> with their new ID in the body and no
/// <c>Location</c> (D79), or <c>422 validation.failed</c> with each refused field's message.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c> before the handler runs, so neither adds anyone. It has no
/// idempotency key (§6.6), so the SPA never sends it again by itself.
/// </remarks>
internal static class AddParticipantEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapAddParticipantEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPost("/participants", AddAsync);
        return admin;
    }

    private static async Task<Results<Created<AddedParticipant>, ProblemHttpResult>> AddAsync(
        ParticipantDetailsRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        ParticipantChanges participants,
        CancellationToken cancellationToken)
    {
        var result = await participants.AddAsync(
            Actor.InApp(user, identityOptions.Value), request, cancellationToken);
        return result.Errors is { } errors
            ? ValidationProblems.Fields(errors)
            : TypedResults.Created((string?)null, new AddedParticipant(result.Id));
    }
}
