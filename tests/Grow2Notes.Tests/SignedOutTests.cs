using System.Net;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests;

/// <summary>
/// A signed-out caller at each endpoint the app maps, with the app hosted as it is deployed: without the test-only
/// sign-in, so with no authentication scheme until S00.04.01 adds the session cookie. A request that the fallback
/// policy refused would be challenged with no scheme to challenge with, which throws, and the caller would get a
/// <c>500</c>; so these show that each endpoint is <c>AllowAnonymous</c>, and that a request that no file, page or API
/// endpoint answers still matches one. The app has no database here, so they run without Docker.
/// </summary>
public sealed partial class SignedOutTests : IAsyncDisposable
{
    public static MatrixTheoryData<string, string> NoSuchFiles { get; } =
        new(["GET", "POST", "PUT", "DELETE"], ["/favicon.ico", "/assets/no-such-file.js"]);

    // The client-route fallback answers GET and HEAD only.
    public static MatrixTheoryData<string, string> ClientRoutesWithOtherMethods { get; } =
        new(["POST", "PUT", "DELETE", "OPTIONS"], ["/", "/manage/anything"]);

    public static MatrixTheoryData<string, string> UnknownApiRequests { get; } =
        new(["GET", "POST", "PUT", "DELETE"], ["/api", "/api/no-such-endpoint"]);

    private readonly AppWithoutDatabase app = new("Production");

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
}
