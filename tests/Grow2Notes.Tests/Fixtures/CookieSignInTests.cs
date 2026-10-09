using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The cookie sign-in (<see cref="CookieSignIn"/>) on a fake clock (<see cref="TestClock"/>), which the session tests
/// build on: the sign-in sets the session cookie with design.md §8.4's attributes, over plain HTTP too, and a later
/// request with that cookie alone is signed in as the user, with the claims factory's claims and the sign-in time, and
/// gets the cookie renewed with the same attributes. <see cref="Grow2NotesFactory"/>'s default scheme sends such a
/// request to the cookie, and one that has the test-only sign-in's role header too to the test-only sign-in.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class CookieSignInTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    // Behind the fallback policy, so only a signed-in request reaches it.
    private const string FallbackPolicyPath = "/api/test-only/fallback-policy";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public CookieSignInTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder
            .MapCookieSignIn()
            .UseClock(clock)
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new ClaimsEndpoint())));
    }

    [Fact]
    public async Task The_sign_in_s_Set_Cookie_holds_the_session_cookie_with_its_attributes()
    {
        using var client = app.CreateHttpsClient();

        var cookie = await client.SignInWithCookieAsync(seeded.A.WorkerId);

        AssertIsTheSessionCookie(cookie);
    }

    // Secure whatever the request's scheme (design.md §8.4). App Service's front end calls the app over HTTP, so were
    // the cookie Secure only on an HTTPS request, a sign-in whose forwarded scheme the app did not trust would set it
    // without Secure, which a browser refuses for a __Host- cookie.
    [Fact]
    public async Task A_sign_in_over_plain_HTTP_sets_the_session_cookie_Secure_all_the_same()
    {
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });

        var cookie = await client.SignInWithCookieAsync(seeded.A.WorkerId);

        AssertIsTheSessionCookie(cookie);
    }

    // The clock moves, so the stamp check runs and rebuilds the principal, which keeps the sign-in time.
    [Fact]
    public async Task A_later_request_with_the_cookie_alone_holds_the_factory_s_claims_and_auth_time_and_renews_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        List<string> expected =
        [
            .. await app.FromTheClaimsFactoryAsync(seeded.A.WorkerId),
            $"{Grow2NotesSignInManager.AuthTimeClaimType}: {TestClock.Start.ToUnixTimeSeconds()}",
        ];
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(seeded.A.WorkerId);
        clock.Advance(TimeSpan.FromSeconds(1));

        using var response = await client.GetAsync(FallbackPolicyPath, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<string[]>(cancellationToken));
        AssertIsTheSessionCookie(Assert.Single(response.SetCookies()));
    }

    // A role header alone goes to the test-only sign-in, which fails to sign the request in, rather than to the cookie,
    // which would sign it in and ignore the header.
    [Fact]
    public async Task With_the_cookie_a_request_with_the_test_only_role_header_alone_is_refused()
    {
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(seeded.A.WorkerId);
        clock.Advance(TimeSpan.FromSeconds(1));
        client.DefaultRequestHeaders.Add(TestSignIn.RoleHeader, "Manager");

        using var response = await client.GetAsync(FallbackPolicyPath, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // A browser takes a __Host- cookie only when it is Secure, has Path=/ and names no Domain. HttpOnly keeps it from
    // script (design.md §8.3), SameSite=Strict from other sites' requests (§9.8 item 1), and with neither Expires nor
    // Max-Age it is a session cookie, which the browser drops when it closes (§8.5).
    private static void AssertIsTheSessionCookie(SetCookieHeaderValue cookie)
    {
        Assert.Equal(CookieSignIn.SessionCookie, cookie.Name.Value);
        Assert.True(cookie.Secure);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/", cookie.Path.Value);
        Assert.False(cookie.Domain.HasValue);
        Assert.Null(cookie.Expires);
        Assert.Null(cookie.MaxAge);
    }

    /// <summary>
    /// Maps an endpoint with no policy of its own, which the fallback policy covers, and which lists the claims of the
    /// request's user. It is routed ahead of the app's own routing, which then leaves the endpoint already chosen, so
    /// it runs where the app's own endpoints do, after the app's authorization.
    /// </summary>
    private sealed class ClaimsEndpoint : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapGet(
                FallbackPolicyPath, (ClaimsPrincipal user) => user.Claims.Select(DescribedClaims.Describe)));
        };
    }
}
