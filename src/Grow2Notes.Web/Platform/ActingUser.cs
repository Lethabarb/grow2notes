using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The user on whose behalf an admin handler acts (mcp-server.md §3.5, convention 1): their ID, which a change records
/// as its actor, and their organisation, the tenant, whose rows the handler reads and changes (design.md §5.9).
/// </summary>
internal sealed record ActingUser(Guid UserId, Guid OrganisationId)
{
    /// <summary>
    /// The user that the request is signed in as: the ID in the user ID claim that the claims factory adds, and the
    /// request's tenant, the organisation in the session's <c>org_id</c> claim (design.md §8.4, §5.9 item 1).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The principal has no user ID claim, or the request has no tenant, so there is no one to act as. The claims
    /// factory adds both claims to every principal it builds, so only a request with no session can lack them.
    /// </exception>
    public static ActingUser FromSession(ClaimsPrincipal user, IdentityOptions identityOptions, ITenantContext tenant)
    {
        var userId = user.FindFirstValue(identityOptions.ClaimsIdentity.UserIdClaimType)
            ?? throw new InvalidOperationException(
                "The signed-in user has no user ID claim, so there is no one to act as.");
        return new(Guid.Parse(userId), tenant.OrganisationId);
    }
}
