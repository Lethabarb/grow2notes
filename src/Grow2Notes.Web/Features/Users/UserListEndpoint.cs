using System.Security.Claims;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// <c>GET /api/admin/users</c> (design.md §6.6): the signed-in manager's organisation's users, as
/// <see cref="UserList"/> lists them, for the Users screen (§4.11).
/// </summary>
/// <remarks>
/// It is mapped in the <c>/api/admin</c> group, whose <see cref="Policies.Manager"/> policy gives a worker a
/// <c>403</c> and a request with no session a <c>401</c>. It answers no <c>ETag</c>, as each user in the list carries
/// their own token (D78), and writes no audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class UserListEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api/admin</c> group, which <see cref="ApiGroup.MapAdminGroup"/> returns.
    /// </summary>
    public static RouteGroupBuilder MapUserListEndpoint(this RouteGroupBuilder admin)
    {
        admin.MapGet("/users", ListAsync);
        return admin;
    }

    private static async Task<Ok<IReadOnlyList<AdminUser>>> ListAsync(
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        ITenantContext tenant,
        UserList users,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.ListAsync(
            ActingUser.FromSession(user, identityOptions.Value, tenant), Channel.App, cancellationToken));
}
