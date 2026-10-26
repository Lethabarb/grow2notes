using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// What an approved assistant's tokens carry (mcp-server.md §4.2, §6.1): who the person is and when they signed in, and
/// nothing about their role. The role is read from the person's row whenever a token is used, never from a token.
/// </summary>
internal static class AssistantTokens
{
    /// <summary>
    /// The claim that holds <see cref="StampHash"/> of the person's security stamp when the token was issued, so a
    /// reset, a deactivation or a sign-out of the app ends the token's use.
    /// </summary>
    public const string StampHashClaimType = "stamp_hash";

    private const string AuthenticationType = "Grow2Notes.Assistant";

    /// <summary>
    /// The base64url SHA-256 of <paramref name="stamp"/>. A token holds this, not the stamp, which would let the token
    /// be used to sign in.
    /// </summary>
    public static string StampHash(string stamp) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(stamp)));

    /// <summary>
    /// The principal that OpenIddict signs the tokens of an approved assistant from: <paramref name="user"/>'s ID,
    /// their organisation, the hash of their security stamp and <paramref name="authTime"/>, the time of the sign-in
    /// that began the session, copied forward unchanged. Every claim goes to the access token only. It has no role,
    /// name or email claim.
    /// </summary>
    public static ClaimsPrincipal PrincipalFor(
        ApplicationUser user, string authTime, IEnumerable<string> scopes, string resource)
    {
        var stamp = user.SecurityStamp
            ?? throw new InvalidOperationException("The user has no security stamp, so there is nothing to bind to.");
        var identity = new ClaimsIdentity(AuthenticationType, Claims.Name, Claims.Role);
        identity.AddClaim(new(Claims.Subject, user.Id.ToString("D")));
        identity.AddClaim(new(TenantContext.OrganisationIdClaimType, user.OrganisationId.ToString("D")));
        identity.AddClaim(new(StampHashClaimType, StampHash(stamp)));
        identity.AddClaim(new(Grow2NotesSignInManager.AuthTimeClaimType, authTime, ClaimValueTypes.Integer64));
        identity.SetDestinations(_ => [Destinations.AccessToken]);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(resource);
        return principal;
    }
}
