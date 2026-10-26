using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// The authorise request that waits for a person's consent, and the person's decision on it (mcp-server.md §4.1). Both
/// are Data Protection tokens on the app's key ring, so nothing is stored, they survive a restart and any instance can
/// read them, and the time they last is the app's <see cref="TimeProvider"/>'s.
/// </summary>
/// <remarks>
/// A <em>handle</em> holds the exact query of a request that OpenIddict has validated, and goes in the address of the
/// Connect screen. A <em>decision</em> is the handle's request, as a hash of its query, with who chose and what; it
/// goes back to <c>/oauth/authorize</c>, which acts on it only for the same query and the same signed-in person. The
/// two have their own purposes, so neither can stand in for the other. Both last <see cref="Lifetime"/> from the
/// request's start: a decision keeps the start of the request it answers. A decision is not single use: following the
/// same address again within the lifetime issues another code, which only the assistant that holds the PKCE verifier
/// can redeem (design.md §13, A117).
/// </remarks>
internal sealed class ConsentRequests(IDataProtectionProvider dataProtection, TimeProvider time)
{
    /// <summary>How long a request, and the decision on it, can be acted on, from the request's start.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private const string RequestPurpose = "Grow2Notes.Mcp.ConsentRequest";
    private const string DecisionPurpose = "Grow2Notes.Mcp.ConsentDecision";

    private readonly IDataProtector requests = dataProtection.CreateProtector(RequestPurpose);
    private readonly IDataProtector decisions = dataProtection.CreateProtector(DecisionPurpose);

    /// <summary>The handle for <paramref name="query"/>, the request's query string without its <c>?</c>.</summary>
    public string Protect(string query) => Seal(requests, new ConsentRequest(query, time.GetUtcNow()));

    /// <summary>The request that <paramref name="handle"/> holds, or null if it is unreadable or has lapsed.</summary>
    public ConsentRequest? Read(string handle) =>
        Open<ConsentRequest>(requests, handle) is { } request && IsLive(request.StartedAt) ? request : null;

    /// <summary>The decision token for <paramref name="request"/>, which keeps the request's start.</summary>
    public string Decide(ConsentRequest request, Guid userId, bool allow) =>
        Seal(decisions, new ConsentDecision(Hash(request.Query), userId, allow, request.StartedAt));

    /// <summary>
    /// The decision that <paramref name="token"/> holds, or null unless it is readable, has not lapsed, was made on
    /// exactly <paramref name="query"/>, and was made by <paramref name="userId"/>.
    /// </summary>
    public ConsentDecision? ReadDecision(string token, string query, Guid userId) =>
        Open<ConsentDecision>(decisions, token) is { } decision
        && IsLive(decision.StartedAt)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(query)), Encoding.UTF8.GetBytes(decision.QueryHash))
        && decision.UserId == userId
            ? decision
            : null;

    private bool IsLive(DateTimeOffset startedAt) => time.GetUtcNow() - startedAt <= Lifetime;

    private static string Hash(string query) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(query)));

    private static string Seal<T>(IDataProtector protector, T value) =>
        WebEncoders.Base64UrlEncode(protector.Protect(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static T? Open<T>(IDataProtector protector, string text)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(protector.Unprotect(WebEncoders.Base64UrlDecode(text)));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }
}

/// <summary>An authorise request waiting for consent: its query string without the <c>?</c>, and its start.</summary>
internal sealed record ConsentRequest(string Query, DateTimeOffset StartedAt);

/// <summary>
/// A person's choice on an authorise request: the SHA-256 hash, base64url, of the request's query, who chose, whether
/// they allowed it, and when the request started.
/// </summary>
internal sealed record ConsentDecision(string QueryHash, Guid UserId, bool Allow, DateTimeOffset StartedAt);
