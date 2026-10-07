using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

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

        services.AddOpenTelemetry()
            // SQL Server's error text can hold the values that failed, so these strip it (design.md §9.5). They are
            // added as the providers are built, so they run before the distro's exporters, which it adds to the built
            // providers when the host starts.
            .WithTracing(tracing => tracing.AddProcessor(new DatabaseSpanProcessor()))
            .WithLogging(logging => logging.AddProcessor(new DatabaseFailureLogProcessor()))
            // The distro also passes the Azure SDK's own events, from Warning up, to ILogger, under categories named
            // after their event sources, such as Azure.Core. Azure.Core's warnings list response headers: Key Vault's
            // answer to the first request of each start, a 401, carries the tenant ID in its challenge and the app's
            // subnet address in its network info. appsettings.json sets every category starting with Azure to Error,
            // so none of them reaches a log or telemetry. Its rule names Azure, not Azure.Core: a rule starting with
            // "Azure." would make the distro listen to every event, down to Verbose, and pass them all to ILogger.
            .UseAzureMonitor(azureMonitor =>
            {
                azureMonitor.ConnectionString = connectionString;
                azureMonitor.Credential =
                    new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId));
                // Live Metrics streams sample requests, dependencies and exceptions, messages included, straight to
                // the portal, outside the workspace whose access, retention and cap design.md §9.5 sets. The design
                // does not ask for it.
                azureMonitor.EnableLiveMetrics = false;
                // Every log entry is exported, whether or not its trace was sampled. Otherwise the exporter drops the
                // entries of traces the sampler left out, and the default sampler, a limit of 5 traces a second, leaves
                // some out even at low traffic, as it does just after a start, so a failed request's one entry,
                // DatabaseFailureHandler's, could be lost. The logs are few, Warning and up and the app's own
                // Information, and the workspace's daily cap bounds what they cost.
                azureMonitor.EnableTraceBasedLogsSampler = false;
            });
        return services;
    }
}
