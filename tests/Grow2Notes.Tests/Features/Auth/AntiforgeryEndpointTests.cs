using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>GET /api/auth/antiforgery</c> (design.md §6.2, §9.8 item 2), called signed out over HTTPS. The app is hosted as
/// it is deployed, without the test-only sign-in, so the endpoint's <c>204</c> shows that it is <c>AllowAnonymous</c>,
/// and without a database, with its Data Protection keys in memory, so the tests run without Docker.
/// </summary>
public sealed class AntiforgeryEndpointTests : IAsyncDisposable
{
    private const string Path = "/api/auth/antiforgery";

    private readonly AppWithoutDatabase host = new("Production");
    private readonly WebApplicationFactory<Program> app;

    public AntiforgeryEndpointTests() => app = host.WithWebHostBuilder(builder => builder.KeepKeysInMemory());

    // With no Sec-Fetch headers, as a client that is not a browser calls it, such as curl in the deploy's smoke test.
    [Fact]
    public async Task Answers_204_with_a_token_in_the_header_and_no_X_Frame_Options_and_is_never_stored()
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(Assert.Single(response.Headers.GetValues("X-XSRF-TOKEN"))));
        Assert.False(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // As design.md §8.4's session cookie. A browser takes a __Host- cookie only when it is Secure, has Path=/ and
    // names no Domain.
    [Fact]
    public async Task Sets_the_cookie_HttpOnly_Secure_and_SameSite_Strict_for_the_whole_site()
    {
        using var client = app.CreateHttpsClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        var cookie = SetCookieHeaderValue.Parse(Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie)));
        Assert.Equal("__Host-grow2notes-xsrf", cookie.Name.ToString());
        Assert.False(string.IsNullOrEmpty(cookie.Value.ToString()));
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/", cookie.Path.ToString());
        Assert.False(cookie.Domain.HasValue);
    }

    // As a browser sends the SPA's fetch.
    [Fact]
    public async Task Answers_a_fetch_from_the_same_origin_204_with_a_token_and_the_cookie()
    {
        using var client = app.CreateHttpsClient();
        using var request = FromABrowser(site: "same-origin", mode: "cors");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(Assert.Single(response.Headers.GetValues("X-XSRF-TOKEN"))));
        var cookie = SetCookieHeaderValue.Parse(Assert.Single(response.Headers.GetValues(HeaderNames.SetCookie)));
        Assert.Equal("__Host-grow2notes-xsrf", cookie.Name.ToString());
    }

    // Each signed out, so with no cookie, as a browser sends a cross-site navigation without the SameSite=Strict one; a
    // cookie set in answer would replace the one that the SPA's token pairs with.
    [Theory]
    [InlineData("cross-site", "navigate")] // Another site's link, form or redirect.
    [InlineData("same-origin", "navigate")] // A link on one of the app's own pages.
    [InlineData("none", "navigate")] // The address bar or a bookmark.
    [InlineData("cross-site", "cors")] // Another site's fetch.
    [InlineData("cross-site", "no-cors")] // Another site's image or script.
    [InlineData("same-site", "cors")] // A fetch from another origin of the same site.
    public async Task A_navigation_or_a_request_from_another_origin_gets_a_403_problem_with_no_token_or_cookie(
        string site, string mode)
    {
        using var client = app.CreateHttpsClient();
        using var request = FromABrowser(site, mode);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Forbidden));
        Assert.False(problem.RootElement.TryGetProperty("code", out _));
        Assert.False(response.Headers.Contains("X-XSRF-TOKEN"));
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();

    // The Sec-Fetch headers that the endpoint reads; a page cannot set them, only the browser.
    private static HttpRequestMessage FromABrowser(string site, string mode)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.Add("Sec-Fetch-Site", site);
        request.Headers.Add("Sec-Fetch-Mode", mode);
        return request;
    }
}
