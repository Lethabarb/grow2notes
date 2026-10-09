using System.Net;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests;

/// <summary>
/// HSTS (design.md §9.3, §9.7) on each kind of response that the app has so far, other than one that the exception
/// handler writes, which goes without it (see <c>Program.cs</c>). The app is hosted as it is deployed, without the
/// test-only sign-in, and without a database, with its Data Protection keys in memory for the antiforgery endpoint, so
/// the tests run without Docker. The header goes only on an HTTPS request and never to <c>localhost</c>, so the tests
/// call another host name, as App Service's front end passes on the one a browser called.
/// </summary>
public sealed class HstsTests : IAsyncDisposable
{
    private const string Hsts = "max-age=31536000; includeSubDomains";

    // RFC 2606 reserves .example, so the name is no real site's.
    private const string HostName = "grow2notes.example";

    private readonly AppWithoutDatabase host = new("Production");
    private readonly WebApplicationFactory<Program> app;

    public HstsTests() => app = host.WithWebHostBuilder(builder => builder.KeepKeysInMemory());

    // The page from the static assets (/index.html) and through the client-route fallback (/, as every client route
    // takes), an API endpoint's answer, the session cookie's 401 problem, the unknown /api path's 404 problem, and
    // liveness.
    [Theory]
    [InlineData("/index.html", HttpStatusCode.OK)]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/api/auth/antiforgery", HttpStatusCode.NoContent)]
    [InlineData("/api/auth/me", HttpStatusCode.Unauthorized)]
    [InlineData("/api/no-such-endpoint", HttpStatusCode.NotFound)]
    [InlineData("/healthz", HttpStatusCode.OK)]
    public async Task A_response_over_HTTPS_carries_HSTS_for_a_year_with_subdomains(string path, HttpStatusCode status)
    {
        using var client = CreateClient($"https://{HostName}");

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(Hsts, StrictTransportSecurity(response));
    }

    [Fact]
    public async Task A_hashed_asset_over_HTTPS_carries_HSTS_for_a_year_with_subdomains()
    {
        using var client = CreateClient($"https://{HostName}");
        var bundle = await client.FetchHashedScriptPathAsync();

        using var response = await client.GetAsync(bundle, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Hsts, StrictTransportSecurity(response));
    }

    // A browser ignores the header on plain HTTP (RFC 6797 §8.1), and https://localhost is where development runs,
    // which a year's pin would hold to HTTPS for every app served there.
    [Theory]
    [InlineData($"http://{HostName}")]
    [InlineData("https://localhost")]
    public async Task A_response_over_plain_HTTP_or_to_localhost_carries_none(string baseAddress)
    {
        using var client = CreateClient(baseAddress);

        using var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(StrictTransportSecurity(response));
    }

    // As ForwardedHeadersTests sends a request from App Service's front end: over the test server's plain HTTP, so only
    // X-Forwarded-Proto makes it HTTPS, and with the host name that the browser called.
    [Fact]
    public async Task A_request_the_front_end_forwarded_from_HTTPS_gets_HSTS()
    {
        var context = await app.Server.SendAsync(request =>
        {
            request.Request.Method = HttpMethods.Get;
            request.Request.Host = new(HostName);
            request.Request.Path = "/";
            request.Request.Headers["X-Forwarded-For"] = "203.0.113.7:51234";
            request.Request.Headers["X-Forwarded-Proto"] = "https";
            request.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:10.0.0.1");
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(Hsts, Assert.Single(context.Response.Headers.StrictTransportSecurity));
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();

    // Like CreateHttpsClient's, it follows no redirect, so a test sees the status that the app answered with.
    private HttpClient CreateClient(string baseAddress) =>
        app.CreateClient(new() { BaseAddress = new(baseAddress), AllowAutoRedirect = false });

    // Every value of the header, as the app sent them, so a second Strict-Transport-Security would show.
    private static string? StrictTransportSecurity(HttpResponseMessage response) =>
        response.Headers.NonValidated.TryGetValues(HeaderNames.StrictTransportSecurity, out var values)
            ? values.ToString()
            : null;
}
