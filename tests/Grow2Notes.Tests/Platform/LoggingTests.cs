using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using System.Net;
using Azure.Core;
using Azure.Core.Diagnostics;
using Azure.Core.Pipeline;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The logging settings of design.md §9.5, read from the app's own services in each environment. The tests host the
/// app without a database, so they run without Docker: they send it no requests, and Data Protection, which fails to
/// load its key ring as the app starts, logs that and carries on.
/// </summary>
public sealed class LoggingTests
{
    // From the range set aside for documentation, standing in for the app's address in its subnet.
    private const string SubnetAddress = "192.0.2.10";

    private const string TransportUpdateFailure = "Failed to update transport";

    // Production is what App Service runs, for test and prod alike, because main.bicep sets no ASPNETCORE_ENVIRONMENT.
    // Testing is what the integration tests run.
    public static TheoryData<string> OutsideDevelopment { get; } = ["Production", "Testing"];

    public static TheoryData<string> EveryEnvironment { get; } = ["Development", "Production", "Testing"];

    [Theory]
    [MemberData(nameof(OutsideDevelopment))]
    public async Task Logs_Warning_and_above_the_app_s_own_Information_and_nothing_from_EF_Core_s_SQL_categories(
        string environment)
    {
        await using var app = new AppWithoutDatabase(environment);
        var filters = app.Services.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>();

        // A provider with no rule of its own, as the console and telemetry's OpenTelemetry provider are, takes these
        // levels from the app's rules. On Windows the host also adds the Event Log provider, whose own Warning rule
        // replaces them; App Service runs the app on Linux, where that provider is not added.
        using var loggers = new LoggerFactory([new EveryLevelProvider()], filters);

        // ASP.NET Core logs every request under this category at Information.
        var framework = loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
        Assert.False(framework.IsEnabled(LogLevel.Information));
        Assert.True(framework.IsEnabled(LogLevel.Warning));

        var own = loggers.CreateLogger<MelbourneClock>();
        Assert.False(own.IsEnabled(LogLevel.Debug));
        Assert.True(own.IsEnabled(LogLevel.Information));

        // A SQL Server error message can hold the values that failed, such as a truncated value or a duplicate key. EF
        // Core logs the exception, message included, under Update when a save fails and under Query when a query or
        // ExecuteUpdate fails, and the SQL of a failed command under Database.Command. DatabaseFailureHandler logs
        // these failures instead.
        Assert.False(loggers.CreateLogger(DbLoggerCategory.Update.Name).IsEnabled(LogLevel.Critical));
        Assert.False(loggers.CreateLogger(DbLoggerCategory.Database.Command.Name).IsEnabled(LogLevel.Critical));
        Assert.False(loggers.CreateLogger(DbLoggerCategory.Query.Name).IsEnabled(LogLevel.Critical));

        // A rule that names the OpenTelemetry provider, by its alias or its type, would give telemetry levels of its
        // own. TelemetryTests checks that the distro adds no rule either.
        Assert.DoesNotContain(filters.CurrentValue.Rules,
            rule => rule.ProviderName?.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task With_telemetry_on_Azure_SDK_events_reach_logs_and_telemetry_only_from_Error()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var logs = new RecordingLoggerProvider();
        var exportedLogs = new LogCollector();
        await using var factory = new AppWithoutDatabase("Production");
        await using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.UseTelemetrySentTo(new IngestionRecorder());

            // Beside the console and the exporter, with no rules of their own, so they receive what those do.
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
            builder.ConfigureServices(services =>
                services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(exportedLogs)));
        });

        // Starting the app starts the distro's forwarder, which passes the Azure SDK's events to ILogger.
        _ = app.Services;

        var tenant = Guid.NewGuid().ToString();
        var raised = new ConcurrentQueue<(string Source, EventLevel Level, string Message)>();
        using (new AzureEventSourceListener(
            (e, text) => raised.Enqueue((e.EventSource.Name, e.Level, text)), EventLevel.Warning))
        {
            using var transport = new HttpClientTransport(new KeyVaultChallenge(tenant));
            var pipeline = HttpPipelineBuilder.Build(new KeyVaultClientOptions(transport));
            using var message = pipeline.CreateMessage();
            message.Request.Uri.Reset(new Uri("https://kv.example.invalid/keys/data-protection"));
            await pipeline.SendAsync(message, cancellationToken);
            Assert.Equal(401, message.Response.Status);

            // Azure.Core has few events at Error. Updating a transport that cannot make a new HttpClient raises one.
            transport.Update(new HttpPipelineTransportOptions());
        }

        // Azure.Core reported the 401 at Warning, with the tenant ID and subnet address, and the update at Error.
        Assert.Contains(raised, e => e is { Source: "Azure-Core", Level: EventLevel.Warning }
            && e.Message.Contains(tenant) && e.Message.Contains(SubnetAddress));
        Assert.Contains(raised, e => e is { Source: "Azure-Core", Level: EventLevel.Error }
            && e.Message.Contains(TransportUpdateFailure));

        // The Error reached the logger providers and the exporter, so the forwarder passed this app the events.
        Assert.Contains(logs.Entries, e => e is { Category: "Azure.Core", Level: LogLevel.Error }
            && e.Message.Contains(TransportUpdateFailure));
        Assert.Contains(exportedLogs.Logs,
            l => l.CategoryName == "Azure.Core" && l.FormattedMessage?.Contains(TransportUpdateFailure) == true);

        // The Warning reached neither.
        Assert.Empty(logs.Entries
            .Where(e => e.Category.StartsWith("Azure", StringComparison.Ordinal) && e.Level < LogLevel.Error)
            .Select(e => $"{e.Category} {e.Level}: {e.Message}"));
        Assert.DoesNotContain(
            logs.Entries.SelectMany(e => e.Values.Select(v => v.Value as string).Append(e.Message))
                .Concat(exportedLogs.Logs.SelectMany(
                    l => l.Attributes.Select(a => a.Value as string).Append(l.FormattedMessage).Append(l.Body))),
            text => text is not null && (text.Contains(tenant) || text.Contains(SubnetAddress)));
    }

    [Theory]
    [MemberData(nameof(EveryEnvironment))]
    public async Task Has_no_HTTP_logging_middleware(string environment)
    {
        await using var app = new AppWithoutDatabase(environment);

        // AddHttpLogging and AddW3CLogging register these options, and UseHttpLogging and UseW3CLogging stop the app
        // starting without what those calls register. With neither registered, neither middleware is in the pipeline.
        Assert.Empty(app.Services.GetServices<IConfigureOptions<HttpLoggingOptions>>());
        Assert.Empty(app.Services.GetServices<IConfigureOptions<W3CLoggerOptions>>());
    }

    // Development is left out: design.md §9.5 allows both there. The app turns them on nowhere.
    [Theory]
    [MemberData(nameof(OutsideDevelopment))]
    public async Task Both_contexts_have_sensitive_data_logging_and_detailed_errors_off_outside_Development(
        string environment)
    {
        await using var app = new AppWithoutDatabase(environment);
        using var scope = app.Services.CreateScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>(),
            scope.ServiceProvider.GetRequiredService<KeysDbContext>(),
        ];

        // Read from each context rather than from its registered options, so a setting made in OnConfiguring counts.
        Assert.All(contexts, db =>
        {
            var core = db.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();
            Assert.NotNull(core);
            Assert.False(core.IsSensitiveDataLoggingEnabled);
            Assert.False(core.DetailedErrorsEnabled);
        });
    }

    // Enabled at every level and logs nothing, so a logger made from it is enabled exactly where the rules allow.
    private sealed class EveryLevelProvider : ILoggerProvider, ILogger
    {
        public ILogger CreateLogger(string categoryName) => this;

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Answers as Key Vault answers a request that carries no token, as the Key Vault client's first request of each
    /// start does: a 401 whose challenge names the tenant, with network info that gives the caller's address.
    /// </summary>
    private sealed class KeyVaultChallenge(string tenant) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Headers.TryAddWithoutValidation("WWW-Authenticate",
                $"Bearer authorization=\"https://login.microsoftonline.com/{tenant}\", " +
                "resource=\"https://vault.azure.net\"");
            response.Headers.TryAddWithoutValidation("x-ms-keyvault-network-info",
                $"conn_type=Ipv4;addr={SubnetAddress};act_addr_fam=InterNetwork;");
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Logs the headers the Key Vault client's options do: Azure.Core's defaults, WWW-Authenticate among them, and Key
    /// Vault's network info.
    /// </summary>
    private sealed class KeyVaultClientOptions : ClientOptions
    {
        public KeyVaultClientOptions(HttpPipelineTransport transport)
        {
            Transport = transport;
            Diagnostics.LoggedHeaderNames.Add("x-ms-keyvault-network-info");
        }
    }

    private sealed class AppWithoutDatabase(string environment) : WebApplicationFactory<Program>
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
}
