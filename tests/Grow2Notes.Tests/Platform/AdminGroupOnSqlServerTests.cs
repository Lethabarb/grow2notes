using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The <c>/api/admin</c> group (design.md §2, §6.6), at test-only <c>GET</c> and <c>PUT</c> endpoints mapped in it,
/// called over HTTPS signed out and, with the test-only sign-in, as seeded organisation A's worker and manager and B's
/// manager: the group's <c>Manager</c> policy refuses the first two and admits both managers, and, as the group is
/// inside the <c>/api</c> group, a <c>PUT</c> still needs the antiforgery token and a JSON body or none (§9.8).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AdminGroupOnSqlServerTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/admin/test-only/in-the-group";

    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public AdminGroupOnSqlServerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new TestOnlyEndpoints())));
    }

    public static TheoryData<string, Caller, HttpStatusCode> Refusals { get; } = new()
    {
        { "GET", Caller.SignedOut, HttpStatusCode.Unauthorized },
        { "PUT", Caller.SignedOut, HttpStatusCode.Unauthorized },
        { "GET", Caller.WorkerOfA, HttpStatusCode.Forbidden },
        { "PUT", Caller.WorkerOfA, HttpStatusCode.Forbidden },
    };

    public static TheoryData<string, Caller> Admissions { get; } = new()
    {
        { "GET", Caller.ManagerOfA },
        { "PUT", Caller.ManagerOfA },
        { "GET", Caller.ManagerOfB },
        { "PUT", Caller.ManagerOfB },
    };

    // A PUT sends the caller's own token, as the SPA does, so only the policy can refuse it.
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_caller_the_group_refuses_gets_a_problem_that_is_never_stored(
        string method, Caller caller, HttpStatusCode status)
    {
        using var client = ClientFor(caller);
        using var request = await RequestAsync(client, method);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(status);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [MemberData(nameof(Admissions))]
    public async Task A_manager_of_either_organisation_reaches_the_endpoint(string method, Caller caller)
    {
        using var client = ClientFor(caller);
        using var request = await RequestAsync(client, method);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // With the antiforgery cookie, as a browser sends it with a forged request, but no token.
    [Fact]
    public async Task A_PUT_without_the_antiforgery_token_gets_a_400_problem_with_no_code_that_is_never_stored()
    {
        using var client = ClientFor(Caller.ManagerOfA);
        await client.FetchTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, Path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.BadRequest));
        Assert.False(problem.RootElement.TryGetProperty("code", out _));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // A form post (design.md §9.8 item 3), with everything else a request needs to get through.
    [Fact]
    public async Task A_PUT_with_a_form_body_gets_a_415_problem_with_no_code_that_is_never_stored()
    {
        using var client = ClientFor(Caller.ManagerOfA);
        using var request = await RequestAsync(client, "PUT");
        request.Content = new FormUrlEncodedContent([new("name", "value")]);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnsupportedMediaType));
        Assert.False(problem.RootElement.TryGetProperty("code", out _));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private HttpClient ClientFor(Caller caller) => app.CreateHttpsClient().SignInAs(caller, seeded);

    // A PUT carries a token that the client fetched as the user it calls as, as the SPA fetches one after sign-in.
    private static async Task<HttpRequestMessage> RequestAsync(HttpClient client, string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), Path);
        if (HttpMethods.IsPut(method))
        {
            request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        }

        return request;
    }

    /// <summary>
    /// Maps a <c>GET</c> and a <c>PUT</c> at <see cref="Path"/>, in the app's <c>/api/admin</c> group, each answering
    /// <c>204</c>. Neither has a policy of its own nor is <c>AllowAnonymous</c>, so the fallback policy, which admits a
    /// worker, would apply to them but for the group's. They are routed ahead of the app's own routing, which then
    /// leaves the endpoint already chosen, so they run where the app's own endpoints do, after the app's authorization.
    /// </summary>
    private sealed class TestOnlyEndpoints : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints =>
            {
                var admin = endpoints.MapApiGroup().MapAdminGroup();
                admin.MapGet("/test-only/in-the-group", () => TypedResults.NoContent());
                admin.MapPut("/test-only/in-the-group", () => TypedResults.NoContent());
            });
        };
    }
}
