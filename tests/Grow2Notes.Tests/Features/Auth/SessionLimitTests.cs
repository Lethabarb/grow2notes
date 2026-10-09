using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// The session's limits (design.md §8.4, §8.5), with the cookie sign-in (<see cref="CookieSignIn"/>) on a fake clock
/// (<see cref="TestClock"/>) that moves before every request, so the session check runs on each: a session ends after
/// 30 minutes with no request, and 12 hours after sign-in however active it is. The cookie handler ends a session only
/// once its expiry has passed, and <see cref="SessionRules"/> only once more than 12 hours have, so the <c>401</c>s are
/// tested a second past 30 minutes and a minute past 12 hours. Each request is to <c>GET /api/auth/me</c>, which names
/// the organisation in the principal that the session check rebuilds from the user's row, so each answer also shows
/// that <c>org_id</c> survives the rebuild (design.md §14 M0 <i>Done when</i>).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SessionLimitTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string MePath = "/api/auth/me";

    private readonly FakeTimeProvider clock = new(TestClock.Start);

    // Signing in changes no row, so the tests sign in as A's seeded worker (tests/README.md).
    private readonly Guid userId;
    private readonly Guid organisationId;

    private readonly WebApplicationFactory<Program> app;

    public SessionLimitTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        organisationId = sqlServer.Seeded.A.OrganisationId;
        userId = sqlServer.Seeded.A.WorkerId;
        app = factory.WithWebHostBuilder(builder => builder
            .MapCookieSignIn()
            .UseClock(clock)
            .ConfigureServices(services => services.AddSingleton<IStartupFilter>(new SignInOutsideTheSignInManager())));
    }

    // 58 minutes after sign-in, the second request comes after the expiry of the cookie that the sign-in set, so only
    // the renewed cookie lets it in.
    [Fact]
    public async Task A_request_after_29_minutes_idle_renews_the_cookie_so_one_29_minutes_later_is_let_in_too()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var organisationName = await OrganisationNameAsync();
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(userId);

        clock.Advance(TimeSpan.FromMinutes(29));
        using var first = await client.GetAsync(MePath, cancellationToken);

        await AssertNamesTheOrganisationAsync(first, organisationName);
        var renewed = Assert.Single(first.SetCookies());
        Assert.Equal(CookieSignIn.SessionCookie, renewed.Name.Value);
        Assert.NotEqual(string.Empty, renewed.Value.Value);

        clock.Advance(TimeSpan.FromMinutes(29));
        using var second = await client.GetAsync(MePath, cancellationToken);

        await AssertNamesTheOrganisationAsync(second, organisationName);
    }

    [Fact]
    public async Task A_request_after_30_minutes_and_1_second_idle_gets_401()
    {
        using var client = app.CreateHttpsClient();
        await client.SignInWithCookieAsync(userId);

        clock.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1));
        using var response = await client.GetAsync(MePath, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // Never idle for more than 20 minutes, the session ends at the 12-hour limit alone, which counts from the auth_time
    // that each of the 36 rebuilds has carried forward: had one started it again, the session would still be let in.
    [Fact]
    public async Task A_request_every_20_minutes_is_let_in_at_12_hours_and_refused_at_12_hours_and_1_minute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var organisationName = await OrganisationNameAsync();
        using var client = app.CreateHttpsClient();
        var signedInAt = clock.GetUtcNow();
        await client.SignInWithCookieAsync(userId);

        var twentyMinutes = TimeSpan.FromMinutes(20);
        for (var sinceSignIn = twentyMinutes; sinceSignIn <= TimeSpan.FromHours(12); sinceSignIn += twentyMinutes)
        {
            clock.SetUtcNow(signedInAt + sinceSignIn);
            using var response = await client.GetAsync(MePath, cancellationToken);

            await AssertNamesTheOrganisationAsync(response, organisationName);
        }

        clock.SetUtcNow(signedInAt + TimeSpan.FromHours(12) + TimeSpan.FromMinutes(1));
        using var refused = await client.GetAsync(MePath, cancellationToken);

        await refused.ReadProblemAsync(HttpStatusCode.Unauthorized);
        refused.AssertClearsTheSessionCookie();
    }

    // The cookie is the session cookie, with the user's own claims and security stamp, so the stamp check lets it
    // through; it ends only because, with no auth_time, the 12-hour limit has nothing to count from.
    [Fact]
    public async Task A_sign_in_outside_the_sign_in_manager_has_no_auth_time_and_ends_on_its_next_request()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient();
        using var signIn = await client.PostAsync(
            $"{SignInOutsideTheSignInManager.Path}/{userId}", content: null, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        Assert.Equal(CookieSignIn.SessionCookie, Assert.Single(signIn.SetCookies()).Name.Value);

        clock.Advance(TimeSpan.FromSeconds(1));
        using var response = await client.GetAsync(MePath, cancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        response.AssertClearsTheSessionCookie();
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertNamesTheOrganisationAsync(HttpResponseMessage response, string organisationName)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(organisationName, me.GetProperty("organisationName").GetString());
    }

    // As its row holds it. An organisation is the tenant, not tenant-owned, so reading one needs no tenant.
    private async Task<string> OrganisationNameAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .Where(organisation => organisation.Id == organisationId)
            .Select(organisation => organisation.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Maps <c>POST</c> at <see cref="Path"/> and a user's ID, which signs that user in with the session cookie by
    /// calling <c>HttpContext.SignInAsync</c> with Identity's scheme and the principal that the app's claims factory
    /// builds, rather than through <see cref="Grow2NotesSignInManager"/>, so the session has no
    /// <see cref="Grow2NotesSignInManager.AuthTimeClaimType"/>. Like <see cref="CookieSignIn"/>'s, anyone may call it,
    /// it is outside the <c>/api</c> group, and it is routed ahead of the app's own routing, so it runs where the app's
    /// own endpoints do.
    /// </summary>
    private sealed class SignInOutsideTheSignInManager : IStartupFilter
    {
        public const string Path = "/test-only/sign-in-outside-the-sign-in-manager";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.UseRouting();
            next(app);
            app.UseEndpoints(endpoints => endpoints.MapPost($"{Path}/{{userId:guid}}", SignInAsync).AllowAnonymous());
        };

        private static async Task<Results<NoContent, NotFound>> SignInAsync(
            Guid userId,
            HttpContext context,
            UserManager<ApplicationUser> users,
            IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory)
        {
            if (await users.FindByIdAsync(userId.ToString()) is not { } user)
            {
                return TypedResults.NotFound();
            }

            await context.SignInAsync(IdentityConstants.ApplicationScheme, await claimsFactory.CreateAsync(user));
            return TypedResults.NoContent();
        }
    }
}
