using System.Globalization;
using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// Each policy through the app's pipeline, at a test-only endpoint behind it, called signed out and, with the test-only
/// sign-in, as seeded organisation A's worker and manager and B's manager; and every endpoint that is not
/// <c>AllowAnonymous</c>, called as a seeded user whose role claim no policy names. <see cref="PoliciesTests"/> covers
/// the policies without a database.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PoliciesOnSqlServerTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    // Endpoints that only this test maps, each behind one policy and nothing else, under /api, so their responses get
    // the API's caching rule.
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

    // Roles are named, never inferred (mcp-server.md §3.5, convention 4): Release 2's Support, which UserRole does not
    // have, Worker's name in the wrong case, Worker's stored value, and no role claim at all.
    public static TheoryData<string> RolesNoPolicyNames { get; } = new()
    {
        "Support",
        "worker",
        ((int)UserRole.Worker).ToString(CultureInfo.InvariantCulture),
        TestSignIn.NoRole,
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

    [Theory]
    [MemberData(nameof(RolesNoPolicyNames))]
    public async Task A_role_no_policy_names_is_refused_by_every_endpoint_that_is_not_anonymous(string role)
    {
        // A's manager, whom every policy admits with the role on their row, so only the replaced role can refuse them.
        using var client = ClientFor(Caller.ManagerOfA);
        client.DefaultRequestHeaders.Add(TestSignIn.RoleHeader, role);
        var calls = CallsToEveryEndpointThatIsNotAnonymous();
        // So that the test cannot pass by calling nothing.
        Assert.Contains(calls, call => call.Endpoint.RoutePattern.RawText == FallbackPolicyPath);

        foreach (var call in calls)
        {
            using var request = await call.RequestAsync();
            // Before sending, which makes the request's URI absolute.
            var name = $"{call.Method} {request.RequestUri}";

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            // The request is on both sides, so a failure names the request that got through.
            Assert.Equal($"{name}: 403", $"{name}: {(int)response.StatusCode}");
            await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        }
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private HttpClient ClientFor(Caller caller) => app.CreateClient().SignInAs(caller, seeded);

    // The endpoint matrix's calls of the endpoints that the app and this test map, except those marked AllowAnonymous,
    // which authorization lets anyone call. Each of the app's endpoints is called as its matrix row builds the request,
    // which gives any route parameter a value, and this test's own at their route patterns.
    private List<EndpointCall> CallsToEveryEndpointThatIsNotAnonymous() =>
    [
        .. EndpointMatrix.CallsTo(app.Services.GetRequiredService<EndpointDataSource>())
            .Where(call => call.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null),
    ];

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
