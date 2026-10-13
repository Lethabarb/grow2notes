using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Who an admin handler acts as, and the channel the call came by (mcp-server.md §3.5, convention 1): the user's ID,
/// which a change records as its audit event's actor (design.md §5.4), and whether the call came from the app's own
/// endpoints or, in Release 2, a tool of the MCP server. The organisation is not in it: it is the tenant, which comes
/// from the session (<see cref="ITenantContext"/>, design.md §5.9).
/// </summary>
internal sealed record Actor(Guid UserId, Channel Channel)
{
    /// <summary>
    /// The user that the request is signed in as, through the app's own endpoints: the ID in the user ID claim that the
    /// claims factory adds to every principal it builds (design.md §8.4), with <see cref="Channel.App"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The principal has no user ID claim, so there is no one to act as, and nothing is done as nobody.
    /// </exception>
    public static Actor InApp(ClaimsPrincipal user, IdentityOptions identityOptions) =>
        new(
            Guid.Parse(user.FindFirstValue(identityOptions.ClaimsIdentity.UserIdClaimType)
                ?? throw new InvalidOperationException(
                    "The signed-in user has no user ID claim, so there is no one to act as.")),
            Channel.App);
}
