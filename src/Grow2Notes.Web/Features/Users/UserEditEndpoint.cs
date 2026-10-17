using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// <c>PUT /api/admin/users/{id}</c> (design.md §4.11 <i>Edit</i>, §6.6): changes the name, email address or role of a
/// user of the signed-in manager's organisation, as <see cref="UserEdit"/> does, and answers <c>200</c> with no body
/// and no <c>ETag</c>, as the screen takes the user's next one from the refetched list (D78); or
/// <c>422 validation.failed</c> naming each field at fault, <c>404</c> for a user not in the organisation,
/// <c>428 precondition.required</c> or <c>412 precondition.failed</c> for the <c>If-Match</c> the request sent,
/// <c>409 user.last_manager</c> to make a worker of, or give a new address to, the organisation's only Active manager,
/// or <c>409 user.email_in_use</c> for an address that another account holds.
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>, before anything is read or changed. The ID is a GUID by the
/// route's constraint, so any other path is the unknown-path <c>404</c>. An email that could not be sent throws
/// <see cref="Platform.Email.EmailNotSentException"/>, which the exception handler answers with a <c>500</c> problem,
/// with nothing changed.
/// </remarks>
internal static class UserEditEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapUserEditEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapPut("/users/{id:guid}", EditAsync);
        return admin;
    }

    private static async Task<Results<Ok, NotFound, ProblemHttpResult>> EditAsync(
        Guid id,
        UserDetailsRequest details,
        HttpRequest request,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        UserEdit edit,
        CancellationToken cancellationToken)
    {
        var result = await edit.EditAsync(
            Actor.InApp(user, identityOptions.Value), id, details, request.Headers.IfMatch, cancellationToken);
        return result switch
        {
            { IsNotFound: true } => TypedResults.NotFound(),
            { Error: null } => TypedResults.Ok(),
            { Error: var error } when error == ErrorCode.ValidationFailed =>
                ValidationProblems.Fields(result.FieldErrors),
            { Error: { } error } => error.Problem(),
        };
    }
}
