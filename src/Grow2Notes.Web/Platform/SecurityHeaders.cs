using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Sets design.md §9.7's security headers on every response, in every environment: the strict Content Security Policy,
/// <c>nosniff</c>, no referrer, the browser features that the app may use, and a browsing context group of the app's
/// own. The sixth, <c>Strict-Transport-Security</c>, is <c>UseHsts</c>'s, which sends it only over HTTPS and never to
/// <c>localhost</c>, so this sets no second one.
/// </summary>
/// <remarks>
/// §9.7 keeps the policy off the Vite dev server's page, and that needs no environment check: the dev server serves
/// its own page, so the policy never reaches a page there, and the <c>/api</c> responses that it proxies carry it
/// harmlessly, as a policy on the response to a <c>fetch</c> does nothing.
/// </remarks>
internal static class SecurityHeaders
{
    // §9.7's directives, in its order, on one line.
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; " +
        "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; " +
        "frame-ancestors 'none'; upgrade-insecure-requests";

    private const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), " +
        "publickey-credentials-create=(self), publickey-credentials-get=(self)";

    private static readonly KeyValuePair<string, string>[] Headers =
    [
        new(HeaderNames.ContentSecurityPolicy, ContentSecurityPolicy),
        new(HeaderNames.XContentTypeOptions, "nosniff"),
        new("Referrer-Policy", "no-referrer"),
        new("Permissions-Policy", PermissionsPolicy),
        new("Cross-Origin-Opener-Policy", "same-origin"),
    ];

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            // Set as the response starts, as the caching rules are, because the exception handler clears the
            // response's headers before it writes its problem: set now, they would be lost from a 500, 400 or 413.
            // Each is set, not added, so these values have the last word over any that an endpoint set.
            context.Response.OnStarting(static state =>
            {
                var response = (HttpResponse)state;
                foreach (var (name, value) in Headers)
                {
                    response.Headers[name] = value;
                }

                return Task.CompletedTask;
            }, context.Response);

            return next(context);
        });
}
