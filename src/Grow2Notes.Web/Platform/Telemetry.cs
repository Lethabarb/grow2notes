using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Sends the server's requests, dependencies, exceptions and logs to Application Insights through the Azure Monitor
/// OpenTelemetry distro (design.md §9.5), when <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set, as it is in Azure.
/// Local development, the tests and CI have no Application Insights, so they register no telemetry at all.
/// </summary>
/// <remarks>
/// Profiler and Snapshot Debugger stay off, because snapshots capture variables, which here means note text (design.md
/// §9.5). Their packages are not referenced, and App Service's own Application Insights agent runs only when the
/// <c>ApplicationInsightsAgent_EXTENSION_VERSION</c> app setting turns it on. main.bicep does not set it, and every
/// deploy replaces the whole set of app settings (app.bicep), so turning it on in the portal lasts only until then.
/// </remarks>
internal static class Telemetry
{
    // The distro's own name for the setting. App Service passes it as an environment variable, which configuration
    // includes.
    private const string ConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    public static IServiceCollection AddTelemetryWhenConfigured(
        this IServiceCollection services, IConfiguration configuration)
    {
        // The distro would find the setting by itself, but it cannot start without one. Reading it here, once, lets
        // the same value decide whether telemetry is on and where it goes.
        var connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrEmpty(connectionString))
        {
            return services;
        }

        // Local auth is off for ingestion, so Application Insights accepts only an Entra token from an identity with
        // Monitoring Metrics Publisher, which the app's user-assigned identity has (design.md §9.4). Without its client
        // ID the credential would look for a system-assigned identity, which the app does not have, so the app would
        // start and its telemetry would fail to send, unseen, instead of the app failing here.
        var clientId = configuration["ManagedIdentity:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
                "APPLICATIONINSIGHTS_CONNECTION_STRING is set but ManagedIdentity:ClientId is not. Set it to the " +
                "client ID of the app's user-assigned managed identity, which sends the telemetry.");
        }

        services.AddOpenTelemetry().UseAzureMonitor(azureMonitor =>
        {
            azureMonitor.ConnectionString = connectionString;
            azureMonitor.Credential =
                new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
            // Live Metrics streams sample requests, dependencies and exceptions, messages included, straight to the
            // portal, outside the workspace whose access, retention and cap design.md §9.5 sets. The design does not
            // ask for it.
            azureMonitor.EnableLiveMetrics = false;
        });
        return services;
    }
}
