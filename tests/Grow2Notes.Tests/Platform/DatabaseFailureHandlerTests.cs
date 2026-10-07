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
/// The app's whole pipeline, with a request that throws where an endpoint would and every log entry recorded. The
/// tests host the app without a database, so they run without Docker.
/// </summary>
public sealed class DatabaseFailureHandlerTests
{
    private static readonly string Canary = $"canary-{Guid.NewGuid():N}";

    [Fact]
    public async Task A_database_failure_gets_an_empty_500_and_one_log_entry_with_the_type_and_SQL_error_number_only()
    {
        var failure = new DbUpdateException("An error occurred while saving the entity changes.",
            TestSqlException.Create(2601, $"Cannot insert duplicate key row. The duplicate key value is ({Canary})."));
        await using var app = new AppThatThrows(failure);
        using var client = app.CreateClient();

        using var response = await client.GetAsync(AppThatThrows.Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

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

    [Fact]
    public async Task Any_other_exception_still_gets_an_empty_500_and_is_logged_with_the_exception()
    {
        var exception = new InvalidOperationException("Not a database failure.");
        await using var app = new AppThatThrows(exception);
        using var client = app.CreateClient();

        using var response = await client.GetAsync(AppThatThrows.Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(app.Logs.Entries, e => e.Level == LogLevel.Error && e.Exception == exception);
        Assert.DoesNotContain(app.Logs.Entries, e => e.Category == typeof(DatabaseFailureHandler).FullName);
    }

    private sealed class AppThatThrows(Exception exception) : WebApplicationFactory<Program>
    {
        // A file name that is neither a static asset nor a client route, so no endpoint matches it.
        public const string Path = "/no-such-file.txt";

        public RecordingLoggerProvider Logs { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Hides any connection string on the machine, so the key ring's load fails at once instead of retrying
            // against a server.
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                [new("ConnectionStrings:Grow2Notes", null)]));

            builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(Logs));
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter>(new ThrowAtTheEnd(exception)));
        }

        // Middleware added after the app's own runs for a request that no endpoint matched, inside the app's exception
        // handler, as an endpoint does.
        private sealed class ThrowAtTheEnd(Exception exception) : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                next(app);
                app.Run(_ => throw exception);
            };
        }
    }
}
