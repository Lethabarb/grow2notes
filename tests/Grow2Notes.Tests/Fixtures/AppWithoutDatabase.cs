using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Hosts the app in memory in <paramref name="environment"/> with no database, for a test that only reads the app's
/// services or settings, so it runs without Docker. Data Protection fails to load its key ring as the app starts, logs
/// that and carries on, and making a context or building its model opens no connection.
/// </summary>
public sealed class AppWithoutDatabase(string environment) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        // Hides the connection string of Development and any on the machine, so the key ring's load fails at once
        // instead of retrying against a server.
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            [new("ConnectionStrings:Grow2Notes", null)]));
    }
}
