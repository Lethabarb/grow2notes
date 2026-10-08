using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// Each policy through the app's pipeline, at a test-only endpoint behind it, called signed out and, with the test-only
/// sign-in, as seeded organisation A's worker and manager and B's manager. <see cref="PoliciesTests"/> covers the
/// policies without a database.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PoliciesOnSqlServerTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    // Endpoints that only this test maps, under /api, so their responses get the API's caching rule. No real endpoint
    // needs a signed-in user yet.
    private const string FallbackPolicyPath = "/api/test-only/fallback-policy";
    private const string ManagerPolicyPath = "/api/test-only/manager-policy";

    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public PoliciesOnSqlServerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoints())));
    }

    public enum Caller
    {
        SignedOut,
        WorkerOfA,
        ManagerOfA,
        ManagerOfB,
    }

    public static TheoryData<string, Caller, HttpStatusCode> Refusals { get; } = new()
    {
        { FallbackPolicyPath, Caller.SignedOut, HttpStatusCode.Unauthorized },
        { ManagerPolicyPath, Caller.SignedOut, HttpStatusCode.Unauthorized },
        { ManagerPolicyPath, Caller.WorkerOfA, HttpStatusCode.Forbidden },
    };

    public static TheoryData<string, Caller> Admissions { get; } = new()
    {
        { FallbackPolicyPath, Caller.WorkerOfA },
        { FallbackPolicyPath, Caller.ManagerOfA },
        { FallbackPolicyPath, Caller.ManagerOfB },
        { ManagerPolicyPath, Caller.ManagerOfA },
        { ManagerPolicyPath, Caller.ManagerOfB },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_caller_the_policy_refuses_gets_a_problem_that_is_never_stored(
        string path, Caller caller, HttpStatusCode status)
    {
        using var client = ClientFor(caller);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(status);
        Assert.Equal("no-store", CacheControl(response));
    }

    [Theory]
    [MemberData(nameof(Admissions))]
    public async Task A_caller_the_policy_admits_reaches_the_endpoint(string path, Caller caller)
    {
        using var client = ClientFor(caller);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private HttpClient ClientFor(Caller caller)
    {
        var client = app.CreateClient();
        return caller switch
        {
            Caller.SignedOut => client,
            Caller.WorkerOfA => client.SignInAs(seeded.A.WorkerId),
            Caller.ManagerOfA => client.SignInAs(seeded.A.ManagerId),
            Caller.ManagerOfB => client.SignInAs(seeded.B.ManagerId),
            _ => throw new ArgumentOutOfRangeException(nameof(caller), caller, null),
        };
    }

    // The header exactly as the app sent it. The typed CacheControl property would reformat it from parsed directives.
    private static string? CacheControl(HttpResponseMessage response) =>
        response.Headers.NonValidated.TryGetValues("Cache-Control", out var values) ? values.ToString() : null;

    /// <summary>
    /// Maps an endpoint with no policy of its own, which the fallback policy covers, and one with the
    /// <see cref="Policies.Manager"/> policy. They are routed ahead of the app's own routing, which then leaves the
    /// endpoint already chosen, so they run where the app's own endpoints do, after the app's authorization.
    /// </summary>
    private sealed class TestOnlyEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet(FallbackPolicyPath, () => TypedResults.NoContent());
                endpoints.MapGet(ManagerPolicyPath, () => TypedResults.NoContent())
                    .RequireAuthorization(Policies.Manager);
            });
        };
    }
}
