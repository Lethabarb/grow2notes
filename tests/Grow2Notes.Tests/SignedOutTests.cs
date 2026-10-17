using System.Net;
using System.Net.Http.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests;

/// <summary>
/// A signed-out caller at each endpoint the app maps, with the app hosted as it is deployed: without the test-only
/// sign-in, so with the session cookie as the default scheme. A request that the fallback policy or the
/// <c>Manager</c> policy refuses is challenged by the cookie, and gets a <c>401</c> problem, as a test-only endpoint
/// behind the fallback policy, the app's own <c>/api/auth/me</c>, its admin lists, its <c>/api/admin/users</c> posted
/// to, and a user's setup link, deactivation and reactivation show; so the others show that the page, its files and
/// the health endpoints are <c>AllowAnonymous</c>, and that a request that no file, page or API endpoint answers still
/// matches one, and gets a <c>404</c>. The app has no database here, so they run without Docker: a request with no
/// cookie never reaches one.
/// </summary>
public sealed class SignedOutTests : IAsyncDisposable
{
    // Under /api, so it gets the API's caching rule, as the app's own endpoints there do.
    private const string FallbackPolicyPath = "/api/test-only/fallback-policy";

    public static MatrixTheoryData<string, string> NoSuchFiles { get; } =
        new(["GET", "POST", "PUT", "DELETE"], ["/favicon.ico", "/assets/no-such-file.js"]);

    // The client-route fallback answers GET and HEAD only.
    public static MatrixTheoryData<string, string> ClientRoutesWithOtherMethods { get; } =
        new(["POST", "PUT", "DELETE", "OPTIONS"], ["/", "/manage/anything"]);

    public static MatrixTheoryData<string, string> UnknownApiRequests { get; } =
        new(["GET", "POST", "PUT", "DELETE"], ["/api", "/api/no-such-endpoint"]);

    private readonly AppWithoutDatabase app = new("Production");

    // The cookie challenges with a bare 401, which the status code pages make problem details, rather than redirecting
    // to a login page, which the app does not have (design.md §7.2). Without the app's OnRedirectToLogin, the cookie
    // handler still answers 401, but with a Location naming /Account/Login, so the Location is the check. The session's
    // start-up (design.md §6.2) is the first of the app's own endpoints that needs a signed-in user.
    [Theory]
    [InlineData(FallbackPolicyPath)]
    [InlineData("/api/auth/me")]
    public async Task A_request_the_fallback_policy_refuses_gets_a_401_problem_that_is_never_stored(string path)
    {
        await using var withEndpoint = app.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<IStartupFilter>(new FallbackPolicyEndpoint())));
        using var client = withEndpoint.CreateClient(new() { AllowAutoRedirect = false });

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
    }

    // The /api/admin group's Manager policy, like the fallback policy, needs a signed-in user, so the cookie challenges
    // a request with no session there too, before the endpoint, which would need the database that is not here.
    [Theory]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/participants")]
    [InlineData("/api/admin/participants?includeArchived=true")]
    public async Task An_admin_list_with_no_session_gets_a_401_problem_that_is_never_stored(string path)
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
    }

    // As the list's, over HTTPS with no antiforgery token, which the /api group's check would answer with a 400: the
    // Manager policy refuses it first, so the endpoint, which would need the database that is not here, invites no one.
    [Fact]
    public async Task Invite_user_with_no_session_gets_a_401_problem_that_is_never_stored()
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.PostAsJsonAsync(
            "/api/admin/users",
            new { displayName = "Avery Brooks", email = "avery.brooks@example.org", role = "Worker" },
            TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
    }

    // As Invite user's, for an ID that no user has: the Manager policy refuses it first, so the endpoint, which would
    // need the database that is not here, finds, changes and sends nothing.
    [Theory]
    [InlineData("setup-link")]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    public async Task A_change_to_a_user_with_no_session_gets_a_401_problem_that_is_never_stored(string action)
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.PostAsync(
            $"/api/admin/users/{Guid.NewGuid()}/{action}", content: null, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
    }

    // Over HTTPS, as the SPA calls it, with no antiforgery token, which the /api group's check would answer with a 400.
    // The fallback policy refuses it first, so the endpoint, which would need the database that is not here, never
    // runs and writes nothing; and with no session cookie, none is deleted.
    [Fact]
    public async Task Sign_out_with_no_session_gets_a_401_problem_with_no_Set_Cookie_or_Clear_Site_Data()
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.PostAsync(
            "/api/auth/logout", content: null, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(response.SetCookies());
        Assert.False(response.Headers.Contains("Clear-Site-Data"));
    }

    // As Sign out's, over HTTPS with no antiforgery token: the fallback policy refuses it before the /api group's
    // check, and with no session cookie, none is renewed or deleted.
    [Fact]
    public async Task Stay_signed_in_with_no_session_gets_a_401_problem_with_no_Set_Cookie()
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.PostAsync(
            "/api/auth/ping", content: null, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(response.SetCookies());
    }

    [Theory]
    [MemberData(nameof(NoSuchFiles))]
    public async Task A_path_with_a_file_extension_that_is_no_file_gets_a_404_problem(string method, string path)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(ClientRoutesWithOtherMethods))]
    public async Task A_client_route_called_with_a_method_other_than_GET_gets_a_404_problem(string method, string path)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(UnknownApiRequests))]
    public async Task An_unknown_api_path_gets_a_404_problem(string method, string path)
    {
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // The page from the static assets (/index.html), and through the client-route fallback (/ and a client route).
    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/manage/anything")]
    public async Task The_page_is_served(string path)
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_page_s_script_is_served()
    {
        using var client = app.CreateClient();
        var bundle = await client.FetchHashedScriptPathAsync();

        using var response = await client.GetAsync(bundle, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Readiness runs the database check, which fails here, where there is no database.
    [Theory]
    [InlineData("/healthz", HttpStatusCode.OK)]
    [InlineData("/healthz/ready", HttpStatusCode.ServiceUnavailable)]
    public async Task The_health_endpoints_answer_with_the_app_s_health(string path, HttpStatusCode status)
    {
        using var client = app.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    /// <summary>
    /// Maps an endpoint with no policy of its own, which the fallback policy covers. It is routed ahead of the app's
    /// own routing, which then leaves the endpoint already chosen, so it runs where the app's own endpoints do, after
    /// the app's authorization.
    /// </summary>
    private sealed class FallbackPolicyEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapGet(FallbackPolicyPath, () => TypedResults.NoContent()));
        };
    }
}
