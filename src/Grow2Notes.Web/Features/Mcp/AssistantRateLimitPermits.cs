using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The permits that the AI-assistant access allows (mcp-server.md §5.6, A139). Like <see cref="RateLimitPermits"/> it
/// is a service of the app and never a setting, so production always runs <see cref="Design"/>; only a test host
/// replaces it.
/// </summary>
/// <param name="Requests">Calls of <c>/mcp</c> in a minute, for each user.</param>
/// <param name="OriginRequests">
/// Calls of <c>/mcp</c> that carry an <c>Origin</c> in a minute, for each client address.
/// </param>
/// <param name="ConfirmedChanges">
/// Confirmed invites, sign-in resets and deactivations in an hour, for each user.
/// </param>
/// <param name="TokenClient">
/// Requests to the token and revocation endpoints in a minute, for each approved assistant.
/// </param>
/// <param name="TokenUnregistered">The same requests from anything else in a minute, all together.</param>
/// <param name="TokenOverall">The same requests in a minute, from everyone together.</param>
internal sealed record AssistantRateLimitPermits(
    int Requests,
    int OriginRequests,
    int ConfirmedChanges,
    int TokenClient,
    int TokenUnregistered,
    int TokenOverall)
{
    /// <summary>The numbers of mcp-server.md §5.6.</summary>
    public static readonly AssistantRateLimitPermits Design = new(60, 60, 10, 30, 10, 60);
}
