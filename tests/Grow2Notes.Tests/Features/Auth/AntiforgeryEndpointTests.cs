using System.Net;
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

    // Disposing the factory the app was derived from disposes the app too.
    public ValueTask DisposeAsync() => host.DisposeAsync();
}
