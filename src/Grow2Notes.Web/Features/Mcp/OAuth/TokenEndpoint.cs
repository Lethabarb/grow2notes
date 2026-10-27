using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// <c>POST /oauth/token</c> (mcp-server.md §4.2): the code exchange and the refresh. OpenIddict validates the request
/// first and answers its own errors as JSON: a client secret (<c>invalid_client</c>), a code or refresh token that is
/// replayed or has lapsed, a refresh token reused outside the leeway (which revokes the chain), PKCE, and the resource.
/// It passes on only the requests it accepts, and this decides them from the person's row as it is now, never from the
/// role or organisation that the token was issued with. It is mapped only where <c>Mcp:Enabled</c> is true.
/// </summary>
/// <remarks>
/// It has no <c>org_id</c> to give the tenant, so it reads only <c>AspNetUsers</c> and OpenIddict's tables.
/// </remarks>
internal static class TokenEndpoint
{
    /// <summary>Maps the endpoint.</summary>
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/oauth/token", ExchangeAsync).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext http,
        UserManager<ApplicationUser> users,
        IOpenIddictAuthorizationManager authorizations,
        TimeProvider time,
        IConfiguration configuration)
    {
        var stored = (await http.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal
            ?? throw new InvalidOperationException("OpenIddict passed on a token request with nothing to redeem.");
        var subject = stored.GetClaim(Claims.Subject);
        var user = subject is null ? null : await users.FindByIdAsync(subject);

        switch (AssistantTokens.Check(user, stored, time.GetUtcNow()))
        {
            case AssistantTokenCheck.Allowed:
                // OpenIddict puts back its own private claims (the authorisation, the presenters, the dates) from the
                // code or refresh token that was presented, so only the person's are rebuilt here.
                return Results.SignIn(
                    AssistantTokens.PrincipalFor(
                        user!,
                        stored.GetClaim(Grow2NotesSignInManager.AuthTimeClaimType)!,
                        stored.GetScopes(),
                        McpSetup.Resource(configuration)),
                    authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            case AssistantTokenCheck.UserChanged:
                // Whoever changed, every connection that person has ends, not only this one. The subject is the "D" ID
                // that PrincipalFor wrote, and the revocation must finish even if the assistant disconnects.
                await authorizations.RevokeConnectionsAsync(Guid.Parse(subject!), CancellationToken.None);
                return Refused("The person is no longer allowed to connect an assistant.");

            default:
                // The session that began this chain is past its limit, and its sign-in time never changes, so the chain
                // can never be used again. The person's other assistants, from a later sign-in, are not touched.
                return Refused("The session that began this connection has ended.");
        }
    }

    private static IResult Refused(string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
}
