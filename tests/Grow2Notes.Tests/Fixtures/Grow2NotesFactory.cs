using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Hosts the app in memory against <paramref name="sqlServer"/>, with the web project as its content root, so it serves
/// the real SPA build from that project's wwwroot.
/// </summary>
public sealed class Grow2NotesFactory(SqlServerFixture sqlServer) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development, whose settings point at LocalDB.
        builder.UseEnvironment("Testing");

        // MapStaticAssets reads the build's manifest here rather than the published one. Its gzip and Brotli copies of
        // each file sit under obj/, not wwwroot, and only Development maps them in by default; without this, a request
        // that accepts compression gets an empty body.
        builder.UseStaticWebAssets();

        // Added after the app's own configuration sources, so these also win over environment variables on the machine.
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
        [
            new("ConnectionStrings:Grow2Notes", sqlServer.ConnectionString),
            // With a build manifest, MapStaticAssets otherwise rewrites responses for hot reload, compressing files on
            // the fly. Off, it serves the precompressed files and the manifest's headers, as production does.
            new("ReloadStaticAssetsAtRuntime", "false"),
        ]));
    }
}
