using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Lets a test call the app from an address of its choice. The test server gives every request a null address, so a
/// request that carries <see cref="Name"/> gets that as its remote address, before anything of the app's runs; the
/// app's forwarded headers then apply to it as they do to a real one. An ordinary <see cref="HttpClient"/>, with its
/// cookies, antiforgery tokens and two-factor flows, can then call from any address, by a header in its defaults or
/// in each request.
/// </summary>
internal static class ClientAddressHeader
{
    public const string Name = "X-Test-Client-Address";

    /// <summary>Makes the app that is built from <paramref name="services"/> read the address from the header.</summary>
    public static IServiceCollection AddClientAddressHeader(this IServiceCollection services) =>
        services.AddSingleton<IStartupFilter>(new Filter());

    private sealed class Filter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (context.Request.Headers.TryGetValue(Name, out var address))
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
