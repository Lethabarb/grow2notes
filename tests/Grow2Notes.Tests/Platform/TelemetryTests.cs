using System.Diagnostics;
using System.Reflection;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// These tests build the services without starting a host. The distro adds its exporters to the built providers when
/// the host starts, so no exporter is built, no token is requested and nothing is sent. A test that needs to see what
/// an exporter would see adds a collector to the built provider in the same way.
/// </summary>
public sealed class TelemetryTests
{
    // No such resource exists, and nothing here sends to it.
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000002;IngestionEndpoint=https://ingestion.example.invalid/";

    private const string ClientId = "00000000-0000-0000-0000-000000000001";

    [Fact]
    public void Without_a_connection_string_nothing_is_registered()
    {
        var services = new ServiceCollection();

        services.AddTelemetryWhenConfigured(Configuration(("ManagedIdentity:ClientId", ClientId)));

        Assert.Empty(services);
    }

    [Fact]
    public void With_a_connection_string_and_a_client_ID_telemetry_is_sent_as_the_app_identity()
    {
        var azureMonitor = AzureMonitorWith(
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString),
            ("ManagedIdentity:ClientId", ClientId));

        Assert.Equal(ConnectionString, azureMonitor.ConnectionString);
        var credential = Assert.IsType<ManagedIdentityCredential>(azureMonitor.Credential);
        Assert.Equal($"ClientId {ClientId}", IdentityOf(credential));
    }

    [Fact]
    public void Live_Metrics_is_off()
    {
        var azureMonitor = AzureMonitorWith(
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString),
            ("ManagedIdentity:ClientId", ClientId));

        Assert.False(azureMonitor.EnableLiveMetrics);
    }

    [Fact]
    public void Telemetry_adds_no_logging_rules_so_its_logger_follows_the_app_s_levels()
    {
        using var services = ServicesWith(
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString),
            ("ManagedIdentity:ClientId", ClientId));

        // A rule here could give the OpenTelemetry logger provider levels of its own, apart from appsettings.json's
        // (design.md §9.5). LoggingTests checks that the app's settings name no such rule either.
        Assert.Empty(services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value.Rules);
    }

    [Fact]
    public void A_failed_SqlClient_command_reaches_the_exporters_with_its_error_status_and_no_description()
    {
        using var services = ServicesWith(
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString),
            ("ManagedIdentity:ClientId", ClientId));
        var exported = new SpanCollector();
        services.GetRequiredService<TracerProvider>().AddProcessor(exported);

        // Spans from SqlClient anywhere in the process reach the provider; this database name picks out this test's.
        var database = $"telemetry-test-{Guid.NewGuid():N}";
        using var connection = new SqlConnection($"Data Source=unused.invalid;Initial Catalog={database}");
        using var command = new SqlCommand("SELECT 1", connection);

        // SqlClient fails a command on a closed connection itself, so no server is needed. The instrumentation records
        // it as it records an error from SQL Server, with the exception's message as the description.
        Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

        var span = Assert.Single(exported.Spans, s => Equals(s.GetTagItem("db.name"), database));
        Assert.Equal("mssql", span.GetTagItem("db.system"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Null(span.StatusDescription);
    }

    [Fact]
    public void A_database_failure_logged_with_its_exception_reaches_the_exporters_with_the_type_and_number_only()
    {
        using var services = ServicesWith(
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString),
            ("ManagedIdentity:ClientId", ClientId));
        var exported = new LogCollector();
        services.GetRequiredService<LoggerProvider>().AddProcessor(exported);
        var failure = TestSqlException.Create(2601, $"The duplicate key value is (canary-{Guid.NewGuid():N}).");

        // As the health check service logs a check that threw.
        services.GetRequiredService<ILogger<TelemetryTests>>()
            .LogError(failure, "Health check {HealthCheckName} threw an unhandled exception", "database");

        var log = Assert.Single(exported.Logs, l => l.CategoryName == typeof(TelemetryTests).FullName);
        Assert.Null(log.Exception);
        Assert.Contains(new("SqlErrorNumber", 2601), log.Attributes);
    }

    [Fact]
    public void A_connection_string_without_a_client_ID_stops_the_app_starting()
    {
        var configuration = Configuration(("APPLICATIONINSIGHTS_CONNECTION_STRING", ConnectionString));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddTelemetryWhenConfigured(configuration));
        Assert.Contains("ManagedIdentity:ClientId is not", exception.Message);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

    // The distro also reads configuration from the services, as it would from the host's.
    private static ServiceProvider ServicesWith(params (string Key, string Value)[] settings)
    {
        var configuration = Configuration(settings);
        return new ServiceCollection()
            .AddSingleton(configuration)
            .AddTelemetryWhenConfigured(configuration)
            .BuildServiceProvider();
    }

    private static AzureMonitorOptions AzureMonitorWith(params (string Key, string Value)[] settings)
    {
        using var services = ServicesWith(settings);
        return services.GetRequiredService<IOptions<AzureMonitorOptions>>().Value;
    }

    // ManagedIdentityCredential does not say publicly which identity it signs in as. Its internal ManagedIdentityId
    // does, in ToString, so this reads that; if an Azure.Core or Azure.Identity update moves it, the test fails rather
    // than passing unchecked.
    private static string? IdentityOf(ManagedIdentityCredential credential)
    {
        const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;
        var client = typeof(ManagedIdentityCredential).GetProperty("Client", Internal)?.GetValue(credential);
        return client?.GetType().GetProperty("ManagedIdentityId", Internal)?.GetValue(client)?.ToString();
    }
}
