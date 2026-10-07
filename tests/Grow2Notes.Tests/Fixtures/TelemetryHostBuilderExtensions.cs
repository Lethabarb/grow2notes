using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

public static class TelemetryHostBuilderExtensions
{
    // No such resource exists, and nothing is sent to it.
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000002;IngestionEndpoint=https://ingestion.example.invalid/";

    private const string ClientId = "00000000-0000-0000-0000-000000000001";

    /// <summary>
    /// Turns the app's telemetry on, as it is in Azure, with what the Azure Monitor exporters would send to Application
    /// Insights going to <paramref name="ingestion"/> instead.
    /// </summary>
    public static IWebHostBuilder UseTelemetrySentTo(this IWebHostBuilder builder, IngestionRecorder ingestion)
    {
        // The settings App Service passes. Program.cs reads them as it registers services, too early for configuration
        // added the way Grow2NotesFactory adds its own; WebApplicationFactory passes host settings to Program.cs as
        // command-line arguments.
        builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString);
        builder.UseSetting("ManagedIdentity:ClientId", ClientId);

        // The distro's own exporters still run. Without a credential, which would ask the machine's managed identity
        // endpoint for a token, and without offline storage, which would keep unsent telemetry on disk.
        return builder.ConfigureServices(services => services.PostConfigureAll<AzureMonitorExporterOptions>(options =>
        {
            options.Transport = new HttpClientTransport(ingestion);
            options.Credential = null;
            options.DisableOfflineStorage = true;
        }));
    }
}
