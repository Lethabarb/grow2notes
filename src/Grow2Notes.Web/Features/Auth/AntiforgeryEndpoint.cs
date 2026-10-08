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
/// is bound to the user it was issued to, so the SPA fetches a fresh one after sign-in.
/// </remarks>
internal static class AntiforgeryEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapAntiforgeryEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/auth/antiforgery", Issue).AllowAnonymous();
        return api;
    }

    private static NoContent Issue(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Headers[ApiGroup.AntiforgeryHeader] = tokens.RequestToken;
        return TypedResults.NoContent();
    }
}
