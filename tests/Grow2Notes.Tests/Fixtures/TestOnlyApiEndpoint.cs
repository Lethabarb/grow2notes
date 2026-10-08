using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Maps an endpoint in the app's <c>/api</c> group, at <see cref="Path"/>, which answers <c>204</c> to GET, POST, PUT
/// and DELETE once the group's filter lets a request through. Anyone may call it. It is routed ahead of the app's own
/// routing, which then leaves the endpoint already chosen, so it runs where the app's own endpoints do, after the app's
/// middleware.
/// </summary>
internal sealed class TestOnlyApiEndpoint : IStartupFilter
{
    public const string Path = "/api/test-only/in-the-group";

    /// <summary>Maps the endpoint in the app that <paramref name="builder"/> builds.</summary>
    public static void MapIn(IWebHostBuilder builder) =>
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new TestOnlyApiEndpoint()));

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseRouting();
        next(app);
        app.UseEndpoints(endpoints => endpoints.MapApiGroup()
            .MapMethods("/test-only/in-the-group", ["GET", "POST", "PUT", "DELETE"], () => TypedResults.NoContent())
            .AllowAnonymous());
    };
}
