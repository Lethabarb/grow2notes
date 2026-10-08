using System.Net.Mime;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;
using MediaTypeHeaderValue = Microsoft.Net.Http.Headers.MediaTypeHeaderValue;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The <c>/api</c> route group, in which every API endpoint is mapped, and its filter, which refuses a request that
/// changes something unless any body it has is JSON (design.md §9.8 item 3), so a plain HTML form post cannot reach an
/// endpoint, and it sends a valid antiforgery token (item 2): one that <c>GET /api/auth/antiforgery</c> issued to the
/// same user, in the <see cref="AntiforgeryHeader"/> header, with the cookie that holds its other half. A body that is
/// not JSON gets a <c>415</c>, as minimal APIs answer one they would read as JSON, and a request without a valid token
/// a <c>400</c>, ASP.NET Core's status for a failed check; neither has a design.md §6.9 code.
/// </summary>
/// <remarks>
/// Both checks cover every method but GET, HEAD, OPTIONS and TRACE, which antiforgery leaves unchecked, so every POST,
/// PUT and DELETE. The antiforgery cookie is <c>Secure</c> always, so antiforgery throws on a request that is not
/// HTTPS, whether it issues a token or checks one; behind App Service's front end, which calls the app over HTTP, a
/// request is HTTPS by its forwarded scheme. Minimal APIs' own antiforgery middleware (<c>UseAntiforgery</c>) checks
/// only endpoints that read a form, and the API reads none, so the app does not use it. Like any endpoint filter, this
/// one runs after minimal APIs have bound the endpoint's parameters and validated them, so a request whose body they
/// refuse gets their answer, such as a <c>415</c> for a form post to an endpoint that reads JSON, or a <c>422</c>,
/// before its token is checked; it never reaches the endpoint either. The filter, too, checks the body's type before
/// the token, so every endpoint in the group answers a form post alike, whether it reads a body or not.
/// </remarks>
internal static class ApiGroup
{
    /// <summary>
    /// The header that carries the antiforgery request token, from the antiforgery endpoint to the SPA, which keeps it
    /// in memory, and back on each request that changes something.
    /// </summary>
    public const string AntiforgeryHeader = "X-XSRF-TOKEN";

    /// <summary>Antiforgery, with the cookie and header that the group's filter checks.</summary>
    public static IServiceCollection AddApiGroup(this IServiceCollection services) =>
        services.AddAntiforgery(options =>
        {
            // The session cookie's settings (design.md §8.4). A browser takes a __Host- cookie only when it is Secure,
            // has Path=/ and names no Domain, so no other site under a parent domain can set it.
            options.Cookie.Name = "__Host-grow2notes-xsrf";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.HeaderName = AntiforgeryHeader;

            // The token comes in the header only, so antiforgery never reads a form body to look for it.
            options.SuppressReadingTokenFromFormBody = true;

            // design.md §9.7 sets no X-Frame-Options; its CSP's frame-ancestors 'none' covers framing.
            options.SuppressXFrameOptionsHeader = true;
        });

    /// <summary>
    /// Maps the <c>/api</c> group, whose filter refuses a request that changes something unless any body it has is JSON
    /// and it sends a valid token.
    /// </summary>
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");
        api.AddEndpointFilter(RefuseUnlessJsonWithTokenAsync);
        return api;
    }

    private static async ValueTask<object?> RefuseUnlessJsonWithTokenAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // A request that changes nothing is let through: it needs no token, and is not handed to antiforgery, whose
        // HTTPS check would otherwise refuse a GET over plain HTTP, as in CI's end-to-end run.
        if (HttpMethods.IsGet(httpContext.Request.Method) || HttpMethods.IsHead(httpContext.Request.Method)
            || HttpMethods.IsOptions(httpContext.Request.Method) || HttpMethods.IsTrace(httpContext.Request.Method))
        {
            return await next(context);
        }

        // Both refusals have no body, which the status code pages make problem details.
        if (!HasJsonBodyOrNone(httpContext.Request))
        {
            return TypedResults.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
        return await antiforgery.IsRequestValidAsync(httpContext) ? await next(context) : TypedResults.BadRequest();
    }

    // A request that names a Content-Type must name application/json, with any parameters, such as a charset, even
    // when its body is empty; one that names none must have no body. Whether it has one, the server says, as minimal
    // APIs ask it before they read a body.
    private static bool HasJsonBodyOrNone(HttpRequest request) =>
        request.ContentType is null
            ? !request.HttpContext.Features.GetRequiredFeature<IHttpRequestBodyDetectionFeature>().CanHaveBody
            : MediaTypeHeaderValue.TryParse(request.ContentType, out var type)
                && type.MediaType.Equals(MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase);
}
