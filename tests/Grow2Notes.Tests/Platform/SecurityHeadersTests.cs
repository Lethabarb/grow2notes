using System.Net;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// design.md §9.7's security headers on each kind of response that the app has so far, other than those the exception
/// handler writes, which go without <c>Strict-Transport-Security</c> (see <c>Program.cs</c>) and which
/// <see cref="DatabaseFailureHandlerTests"/> checks for the other five: each carries all six, once each, with the value
/// in §9.7's text block, read from design.md itself (<see cref="DesignedSecurityHeaders"/>), so the code and the design
/// cannot drift apart. A story that adds a kind of response adds it here (tests/README.md).
/// <c>Strict-Transport-Security</c> is <c>UseHsts</c>'s, which sends it only over HTTPS and never to <c>localhost</c>,
/// so the tests call another host name over HTTPS, as <see cref="HstsTests"/> does.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SecurityHeadersTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // RFC 2606 reserves .example, so the name is no real site's.
    private const string BaseAddress = "https://grow2notes.example";

    // The page through the client-route fallback (/, as every client route takes it) and from the static assets
    // (/index.html); /api/auth/me's JSON for A's worker, the session cookie's 401 signed out, and the 403 for a role
    // that no policy names, Release 2's Support; the antiforgery token's 204; the unknown /api path's 404; the 404 of
    // the catch-all for a path with a file extension that is no file; and liveness.
    [Theory]
    [InlineData("/", Caller.SignedOut, null, HttpStatusCode.OK, "text/html")]
    [InlineData("/index.html", Caller.SignedOut, null, HttpStatusCode.OK, "text/html")]
    [InlineData("/api/auth/me", Caller.WorkerOfA, null, HttpStatusCode.OK, "application/json")]
    [InlineData("/api/auth/me", Caller.SignedOut, null, HttpStatusCode.Unauthorized, "application/problem+json")]
    [InlineData("/api/auth/me", Caller.WorkerOfA, "Support", HttpStatusCode.Forbidden, "application/problem+json")]
    [InlineData("/api/auth/antiforgery", Caller.SignedOut, null, HttpStatusCode.NoContent, null)]
    [InlineData("/api/no-such-endpoint", Caller.SignedOut, null, HttpStatusCode.NotFound, "application/problem+json")]
    [InlineData("/favicon.ico", Caller.SignedOut, null, HttpStatusCode.NotFound, "application/problem+json")]
    [InlineData("/healthz", Caller.SignedOut, null, HttpStatusCode.OK, "text/plain")]
    public async Task Each_kind_of_response_carries_the_headers_of_design_md_9_7_once_each_with_their_values(
        string path, Caller caller, string? role, HttpStatusCode status, string? mediaType)
    {
        using var client = CreateClient(caller, role);

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        AssertCarriesTheHeadersOfDesignMd(response);
    }

    [Fact]
    public async Task A_hashed_asset_carries_the_headers_of_design_md_9_7_once_each_with_their_values()
    {
        using var client = CreateClient(Caller.SignedOut, role: null);
        var bundle = await client.FetchHashedScriptPathAsync();

        using var response = await client.GetAsync(bundle, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCarriesTheHeadersOfDesignMd(response);
    }

    // Every value of each header, exactly as the app sent them, so a second one would show.
    private static void AssertCarriesTheHeadersOfDesignMd(HttpResponseMessage response) =>
        Assert.All(DesignedSecurityHeaders.Read(), header =>
        {
            string[] sent = response.Headers.NonValidated.TryGetValues(header.Key, out var values) ? [.. values] : [];
            Assert.Equal(header.Value, Assert.Single(sent));
        });

    // Like HstsTests' client, it follows no redirect, so a test sees the status that the app answered with.
    private HttpClient CreateClient(Caller caller, string? role)
    {
        var client = factory.CreateClient(new() { BaseAddress = new(BaseAddress), AllowAutoRedirect = false })
            .SignInAs(caller, sqlServer.Seeded);
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add(TestSignIn.RoleHeader, role);
        }

        return client;
    }
}
