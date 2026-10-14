using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// <c>POST /api/admin/users</c> (design.md §4.11 <i>Invite user</i>, §6.6): invites a person into the signed-in
/// manager's organisation as <see cref="UserInvite"/> does, and answers <c>201</c> with no body, as Invite user goes
/// back to the list, not to the new user (T01's Notes); or the handler's refusal, <c>422 validation.failed</c> naming
/// each field at fault or <c>409 user.email_in_use</c>.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>, before anything is read or created. An email that could not
/// be sent throws <see cref="Platform.Email.EmailNotSentException"/>, which the exception handler answers with a
/// <c>500</c> problem, with nothing created.
/// </remarks>
internal static class InviteUserEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapInviteUserEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPost("/users", InviteAsync);
        return admin;
    }

    private static async Task<Results<Created, ProblemHttpResult>> InviteAsync(
        InviteUserRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        UserInvite invite,
        CancellationToken cancellationToken)
    {
        var result = await invite.InviteAsync(Actor.InApp(user, identityOptions.Value), request, cancellationToken);
        return result.Error switch
        {
            null => TypedResults.Created(),
            var error when error == ErrorCode.ValidationFailed => ValidationProblems.Fields(result.FieldErrors),
            var error => error.Problem(),
        };
    }
}
