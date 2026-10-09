using System.Net;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests;

/// <summary>
/// HSTS (design.md §9.3, §9.7) where it depends on the request: the header goes only on an HTTPS request and never to
/// <c>localhost</c>, so a request over plain HTTP or to <c>localhost</c> gets none, and one that App Service's front
/// end forwarded from HTTPS, with the host name that the browser called, gets it. <c>SecurityHeadersTests</c> checks
/// it, with §9.7's other headers, on each kind of response, over HTTPS at a host name other than <c>localhost</c>. The
/// app is hosted as it is deployed, without the test-only sign-in, and without a database, so the tests run without
/// Docker.
/// </summary>
public sealed class HstsTests : IAsyncDisposable
{
    private const string Hsts = "max-age=31536000; includeSubDomains";

    // RFC 2606 reserves .example, so the name is no real site's.
    private const string HostName = "grow2notes.example";

    private readonly AppWithoutDatabase app = new("Production");

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

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // Like CreateHttpsClient's, it follows no redirect, so a test sees the status that the app answered with.
    private HttpClient CreateClient(string baseAddress) =>
        app.CreateClient(new() { BaseAddress = new(baseAddress), AllowAutoRedirect = false });

    // Every value of the header, as the app sent them, so a second Strict-Transport-Security would show.
    private static string? StrictTransportSecurity(HttpResponseMessage response) =>
        response.Headers.NonValidated.TryGetValues(HeaderNames.StrictTransportSecurity, out var values)
            ? values.ToString()
            : null;
}
