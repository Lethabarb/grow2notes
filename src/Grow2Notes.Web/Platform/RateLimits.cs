using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The rate limits of design.md §9.9. The sign-in and setup endpoints, the two exports and every other API endpoint
/// each have a policy of the framework's rate limiter, named here for <c>RequireRateLimiting</c>: a fixed window of one
/// minute, in which a request over the limit is refused at once, never queued. A request uses a permit when it reaches
/// the endpoint, whatever the endpoint answers; authorization's refusals come first and use none. The password step
/// and the code step are limited for each account too, inside their endpoints (<see cref="AccountRateLimiter"/>).
/// </summary>
/// <remarks>
/// Every refusal is a <c>429</c> with <c>Retry-After: 60</c> and no body, which the status code pages make problem
/// details, as they do for any other bare error status; it has no design.md §6.9 code. A refusal writes no audit event
/// and logs nothing, so the keys, which are addresses and accounts, go nowhere (§9.5). The permits come from
/// <see cref="RateLimitPermits"/>, so production runs the design's and a test host its own.
/// </remarks>
internal static class RateLimits
{
    /// <summary>The policy of the sign-in and setup endpoints, for each client address.</summary>
    public const string SignIn = "sign-in";

    /// <summary>The policy of the two export endpoints, which share one count for each user.</summary>
    public const string Export = "export";

    /// <summary>
    /// The policy of every other API endpoint: for each user, or for each client address if signed out.
    /// </summary>
    public const string General = "general";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddRateLimits(this IServiceCollection services)
    {
        services.AddSingleton(RateLimitPermits.Design);
        services.AddSingleton<AccountRateLimiter>();

        // Without a status of its own, a refusal is a 503.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                SetRetryAfter(context.HttpContext.Response);
                return ValueTask.CompletedTask;
            };
        });

        // Added through the options, not in AddRateLimiter's callback, so the permits are the ones the app's services
        // hold, whatever replaces them.
        services.AddOptions<RateLimiterOptions>().Configure<RateLimitPermits, IOptions<IdentityOptions>>(
            (options, permits, identityOptions) =>
            {
                var userIdClaimType = identityOptions.Value.ClaimsIdentity.UserIdClaimType;
                options.AddPolicy<string>(
                    SignIn,
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        ClientAddress(context), _ => PerMinute(permits.SignIn)));
                options.AddPolicy<string>(
                    Export,
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        UserOrClientAddress(context, userIdClaimType), _ => PerMinute(permits.Export)));
                options.AddPolicy<string>(
                    General,
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        UserOrClientAddress(context, userIdClaimType), _ => PerMinute(permits.General)));
            });

        return services;
    }

    /// <summary>
    /// The answer of an endpoint whose own check refused the request: the <c>429</c> that the middleware gives, with
    /// the same <c>Retry-After</c>.
    /// </summary>
    public static StatusCodeHttpResult TooManyRequests(HttpResponse response)
    {
        SetRetryAfter(response);
        return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    internal static FixedWindowRateLimiterOptions PerMinute(int permits) => new()
    {
        PermitLimit = permits,
        Window = Window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };

    // The whole window: a refused request can try again once its window is over.
    private static void SetRetryAfter(HttpResponse response) =>
        response.Headers.RetryAfter = ((int)Window.TotalSeconds).ToString(CultureInfo.InvariantCulture);

    // The address after UseForwardedHeaders. An IPv4 client may arrive in its IPv4-mapped form, which would otherwise
    // be a second count for the same client. No address, which only a test host gives, is one count of its own.
    internal static string ClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address
            ? (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString()
            : string.Empty;

    private static string UserOrClientAddress(HttpContext context, string userIdClaimType) =>
        context.User.FindFirstValue(userIdClaimType) ?? ClientAddress(context);
}
