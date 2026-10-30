using System.Text.Json.Serialization;
using Grow2Notes.Web.Platform;
using ModelContextProtocol.AspNetCore;
using OpenIddict.Validation;
using static OpenIddict.Validation.OpenIddictValidationEvents;
using static OpenIddict.Validation.OpenIddictValidationHandlers;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// <c>POST /mcp</c> (mcp-server.md §3.2, §4.2): the MCP server's one address, for an approved assistant's access token
/// and nothing else, and the document that tells an assistant where to sign in. It is mapped only where
/// <c>Mcp:Enabled</c> is true, and the 401 that an assistant starts its sign-in from comes from the validation of
/// its token, with this address's document and scope added.
/// </summary>
internal static class McpEndpoint
{
    private const string DocumentName = ".well-known/oauth-protected-resource";

    /// <summary>
    /// What the challenge of a request with no valid access token names (RFC 9728 §5.1): the document that says where
    /// to sign in, and the scope to ask for.
    /// </summary>
    public static OpenIddictValidationHandlerDescriptor ChallengeDescriptor { get; } =
        OpenIddictValidationHandlerDescriptor.CreateBuilder<ProcessChallengeContext>()
            .UseSingletonHandler<ChallengeParameters>()
            .SetOrder(AttachCustomChallengeParameters.Descriptor.Order - 1_000)
            .SetType(OpenIddictValidationHandlerType.Custom)
            .Build();

    /// <summary>
    /// The address of the protected-resource document: the issuer's, so that the one the challenge names and the one
    /// served are the same.
    /// </summary>
    public static string DocumentAddress(IConfiguration configuration) => McpSetup.Issuer(configuration) + DocumentName;

    /// <summary>
    /// Maps the endpoint, which only a Manager or Support person's access token may call. The SDK maps <c>POST</c>
    /// alone when it is stateless, so <c>GET</c> and <c>DELETE</c> are answered here, after the same checks.
    /// </summary>
    public static IEndpointRouteBuilder MapMcpEndpoint(this IEndpointRouteBuilder app)
    {
        var mcp = app.MapGroup("/mcp").RequireAuthorization(Policies.McpAdmin)
            .RequireRateLimiting(AssistantRateLimits.Requests);
        mcp.MapMcp();
        mcp.MapMethods("", [HttpMethods.Get, HttpMethods.Delete], static (HttpContext http) =>
        {
            http.Response.Headers.Allow = HttpMethods.Post;
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        });
        return app;
    }

    /// <summary>
    /// Maps the protected-resource document (RFC 9728) at the root address only, which every challenge names. The
    /// address with the resource's path added stays a 404, so there is one document to keep right.
    /// </summary>
    public static IEndpointRouteBuilder MapProtectedResourceDocument(this IEndpointRouteBuilder app)
    {
        app.MapGet("/" + DocumentName, static (IConfiguration configuration) =>
            TypedResults.Ok(new ProtectedResource(
                McpSetup.Resource(configuration),
                [McpSetup.Issuer(configuration)],
                [McpSetup.AdminScope],
                ["header"]))).AllowAnonymous();
        return app;
    }

    private sealed record ProtectedResource(
        [property: JsonPropertyName("resource")] string Resource,
        [property: JsonPropertyName("authorization_servers")] string[] AuthorizationServers,
        [property: JsonPropertyName("scopes_supported")] string[] ScopesSupported,
        [property: JsonPropertyName("bearer_methods_supported")] string[] BearerMethodsSupported);

    private sealed class ChallengeParameters(IConfiguration configuration)
        : IOpenIddictValidationHandler<ProcessChallengeContext>
    {
        public ValueTask HandleAsync(ProcessChallengeContext context)
        {
            context.Parameters["resource_metadata"] = DocumentAddress(configuration);
            context.Parameters["scope"] = McpSetup.AdminScope;
            return default;
        }
    }
}
