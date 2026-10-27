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

/// <summary>What <see cref="AssistantTokens.Check"/> finds when a code or a refresh token is presented.</summary>
internal enum AssistantTokenCheck
{
    /// <summary>The person is as they were and the session is within its limit.</summary>
    Allowed,

    /// <summary>The session that began the connection is more than 12 hours old.</summary>
    PastAbsoluteLimit,

    /// <summary>
    /// The person is gone, no longer active, no longer a manager or Support, in another organisation, or has signed
    /// out.
    /// </summary>
    UserChanged,
}

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

    /// <summary>
    /// Whether the connection that <paramref name="stored"/> (the principal inside a code or a refresh token) belongs
    /// to may go on, by <paramref name="user"/>'s row now. A person who has changed is found first, so that a
    /// deactivated person past the 12 hours still has their authorisations revoked.
    /// </summary>
    public static AssistantTokenCheck Check(ApplicationUser? user, ClaimsPrincipal stored, DateTimeOffset now)
    {
        if (user is not
            {
                Status: UserStatus.Active,
                Role: UserRole.Manager or UserRole.Support,
                SecurityStamp: { } stamp,
            }
            || !SameValue(stored.FindFirstValue(StampHashClaimType), StampHash(stamp))
            || !SameValue(
                stored.FindFirstValue(TenantContext.OrganisationIdClaimType), user.OrganisationId.ToString("D")))
        {
            return AssistantTokenCheck.UserChanged;
        }

        return SessionRules.IsWithinAbsoluteLimit(stored, now)
            ? AssistantTokenCheck.Allowed
            : AssistantTokenCheck.PastAbsoluteLimit;
    }

    /// <summary>
    /// Ends every connection that <paramref name="userId"/> has: revokes their authorisations, which refuses the
    /// refresh tokens and every access token already issued. It is the one way a change to a person ends their
    /// connections, and it joins the transaction it is called in.
    /// </summary>
    public static Task RevokeConnectionsAsync(
        this IOpenIddictAuthorizationManager authorizations, Guid userId, CancellationToken ct) =>
        authorizations.RevokeBySubjectAsync(userId.ToString("D"), ct).AsTask();

    private static bool SameValue(string? stored, string current) =>
        stored is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(current));
}
