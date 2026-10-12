using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/ping</c> (design.md §6.2, §8.5): Stay signed in, from the warning before the idle sign-out (A24).
/// It answers <c>204</c> and does nothing more: the session check renews the session cookie on every request it lets in
/// (§8.4, A50), so the ping restarts the 30-minute idle limit as any request does.
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>, before the <c>/api</c> group's antiforgery check. Past the 12-hour
/// limit, <see cref="SessionRules"/> refuses the session and deletes its cookie, as on any request, so Stay signed in
/// cannot extend a session beyond it. §5.4 lists no event for it, so nothing is written.
/// </remarks>
internal static class PingEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapPingEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/ping", () => TypedResults.NoContent());
        return api;
    }
}
