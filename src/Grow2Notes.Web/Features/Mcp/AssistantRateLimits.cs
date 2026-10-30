using System.Threading.RateLimiting;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The rate limits of the AI-assistant access (mcp-server.md §5.6, A139). They exist only where <c>Mcp:Enabled</c> is
/// true, and their numbers are <see cref="AssistantRateLimitPermits"/>. Every refusal is the <c>429</c> with
/// <c>Retry-After: 60</c> that <see cref="RateLimits"/> gives, with no audit event and no log, so the keys, which are
/// users, client IDs and addresses, go nowhere (§9.5).
/// </summary>
/// <remarks>
/// Calls of <c>/mcp</c> are counted for each user by the framework's limiter (<see cref="Requests"/>), after
/// authorization, so a 401 or 403 uses no permit. Three counts must come before authentication, in
/// <see cref="UseAssistantRateLimits"/>: <c>McpOriginRule</c> reads the approved assistants for every request that
/// carries an <c>Origin</c>, and OpenIddict answers its own refusals at the token, revocation and authorise endpoints
/// inside authentication, where an endpoint's policy never sees them. The confirmed changes of a user are counted by
/// <see cref="ConfirmedChangeLimit"/>.
/// </remarks>
internal static class AssistantRateLimits
{
    /// <summary>The policy of the <c>/mcp</c> endpoint: for each user, whichever address the user calls from.</summary>
    public const string Requests = "mcp";

    /// <summary>Adds the permits, the policy and the counts. Call it only where <c>Mcp:Enabled</c> is true.</summary>
    public static IServiceCollection AddAssistantRateLimits(this IServiceCollection services)
    {
        services.AddSingleton(AssistantRateLimitPermits.Design);
        services.AddSingleton<EarlyCounts>();
        services.AddSingleton<ConfirmedChangeLimit>();

        // Added through the options, as AddRateLimits adds its own, so the permits are the ones the app's services
        // hold. The key is the token's subject, never the address: the access token has no other user ID claim, and an
        // address would count every user behind one address together. A principal with no subject cannot get here.
        services.AddOptions<RateLimiterOptions>().Configure<AssistantRateLimitPermits>((options, permits) =>
            options.AddPolicy<Guid>(
                Requests,
                context => RateLimitPartition.GetFixedWindowLimiter(
                    Actor.InMcp(context.User).UserId, _ => RateLimits.PerMinute(permits.Requests))));

        return services;
    }

    /// <summary>
    /// Adds the counts that come before authentication: a request of <c>/mcp</c> that carries an <c>Origin</c>, for
    /// each client address; the token and revocation endpoints, for each approved assistant and then overall; and the
    /// authorise endpoint, for each client address. Add it only where <c>Mcp:Enabled</c> is true, after the
    /// exception handling and before <c>McpOriginRule</c>.
    /// </summary>
    public static IApplicationBuilder UseAssistantRateLimits(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (await IsRefusedAsync(context))
            {
                await RateLimits.TooManyRequests(context.Response).ExecuteAsync(context);
                return;
            }

            await next(context);
        });

    private static async Task<bool> IsRefusedAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var counts = context.RequestServices.GetRequiredService<EarlyCounts>();
        if (path.StartsWithSegments("/mcp"))
        {
            return context.Request.Headers.Origin.Count > 0 && !counts.TryOrigin(RateLimits.ClientAddress(context));
        }

        if (path.StartsWithSegments("/oauth/token") || path.StartsWithSegments("/oauth/revoke"))
        {
            // Its own count first, so that one assistant that loops is held at its own and cannot use up the overall
            // count for the others, and a request that its own count refuses uses none of the overall.
            return !(counts.TryToken(await TokenKeyAsync(context)) && counts.TryOverall());
        }

        return path.StartsWithSegments("/oauth/authorize") && !counts.TryAuthorise(RateLimits.ClientAddress(context));
    }

    // The ID of the approved assistant that the form names, as stored, or the empty key of everything else: no form, a
    // form that cannot be read, no client_id, one that is too long, and one that is not approved, which includes a
    // variant of an approved ID in case or trailing spaces, as the manager's second check is ordinal. So the keys can
    // only be an approved assistant's ID or empty, and a request cannot make a count of its own. OpenIddict reads the
    // same form after this, which the request caches.
    private static async Task<string> TokenKeyAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType)
        {
            return string.Empty;
        }

        string? clientId;
        try
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            clientId = form["client_id"];
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or BadHttpRequestException)
        {
            return string.Empty;
        }

        if (clientId is not { Length: >= 1 and <= Limits.McpClientId })
        {
            return string.Empty;
        }

        var applications = context.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
        return await applications.FindByClientIdAsync(clientId, context.RequestAborted) is { } application
            ? await applications.GetClientIdAsync(application, context.RequestAborted) ?? string.Empty
            : string.Empty;
    }

    // Four fixed-window limiters whose partitions start their window at their first request, and which the framework
    // drops when idle. In memory, in the one instance that the app runs (§10.1), as AccountRateLimiter is.
    private sealed class EarlyCounts(AssistantRateLimitPermits permits, RateLimitPermits signIn) : IDisposable
    {
        private const string Everyone = "";

        private readonly PartitionedRateLimiter<string> _origin = PartitionedRateLimiter.Create<string, string>(
            address => RateLimitPartition.GetFixedWindowLimiter(
                address, _ => RateLimits.PerMinute(permits.OriginRequests)));

        private readonly PartitionedRateLimiter<string> _token = PartitionedRateLimiter.Create<string, string>(
            key => RateLimitPartition.GetFixedWindowLimiter(
                key,
                _ => RateLimits.PerMinute(key.Length == 0 ? permits.TokenUnregistered : permits.TokenClient)));

        private readonly PartitionedRateLimiter<string> _overall = PartitionedRateLimiter.Create<string, string>(
            _ => RateLimitPartition.GetFixedWindowLimiter(
                Everyone, _ => RateLimits.PerMinute(permits.TokenOverall)));

        private readonly PartitionedRateLimiter<string> _authorise = PartitionedRateLimiter.Create<string, string>(
            address => RateLimitPartition.GetFixedWindowLimiter(
                address, _ => RateLimits.PerMinute(signIn.SignIn)));

        public bool TryOrigin(string address) => TryAcquire(_origin, address);

        public bool TryToken(string key) => TryAcquire(_token, key);

        public bool TryOverall() => TryAcquire(_overall, Everyone);

        public bool TryAuthorise(string address) => TryAcquire(_authorise, address);

        public void Dispose()
        {
            _origin.Dispose();
            _token.Dispose();
            _overall.Dispose();
            _authorise.Dispose();
        }

        private static bool TryAcquire(PartitionedRateLimiter<string> limiter, string key)
        {
            using var lease = limiter.AttemptAcquire(key);
            return lease.IsAcquired;
        }
    }
}
