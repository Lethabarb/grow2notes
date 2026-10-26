using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;

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
        // The stores save through the scoped Grow2NotesDbContext, so the operator command's changes join its
        // transaction. The entity cache lasts one scope, and a retried transaction would otherwise get back an
        // application that its rolled-back attempt had removed.
        services.AddOpenIddict().AddCore(core =>
        {
            core.UseEntityFrameworkCore().UseDbContext<Grow2NotesDbContext>().ReplaceDefaultEntities<Guid>();
            core.DisableEntityCaching();
        });
        return services;
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
