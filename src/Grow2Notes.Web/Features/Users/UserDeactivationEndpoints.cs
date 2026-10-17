using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// <c>POST /api/admin/users/{id}/deactivate</c> and <c>…/reactivate</c> (design.md §4.11 <i>Deactivate</i> and
/// <i>Reactivate</i>, §6.6): deactivate or reactivate a user of the signed-in manager's organisation, as
/// <see cref="UserDeactivation"/> does, and answer <c>204</c> with no <c>ETag</c>, as the screen takes the user's next
/// one from the refetched list (D78), the user already in that state included; or <c>404</c> for a user not in the
/// organisation, <c>428 precondition.required</c> or <c>412 precondition.failed</c> for the <c>If-Match</c> the
/// request sent, or <c>409 user.last_manager</c> to deactivate the organisation's only Active manager.
/// </summary>
/// <remarks>
/// They are mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>, before anything is read or changed. The ID is a GUID by the
/// route's constraint, so any other path is the unknown-path <c>404</c>.
/// </remarks>
internal static class UserDeactivationEndpoints
{
    /// <summary>
    /// Maps the endpoints in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapUserDeactivationEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapPost("/users/{id:guid}/deactivate", DeactivateAsync);
        admin.MapPost("/users/{id:guid}/reactivate", ReactivateAsync);
        return admin;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeactivateAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        UserDeactivation deactivation,
        CancellationToken cancellationToken) =>
        Answer(await deactivation.DeactivateAsync(
            Actor.InApp(user, identityOptions.Value), id, request.Headers.IfMatch, cancellationToken));

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ReactivateAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        UserDeactivation deactivation,
        CancellationToken cancellationToken) =>
        Answer(await deactivation.ReactivateAsync(
            Actor.InApp(user, identityOptions.Value), id, request.Headers.IfMatch, cancellationToken));

    private static Results<NoContent, NotFound, ProblemHttpResult> Answer(UserChangeResult result) => result switch
    {
        { IsNotFound: true } => TypedResults.NotFound(),
        { Error: null } => TypedResults.NoContent(),
        { Error: { } error } => error.Problem(),
    };
}
