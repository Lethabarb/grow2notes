using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// <c>POST /api/admin/users/{id}/setup-link</c> (design.md §4.11 <i>Resend invite</i>, §6.6): sends an Invited user of
/// the signed-in manager's organisation a new setup link, as <see cref="UserSetupLink"/> does, and answers <c>204</c>
/// with no <c>ETag</c>, as the screen takes the user's next one from the refetched list (D78); or <c>404</c> for a user
/// not in the organisation, <c>428 precondition.required</c> or <c>412 precondition.failed</c> for the
/// <c>If-Match</c> the request sent, or <c>409 user.not_active</c> for a user who is not Invited.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>, before anything is read or changed. The ID is a GUID by the
/// route's constraint, so any other path is the unknown-path <c>404</c>. An email that could not be sent throws
/// <see cref="Platform.Email.EmailNotSentException"/>, which the exception handler answers with a <c>500</c> problem,
/// with nothing changed.
/// </remarks>
internal static class UserSetupLinkEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapUserSetupLinkEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPost("/users/{id:guid}/setup-link", SendAsync);
        return admin;
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> SendAsync(
        Guid id,
        HttpRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        UserSetupLink setupLink,
        CancellationToken cancellationToken)
    {
        var result = await setupLink.SendAsync(
            Actor.InApp(user, identityOptions.Value), id, request.Headers.IfMatch, cancellationToken);
        return result switch
        {
            { IsNotFound: true } => TypedResults.NotFound(),
            { Error: null } => TypedResults.NoContent(),
            { Error: { } error } => error.Problem(),
        };
    }
}
