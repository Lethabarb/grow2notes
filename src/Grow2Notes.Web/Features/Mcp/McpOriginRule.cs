using OpenIddict.Abstractions;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The rule on a request's <c>Origin</c> header at <c>/mcp</c> (mcp-server.md §4.5 step 2, MA10): none is fine, as an
/// assistant that is not in a browser sends none, and one is fine only if it is the origin of an approved assistant's
/// HTTPS redirect URI. Anything else is a page of another site calling the endpoint from a person's browser, and
/// gets a 403 before any token is read. No IP restriction is applied, and no response ever carries a cross-origin
/// (CORS) header: the app configures none. A request that carries an Origin is counted by client address before this
/// rule, so that its read of the approved assistants is never unlimited (A139).
/// </summary>
internal static class McpOriginRule
{
    /// <summary>Adds the rule. Add it only where <c>Mcp:Enabled</c> is true, after the exception handling.</summary>
    public static IApplicationBuilder UseMcpOriginRule(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/mcp") && !await IsAcceptableAsync(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });

    private static async Task<bool> IsAcceptableAsync(HttpContext context)
    {
        var origins = context.Request.Headers.Origin;
        if (origins.Count == 0)
        {
            return true;
        }

        // A repeated header, or the "null" origin that a sandboxed page sends, is no origin of an assistant.
        if (origins.Count > 1 || origins[0] is not { Length: > 0 } origin)
        {
            return false;
        }

        // Read when a request has an Origin, which no assistant outside a browser sends, so no cache is needed.
        var applications = context.RequestServices.GetRequiredService<IOpenIddictApplicationManager>();
        await foreach (var application in applications.ListAsync(
            count: null, offset: null, context.RequestAborted))
        {
            foreach (var redirectUri in await applications.GetRedirectUrisAsync(application, context.RequestAborted))
            {
                if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
                    && uri.Scheme == Uri.UriSchemeHttps
                    && string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
