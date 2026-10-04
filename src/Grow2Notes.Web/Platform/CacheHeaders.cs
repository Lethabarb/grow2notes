namespace Grow2Notes.Web.Platform;

/// <summary>
/// Applies the design.md §7.2 caching rules: <c>no-store</c> under /api, <c>no-cache</c> for index.html, and a one-year
/// immutable cache for Vite's hashed /assets files.
/// </summary>
internal static class CacheHeaders
{
    public static IApplicationBuilder UseCacheHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            // Applied as the response starts, after the endpoint, the static asset handlers and any error handling
            // have set their own headers, so these rules always have the final word.
            context.Response.OnStarting(static state =>
            {
                Apply((HttpContext)state);
                return Task.CompletedTask;
            }, context);

            return next(context);
        });

    private static void Apply(HttpContext context)
    {
        var response = context.Response;
        var path = context.Request.Path;

        if (path.StartsWithSegments("/api"))
        {
            // Covers every API response, including the unknown-path 404, handled errors and endpoints added later.
            response.Headers.CacheControl = "no-store";
        }
        else if (response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            // index.html, whether from MapStaticAssets or the client-route fallback, names the current hashed assets,
            // so the browser revalidates it on every load and picks up a new release. Checked before /assets so the
            // fallback page for an extensionless /assets/... path is never marked immutable.
            response.Headers.CacheControl = "no-cache";
        }
        else if (path.StartsWithSegments("/assets") && response.StatusCode < StatusCodes.Status400BadRequest)
        {
            // Vite content-hashes every file it writes to /assets, but MapStaticAssets only recognises its own
            // fingerprints and marks these no-cache. A 404 for a missing asset is left uncached. Unhashed files (for
            // example from the SPA's public/ folder) must never be placed under /assets.
            response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
}
