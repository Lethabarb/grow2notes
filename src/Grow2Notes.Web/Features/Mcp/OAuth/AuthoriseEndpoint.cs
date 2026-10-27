using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// <c>GET /oauth/authorize</c> (mcp-server.md §4.1): the step of the authorisation flow that no code can pass without
/// consent. OpenIddict validates the request first and passes on the ones it accepts; this answers them. It is mapped
/// only where <c>Mcp:Enabled</c> is true.
/// </summary>
/// <remarks>
/// Anyone may call it, as an assistant's request arrives with no session, but it sees the session's cookie when there
/// is one. It never sends anything to the assistant except through OpenIddict:
/// <list type="number">
/// <item>A valid request with no <c>decision</c> is answered <c>302 /connect?request=</c> with its handle
/// (<see cref="ConsentRequests"/>); nothing is remembered, so every request goes to the screen.</item>
/// <item>A request that ends in the <c>decision</c> that <c>POST /api/connect</c> issued is acted on only if the
/// decision is readable, has not lapsed, was made on this exact query by the person signed in now, and the session has
/// an <c>auth_time</c>; otherwise it is answered <c>302 /connect</c> and nothing is written.</item>
/// <item>Then <see cref="ConsentDecisions.TakeAsync"/> decides. A principal is signed in, and OpenIddict answers
/// <c>302</c> to the registered redirect URI with the code, <c>state</c> and <c>iss</c>. No principal is
/// <c>access_denied</c>, which OpenIddict answers the same way with the error.</item>
/// </list>
/// </remarks>
internal static class AuthoriseEndpoint
{
    /// <summary>
    /// The Connect screen. With no request, it shows the general error, which is where a refused request goes.
    /// </summary>
    public const string ConnectPath = "/connect";

    private const string DecisionParameter = "&decision=";

    /// <summary>Maps the endpoint.</summary>
    public static IEndpointRouteBuilder MapAuthoriseEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/oauth/authorize", AuthoriseAsync).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> AuthoriseAsync(
        HttpContext http,
        ConsentRequests consents,
        ConsentDecisions decisions,
        IOptions<IdentityOptions> identityOptions,
        CancellationToken cancellationToken)
    {
        var request = http.GetOpenIddictServerRequest();
        var query = http.Request.QueryString.Value is { Length: > 1 } value ? value[1..] : "";
        var decisionAt = query.LastIndexOf(DecisionParameter, StringComparison.Ordinal);
        if (decisionAt < 0)
        {
            return Results.Redirect($"{ConnectPath}?request={consents.Protect(query)}");
        }

        var claimTypes = identityOptions.Value.ClaimsIdentity;
        var authTime = http.User.FindFirstValue(Grow2NotesSignInManager.AuthTimeClaimType);
        if (!Guid.TryParse(http.User.FindFirstValue(claimTypes.UserIdClaimType), out var userId)
            || authTime is null
            || request?.ClientId is not { } clientId
            || consents.ReadDecision(query[(decisionAt + DecisionParameter.Length)..], query[..decisionAt], userId)
                is not { } decision)
        {
            return Results.Redirect(ConnectPath);
        }

        var principal = await decisions.TakeAsync(
            new Actor(userId, Channel.App), decision.Allow, clientId, authTime, cancellationToken);
        return principal is null
            ? Results.Forbid(
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The connection was not allowed.",
                }),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme])
            : Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
