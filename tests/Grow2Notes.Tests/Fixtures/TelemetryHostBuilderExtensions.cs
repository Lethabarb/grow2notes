using Azure.Core.Pipeline;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

public static class TelemetryHostBuilderExtensions
{
    private const string ClientId = "00000000-0000-0000-0000-000000000001";

    /// <summary>
    /// Turns the app's telemetry on, as it is in Azure, with what the Azure Monitor exporters would send to Application
    /// Insights going to <paramref name="ingestion"/> instead.
    /// </summary>
    public static IWebHostBuilder UseTelemetrySentTo(this IWebHostBuilder builder, IngestionRecorder ingestion)
    {
        // The settings App Service passes. Program.cs reads them as it registers services, too early for configuration
        // added the way Grow2NotesFactory adds its own; WebApplicationFactory passes host settings to Program.cs as
        // command-line arguments. No such resource exists, and nothing is sent to it. The exporter keeps one transmitter
        // per connection string for the life of the process, built with the first app's transport, so each app has a
        // key of its own; otherwise its telemetry would go to an earlier test's recorder.
        builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING",
            $"InstrumentationKey={Guid.NewGuid()};IngestionEndpoint=https://ingestion.example.invalid/");
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
