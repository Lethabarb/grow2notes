using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// <c>GET /api/admin/participants?includeArchived=</c> (design.md §6.6): the signed-in manager's organisation's
/// participants, as <see cref="ParticipantList"/> lists them, for the Participants screens (§4.8), which ask for the
/// archived ones too and show each view from the one list.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>. It answers no <c>ETag</c>, as each participant in the list
/// carries their own token (D78), and writes no audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class ParticipantListEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapParticipantListEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapGet("/participants", ListAsync);
        return admin;
    }

    private static async Task<Ok<IReadOnlyList<AdminParticipant>>> ListAsync(
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        ParticipantList participants,
        CancellationToken cancellationToken,
        bool includeArchived = false) =>
        TypedResults.Ok(await participants.ListAsync(
            Actor.InApp(user, identityOptions.Value), includeArchived, cancellationToken));
}
