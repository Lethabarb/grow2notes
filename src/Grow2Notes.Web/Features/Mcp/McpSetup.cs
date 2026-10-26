using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers.Authentication;
using static OpenIddict.Server.OpenIddictServerHandlers.Discovery;

namespace Grow2Notes.Web.Features.Mcp;

/// <summary>
/// The AI-assistant access for managers and admins (mcp-server.md): the registration of OpenIddict, and the off
/// switch. OpenIddict's core and its stores are always registered, so the operator command that approves an assistant
/// works before the access is switched on. The authorisation server is added only where <c>Mcp:Enabled</c> is true,
/// and the paths of the access answer 404 whether it is or not.
/// </summary>
internal static class McpSetup
{
    /// <summary>The setting that switches the access on. No setting means off.</summary>
    public const string EnabledKey = "Mcp:Enabled";

    /// <summary>
    /// The one scope an approved assistant is given: the managers' and admins' actions (mcp-server.md §4).
    /// </summary>
    public const string AdminScope = "grow2notes.admin";

    /// <summary>Whether <paramref name="configuration"/> switches the access on.</summary>
    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue<bool>(EnabledKey);

    /// <summary>
    /// The authorisation server's issuer: the app's origin with a trailing slash, as <see cref="Uri.AbsoluteUri"/>
    /// writes it. The protected-resource document must name this identical string.
    /// </summary>
    public static string Issuer(IConfiguration configuration) => AppOrigin.Require(configuration).AbsoluteUri;

    /// <summary>The MCP endpoint, which is the resource an approved assistant is given access to.</summary>
    public static string Resource(IConfiguration configuration) =>
        new Uri(AppOrigin.Require(configuration), "mcp").AbsoluteUri;

    public static IServiceCollection AddMcp(this IServiceCollection services, IConfiguration configuration)
    {
        // The consent step's classes are registered whether or not the access is on: the handler writes audit events
        // that a test calls on an app with the access off.
        services.AddScoped<ConsentRequests>();
        services.AddScoped<ConsentDecisions>();

        var openIddict = services.AddOpenIddict();

        // The stores save through the scoped Grow2NotesDbContext, so the operator command's changes join its
        // transaction. The entity cache lasts one scope, and a retried transaction would otherwise get back an
        // application that its rolled-back attempt had removed.
        openIddict.AddCore(core =>
        {
            core.UseEntityFrameworkCore().UseDbContext<Grow2NotesDbContext>().ReplaceDefaultEntities<Guid>();
            core.DisableEntityCaching();
        });

        if (IsEnabled(configuration))
        {
            openIddict.AddServer(AddAuthorisationServer);
            services.AddOptions<OpenIddictServerOptions>().Configure<IConfiguration>((options, config) =>
            {
                options.Issuer = new Uri(Issuer(config));
                options.Resources.Add(new Uri(Resource(config)));
            });
        }

        return services;
    }

    // Only the authorize and token endpoints exist: no JWKS, revocation or other endpoint. A code is exchanged with
    // PKCE S256 alone, by a public client, and no identity token is ever issued, so openid is not a scope. Responses
    // go in the query only, so no page has to post a form under the CSP. Tokens are in Data Protection format on the
    // app's key ring; the two keys OpenIddict will not start without are made in memory at each start and protect
    // nothing that is issued.
    private static void AddAuthorisationServer(OpenIddictServerBuilder server)
    {
        server.SetAuthorizationEndpointUris("oauth/authorize")
            .SetTokenEndpointUris("oauth/token")
            // OpenIddict serves a JWKS endpoint unless told otherwise; no issued token needs one.
            .SetJsonWebKeySetEndpointUris(Array.Empty<string>())
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange()
            .Configure(options =>
            {
                options.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain);
                options.Scopes.Remove(Scopes.OpenId);
                options.ResponseModes.Remove(ResponseModes.FormPost);
                options.ResponseModes.Remove(ResponseModes.Fragment);
            })
            .RegisterScopes(AdminScope, Scopes.OfflineAccess)
            .AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey()
            .UseDataProtection();

        // The authorize request is validated by OpenIddict and then passed on to AuthoriseEndpoint, so no code can be
        // issued without consent. An error with no redirect URI that OpenIddict can use goes there too, instead of
        // OpenIddict's own error page; one with a validated redirect URI is still sent to it, with iss.
        server.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableErrorPassthrough();

        // An assistant is sent back to a state it chose and a resource it asked for (RFC 8707), and it must tell the
        // person's browser apart from any other, so the request must hold exactly one resource and a state. It runs
        // after the built-in checks, so the redirect URI is validated and the refusal can be sent to it.
        server.AddEventHandler<ValidateAuthorizationRequestContext>(handler => handler
            .UseInlineHandler(context =>
            {
                if (string.IsNullOrEmpty(context.Request.State))
                {
                    context.Reject(Errors.InvalidRequest, "The state parameter is required.");
                }
                else if (context.Request.Resources?.Count() != 1)
                {
                    context.Reject(Errors.InvalidRequest, "Exactly one resource parameter is required.");
                }

                return default;
            })
            .SetOrder(ValidateAuthorizedParty.Descriptor.Order + 1_000));

        // OpenIddict 7.7.1 has neither a way to advertise client ID metadata documents nor a way to leave out the
        // client authentication methods it advertises: every approved assistant is a public client.
        server.AddEventHandler<HandleConfigurationRequestContext>(handler => handler
            .UseInlineHandler(context =>
            {
                context.Metadata["client_id_metadata_document_supported"] = true;
                context.TokenEndpointAuthenticationMethods.Clear();
                context.TokenEndpointAuthenticationMethods.Add(ClientAuthenticationMethods.None);
                return default;
            })
            .SetOrder(AttachAdditionalMetadata.Descriptor.Order + 1_000));
    }

    /// <summary>
    /// Maps the endpoints of the consent step, which exist only where the access is switched on: the authorise
    /// endpoint, and the Connect screen's two in the <c>/api</c> group, <paramref name="api"/>. Where it is off they
    /// are unmapped, so each falls to a 404 catch-all.
    /// </summary>
    public static void MapMcpEndpoints(this WebApplication app, RouteGroupBuilder api)
    {
        app.MapAuthoriseEndpoint();
        api.MapConnectEndpoints();
    }

    /// <summary>
    /// Any path of the access, and of the discovery documents, that nothing more specific answers is a 404 for every
    /// method, never the SPA page and never a 401, as under <c>/api</c> (design.md §6.1). Each is mapped after the real
    /// endpoints, which routing prefers, and before the SPA fallback.
    /// </summary>
    public static void MapMcpFallbacks(this WebApplication app)
    {
        app.Map("/oauth/{**rest}", () => Results.NotFound()).AllowAnonymous();
        app.Map("/.well-known/{**rest}", () => Results.NotFound()).AllowAnonymous();
        app.Map("/mcp/{**rest}", () => Results.NotFound()).AllowAnonymous();
    }
}
