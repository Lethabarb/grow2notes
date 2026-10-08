using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The app's whole pipeline, with a test-only endpoint that throws and every log entry recorded. The tests host the
/// app without a database, so they run without Docker.
/// </summary>
public sealed class DatabaseFailureHandlerTests
{
    private static readonly string Canary = $"canary-{Guid.NewGuid():N}";

    // Production is what App Service runs. Development is where the app would show an exception that reached the
    // developer exception page.
    public static TheoryData<string> Environments { get; } = ["Development", "Production"];

    [Fact]
    public async Task A_database_failure_gets_a_500_problem_and_one_log_entry_with_the_type_and_SQL_error_number_only()
    {
        var failure = new DbUpdateException("An error occurred while saving the entity changes.",
            TestSqlException.Create(2601, $"Cannot insert duplicate key row. The duplicate key value is ({Canary})."));
        await using var app = new AppThatThrows(failure, "Production");
        using var client = app.CreateClient();

        using var response = await client.GetAsync(AppThatThrows.Path, TestContext.Current.CancellationToken);

        var body = await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(Canary, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(DbUpdateException), body, StringComparison.Ordinal);
        Assert.Equal("no-store", CacheControl(response));

        var entry = Assert.Single(app.Logs.Entries, e => e.Category == typeof(DatabaseFailureHandler).FullName);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(
            "Database failure: Microsoft.EntityFrameworkCore.DbUpdateException, SQL error 2601", entry.Message);
        Assert.Null(entry.Exception);

        // Nothing else logged the exception: not the exception handler middleware, not Kestrel.
        Assert.DoesNotContain(app.Logs.Entries, e =>
            e.Message.Contains(Canary, StringComparison.Ordinal) || e.Exception?.ToString().Contains(Canary) == true
            || e.Values.Any(v => v.Value?.ToString()?.Contains(Canary, StringComparison.Ordinal) == true));
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task Any_other_exception_gets_a_500_problem_without_its_text_and_is_logged_with_the_exception(
        string environment)
    {
        var exception = new InvalidOperationException($"Not a database failure: {Canary}.");
        await using var app = new AppThatThrows(exception, environment);
        using var client = app.CreateClient();

        using var response = await client.GetAsync(AppThatThrows.Path, TestContext.Current.CancellationToken);

        var body = await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(Canary, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.Equal("no-store", CacheControl(response));
        Assert.Contains(app.Logs.Entries, e => e.Level == LogLevel.Error && e.Exception == exception);
        Assert.DoesNotContain(app.Logs.Entries, e => e.Category == typeof(DatabaseFailureHandler).FullName);
    }

    // The header exactly as the app sent it. The typed CacheControl property would reformat it from parsed directives.
    private static string? CacheControl(HttpResponseMessage response) =>
        response.Headers.NonValidated.TryGetValues("Cache-Control", out var values) ? values.ToString() : null;

    private sealed class AppThatThrows(Exception exception, string environment) : WebApplicationFactory<Program>
    {
        // An endpoint that only this test maps, under /api, so its responses get the API's caching rule.
        public const string Path = "/api/test-only/throw";

        public RecordingLoggerProvider Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);

            // Hides any connection string on the machine, so the key ring's load fails at once instead of retrying
            // against a server.
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                [new("ConnectionStrings:Grow2Notes", null)]));

            builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(Logs));
            builder.ConfigureServices(
                services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoint(exception)));
        }

        /// <summary>
        /// Maps an endpoint that throws. It is routed ahead of the app's own routing, which then leaves the endpoint
        /// already chosen, so it runs where the app's own endpoints do: inside the app's exception handler.
        /// </summary>
        private sealed class TestOnlyEndpoint(Exception exception) : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                app.UseRouting();
                next(app);
                app.UseEndpoints(endpoints => endpoints.MapGet(Path, _ => throw exception));
            };
        }
    }
}
