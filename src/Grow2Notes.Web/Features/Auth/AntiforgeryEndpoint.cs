using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>GET /api/auth/antiforgery</c> (design.md §6.2, §9.8 item 2): sets the antiforgery cookie, when the request has
/// none, and answers <c>204</c> with a request token in the <see cref="ApiGroup.AntiforgeryHeader"/> response header,
/// for the SPA to keep in memory; a cookie that script could read would keep the token on the device. The
/// <see cref="ApiGroup"/> filter checks the two together.
/// </summary>
/// <remarks>
/// Anyone may call it (§6.2): the sign-in page needs a token before anyone is signed in, to send its own POST. A token
/// is bound to the user it was issued to, so the SPA fetches a fresh one after sign-in. A browser's navigation to it,
/// and a request that a browser says comes from another origin, get a <c>403</c> and no cookie.
/// </remarks>
internal static class AntiforgeryEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapAntiforgeryEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/auth/antiforgery", Issue).AllowAnonymous();
        return api;
    }

    private static Results<NoContent, StatusCodeHttpResult> Issue(HttpContext context, IAntiforgery antiforgery)
    {
        // With no body, which the status code pages make problem details with no design.md §6.9 code.
        if (IsNavigationOrFromAnotherOrigin(context.Request))
        {
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Headers[ApiGroup.AntiforgeryHeader] = tokens.RequestToken;
        return TypedResults.NoContent();
    }

    // Another site's page can navigate the browser here, top level, without the SameSite=Strict cookie, so a new cookie
    // would be set in answer and replace the one that the SPA's token pairs with, and the SPA's next change would get a
    // 400. The SPA only ever fetches the token, from its own origin, as the browser says in its Sec-Fetch headers,
    // which no page can set. A request with none is let through: it comes from a client that is not a browser, such as
    // curl in the deploy's smoke test, or from a browser too old to send them, which this check cannot protect.
    private static bool IsNavigationOrFromAnotherOrigin(HttpRequest request) =>
        request.Headers["Sec-Fetch-Mode"] == "navigate"
        || (request.Headers.TryGetValue("Sec-Fetch-Site", out var site) && site != "same-origin");
}
