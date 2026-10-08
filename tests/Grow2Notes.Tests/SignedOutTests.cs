using System.Net;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests;

/// <summary>
/// A signed-out caller at each endpoint the app maps, with the app hosted as it is deployed: without the test-only
/// sign-in, so with the session cookie as the default scheme. A request that the fallback policy refuses is challenged
/// by the cookie, and gets a <c>401</c> problem, as a test-only endpoint behind that policy and the app's own
/// <c>/api/auth/me</c> show; so the others show that the page, its files and the health endpoints are
/// <c>AllowAnonymous</c>, and that a request that no file, page or API endpoint answers still matches one, and gets a
/// <c>404</c>. The app has no database here, so they run without Docker: a request with no cookie never reaches one.
/// </summary>
public sealed partial class SignedOutTests : IAsyncDisposable
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

    // The page itself, from the static assets, and a client route, from the client-route fallback.
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
        var page = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        // The bundle's file name changes with its content, so it is read from the page rather than written here.
        var bundle = HashedScript().Match(page);
        Assert.True(bundle.Success, "index.html names no script under /assets.");

        using var response = await client.GetAsync(bundle.Groups["path"].Value, TestContext.Current.CancellationToken);

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

    [GeneratedRegex("""<script\b[^>]*\bsrc="(?<path>/assets/[^"]+)"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex HashedScript();

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
