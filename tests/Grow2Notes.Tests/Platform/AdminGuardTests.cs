using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The admin guard (design.md §2, §6.6, §14 M1): every endpoint in the app's <c>EndpointDataSource</c> whose route
/// starts with <c>/api/admin</c> must name its methods, none of them <c>DELETE</c>, as one that names none answers
/// <c>DELETE</c> too; must carry the <see cref="Policies.Manager"/> policy, which the <c>/api/admin</c> group
/// (<see cref="ApiGroup.MapAdminGroup"/>) gives each endpoint in it; and must not be <c>AllowAnonymous</c>, which lets
/// any caller past every policy. The app is hosted as it is mapped in production, without a database, so the tests run
/// without Docker. Until the first admin endpoint, the app maps none, so test-only endpoints show the guard finding
/// each rule broken.
/// </summary>
public sealed class AdminGuardTests : IAsyncDisposable
{
    private readonly AppWithoutDatabase app = new("Production");

    [Fact]
    public void Every_admin_endpoint_names_its_methods_but_not_DELETE_and_admits_signed_in_managers_only()
    {
        var breaches = BreachesOf(EndpointsOf(app));

        // Every breach at once, rather than the first.
        Assert.True(
            breaches is [], string.Join(Environment.NewLine, ["Admin endpoints that break the rules:", .. breaches]));
    }

    [Fact]
    public async Task The_guard_names_each_admin_endpoint_that_breaks_a_rule_and_no_other()
    {
        await using var withEndpoints = app.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoints())));

        var breaches = BreachesOf(EndpointsOf(withEndpoints));

        Assert.Equal(
            [
                "DELETE /api/admin/test-only/delete: names DELETE",
                "GET /api/admin/test-only/anonymous: is AllowAnonymous",
                "PUT /api/admin/test-only/outside-the-group: lacks the Manager policy",
                "any method /api/admin/test-only/any-method: names no method, so answers DELETE too",
            ],
            breaches.Order(StringComparer.Ordinal));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static EndpointDataSource EndpointsOf(WebApplicationFactory<Program> app) =>
        app.Services.GetRequiredService<EndpointDataSource>();

    /// <summary>
    /// Each endpoint under <c>/api/admin</c> in <paramref name="endpoints"/> that breaks a rule, with every rule it
    /// breaks.
    /// </summary>
    private static List<string> BreachesOf(EndpointDataSource endpoints) =>
    [
        .. from endpoint in endpoints.Endpoints.OfType<RouteEndpoint>()
           where EndpointMatrix.IsAdmin(endpoint.RoutePattern.RawText ?? "")
           let broken = RulesBrokenBy(endpoint).ToList()
           where broken is not []
           select $"{NameOf(endpoint)}: {string.Join(", ", broken)}"
    ];

    private static IEnumerable<string> RulesBrokenBy(RouteEndpoint endpoint)
    {
        var methods = EndpointMatrix.MethodsOf(endpoint);
        if (methods is [])
        {
            yield return "names no method, so answers DELETE too";
        }

        if (methods.Any(HttpMethods.IsDelete))
        {
            yield return "names DELETE";
        }

        if (!endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy == Policies.Manager))
        {
            yield return "lacks the Manager policy";
        }

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            yield return "is AllowAnonymous";
        }
    }

    private static string NameOf(RouteEndpoint endpoint)
    {
        var methods = EndpointMatrix.MethodsOf(endpoint);
        return $"{(methods is [] ? "any method" : string.Join(" or ", methods))} {endpoint.RoutePattern.RawText}";
    }

    /// <summary>
    /// Maps, beside the app's endpoints, one that breaks each rule, as a story that got its admin endpoint wrong would:
    /// a <c>DELETE</c> in the <c>/api/admin</c> group; a <c>PUT</c> at an <c>/api/admin</c> path in the <c>/api</c>
    /// group but outside the admin one, with no policy of its own; a <c>GET</c> in the admin group marked
    /// <c>AllowAnonymous</c>; and one in the admin group mapped with <c>Map</c>, which names no method. Beside them, a
    /// <c>PUT</c> in the admin group, mapped as every admin endpoint should be, which the guard passes over. They are
    /// listed, never called.
    /// </summary>
    private sealed class TestOnlyEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints =>
            {
                var api = endpoints.MapApiGroup();
                var admin = api.MapAdminGroup();
                admin.MapDelete("/test-only/delete", () => TypedResults.NoContent());
                api.MapPut("/admin/test-only/outside-the-group", () => TypedResults.NoContent());
                admin.MapGet("/test-only/anonymous", () => TypedResults.NoContent()).AllowAnonymous();
                admin.Map("/test-only/any-method", () => TypedResults.NoContent());
                admin.MapPut("/test-only/as-it-should-be", () => TypedResults.NoContent());
            });
        };
    }
}
