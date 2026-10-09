using System.Net;
using Grow2Notes.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// design.md §7.2's caching rules, signed out and, for the page and its files, signed in with the session cookie
/// (<see cref="CookieSignIn"/>) on a fake clock (<see cref="TestClock"/>) that moves a second before the request, so
/// the session check renews the cookie on it, as it does on a signed-in browser's every request. The cookie handler
/// marks a response that renews the cookie <c>no-cache,no-store</c>, with <c>Pragma: no-cache</c> and an
/// <c>Expires</c> in 1970, which the rules replace.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class CacheHeadersTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    public static MatrixTheoryData<string, string> UnknownApiRequests { get; } =
        new(["GET", "POST", "PUT", "DELETE"], ["/api", "/api/no-such-endpoint"]);

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;

    // Signing in changes no row, so the tests sign in as A's seeded worker (tests/README.md).
    private readonly Guid userId;

    private readonly WebApplicationFactory<Program> withCookieSignIn;

    public CacheHeadersTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        userId = sqlServer.Seeded.A.WorkerId;
        withCookieSignIn = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
    }

    [Fact]
    public async Task Hashed_assets_are_cached_for_a_year_as_immutable()
    {
        using var client = factory.CreateClient();
        var bundle = await client.FetchHashedScriptPathAsync();

        using var response = await client.GetAsync(bundle, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.SetCookies());
        AssertCachedForAYear(response, "public");
    }

    // A shared cache could otherwise hand the response, and the session cookie it sets, to someone else.
    [Fact]
    public async Task A_hashed_asset_that_renews_the_session_cookie_is_cached_for_a_year_by_the_browser_alone()
    {
        using var client = await SignedInClientAsync();
        var bundle = await client.FetchHashedScriptPathAsync();
        clock.Advance(TimeSpan.FromSeconds(1));

        using var response = await client.GetAsync(bundle, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRenewsTheSessionCookie(response);
        AssertCachedForAYear(response, "private");
    }

    // A browser that revalidates the bundle, as a reload can, updates its stored headers from the 304's, so a 304 with
    // the renewal's no-cache headers would have it revalidate the bundle on every later load too.
    [Fact]
    public async Task A_hashed_asset_s_304_that_renews_the_session_cookie_is_cached_for_a_year_by_the_browser_alone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = await SignedInClientAsync();
        var bundle = await client.FetchHashedScriptPathAsync();
        using var first = await client.GetAsync(bundle, cancellationToken);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var request = new HttpRequestMessage(HttpMethod.Get, bundle) { Headers = { IfNoneMatch = { etag } } };

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        AssertRenewsTheSessionCookie(response);
        AssertCachedForAYear(response, "private");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/manage/anything")]
    public async Task Index_html_is_revalidated_on_every_load(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-cache", CacheControl(response));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/manage/anything")]
    public async Task Index_html_that_renews_the_session_cookie_is_revalidated_on_every_load_by_the_browser_alone(
        string path)
    {
        using var client = await SignedInClientAsync();
        clock.Advance(TimeSpan.FromSeconds(1));

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertRenewsTheSessionCookie(response);
        Assert.Equal("private, no-cache", CacheControl(response));
    }

    [Theory]
    [MemberData(nameof(UnknownApiRequests))]
    public async Task Api_responses_are_never_stored(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("no-store", CacheControl(response));
    }

    public ValueTask DisposeAsync() => withCookieSignIn.DisposeAsync();

    // A Pragma: no-cache would have Chromium and WebKit revalidate the file on every load, whatever Cache-Control says.
    private static void AssertCachedForAYear(HttpResponseMessage response, string cacheability)
    {
        Assert.Equal($"{cacheability}, max-age=31536000, immutable", CacheControl(response));
        Assert.False(HasHeader(response, HeaderNames.Pragma), "The response has a Pragma header.");
        Assert.False(HasHeader(response, HeaderNames.Expires), "The response has an Expires header.");
    }

    // HttpClient keeps Expires with the content's headers, and Pragma with the response's.
    private static bool HasHeader(HttpResponseMessage response, string name) =>
        response.Headers.NonValidated.Contains(name) || response.Content.Headers.NonValidated.Contains(name);

    private static void AssertRenewsTheSessionCookie(HttpResponseMessage response)
    {
        var cookie = Assert.Single(response.SetCookies());
        Assert.Equal(CookieSignIn.SessionCookie, cookie.Name.Value);
        Assert.NotEqual(string.Empty, cookie.Value.Value);
    }

    // The header exactly as the app sent it. The typed CacheControl property would reformat it from parsed directives.
    private static string? CacheControl(HttpResponseMessage response) =>
        response.Headers.NonValidated.TryGetValues("Cache-Control", out var values) ? values.ToString() : null;

    private async Task<HttpClient> SignedInClientAsync()
    {
        var client = withCookieSignIn.CreateHttpsClient();
        await client.SignInWithCookieAsync(userId);
        return client;
    }
}
