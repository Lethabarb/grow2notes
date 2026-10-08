using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        await using var app = new AppThatThrows(_ => throw failure, "Production");
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
        Assert.DoesNotContain(app.Logs.Entries, HoldsCanary);
    }

    [Fact]
    public async Task A_database_failure_after_the_client_has_gone_is_not_logged_and_ends_with_a_499()
    {
        // What a SaveChanges throws when the request's cancellation stops its SQL command: SqlClient's error 0.
        var failure = new DbUpdateException("An error occurred while saving the entity changes.",
            TestSqlException.Create(0, $"A severe error occurred on the current command. {Canary}"));
        await using var app = new AppThatThrows(context =>
        {
            // As when the client goes, RequestAborted is cancelled before the SQL command fails.
            context.Abort();
            throw failure;
        }, "Production");
        using var client = app.CreateClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync(AppThatThrows.Path, TestContext.Current.CancellationToken));

        // The client gave up on the request at once, so wait for the app to finish with it before reading its logs.
        Assert.Equal(StatusCodes.Status499ClientClosedRequest,
            await app.Finished.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(app.Logs.Entries, e => e.Category == typeof(DatabaseFailureHandler).FullName);
        Assert.DoesNotContain(app.Logs.Entries, HoldsCanary);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    public async Task A_bad_request_exception_gets_a_problem_with_its_own_status_and_is_not_logged(
        HttpStatusCode status)
    {
        var exception = new BadHttpRequestException($"The request could not be read: {Canary}.", (int)status);
        await using var app = new AppThatThrows(_ => throw exception, "Production");
        using var client = app.CreateClient();

        using var response = await client.GetAsync(AppThatThrows.Path, TestContext.Current.CancellationToken);

        var body = await response.ReadProblemAsync(status);
        Assert.DoesNotContain(Canary, body, StringComparison.Ordinal);
        Assert.Equal("no-store", CacheControl(response));
        Assert.DoesNotContain(app.Logs.Entries, HoldsCanary);
    }

    [Fact]
    public async Task A_body_over_Kestrels_size_limit_gets_a_413_problem_and_is_not_logged()
    {
        const int Limit = 16;
        await using var app = new AppThatThrows(context => context.Request.Body.CopyToAsync(Stream.Null), "Production");

        // On Kestrel, at any free port, because the test server has no size limit. Kestrel throws as the body is read.
        app.UseKestrel(0);
        app.UseKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = Limit);
        using var client = app.CreateClient();
        using var body = new ByteArrayContent(new byte[Limit + 1]);

        using var response = await client.PostAsync(AppThatThrows.Path, body, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.RequestEntityTooLarge);
        Assert.DoesNotContain(app.Logs.Entries, e => e.Exception is BadHttpRequestException);
    }

    [Theory]
    [MemberData(nameof(Environments))]
    public async Task Any_other_exception_gets_a_500_problem_without_its_text_and_is_logged_with_the_exception(
        string environment)
    {
        var exception = new InvalidOperationException($"Not a database failure: {Canary}.");
        await using var app = new AppThatThrows(_ => throw exception, environment);
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

    private static bool HoldsCanary(RecordedLog entry) =>
        entry.Message.Contains(Canary, StringComparison.Ordinal)
        || entry.Exception?.ToString().Contains(Canary, StringComparison.Ordinal) == true
        || entry.Values.Any(v => v.Value?.ToString()?.Contains(Canary, StringComparison.Ordinal) == true);

    private sealed class AppThatThrows(RequestDelegate endpoint, string environment) : WebApplicationFactory<Program>
    {
        // An endpoint that only this test maps, under /api, so its responses get the API's caching rule.
        public const string Path = "/api/test-only/throw";

        private readonly TaskCompletionSource<int> finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingLoggerProvider Logs { get; } = new();

        /// <summary>The status the app ended its one request with, even with no client left to receive it.</summary>
        public Task<int> Finished => finished.Task;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);

            // Hides any connection string on the machine, so the key ring's load fails at once instead of retrying
            // against a server.
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                [new("ConnectionStrings:Grow2Notes", null)]));

            builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(Logs));
            builder.ConfigureServices(
                services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoint(endpoint, finished)));
        }

        /// <summary>
        /// Maps the endpoint for every method. It is routed ahead of the app's own routing, which then leaves the
        /// endpoint already chosen, so it runs where the app's own endpoints do: inside the app's exception handler.
        /// </summary>
        private sealed class TestOnlyEndpoint(RequestDelegate endpoint, TaskCompletionSource<int> finished)
            : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
            {
                // Outside the app's own middleware, so it sees the status the app ended with. In a finally, because
                // after an abort the test server throws on any write, such as the status code pages' problem body.
                app.Use(async (context, rest) =>
                {
                    try
                    {
                        await rest(context);
                    }
                    finally
                    {
                        finished.TrySetResult(context.Response.StatusCode);
                    }
                });
                app.UseRouting();
                next(app);
                app.UseEndpoints(endpoints => endpoints.Map(Path, endpoint).AllowAnonymous());
            };
        }
    }
}
