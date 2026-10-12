using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/ping</c> (design.md §6.2, §8.5), called as Stay signed in calls it: over HTTPS, signed in with the
/// cookie sign-in (<see cref="CookieSignIn"/>) as A's seeded worker, since the ping changes no row, with the
/// antiforgery token fetched after signing in. The app is hosted on a fake clock (<see cref="TestClock"/>) that moves
/// before each later request, so the session check runs on each: the ping renews the session as any request does
/// (§8.4, A50), and meets the same limits, 30 minutes with no request and 12 hours from sign-in
/// (<see cref="SessionLimitTests"/>). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what
/// a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PingEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/auth/ping";

    private const string MePath = "/api/auth/me";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Guid userId;
    private readonly WebApplicationFactory<Program> app;

    public PingEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        userId = sqlServer.Seeded.A.WorkerId;
        app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
    }

    // 58 minutes after sign-in, the request comes after the expiry of the cookie that the sign-in set, so only the
    // cookie that the ping renewed lets it in.
    [Fact]
    public async Task A_ping_29_minutes_after_sign_in_answers_204_and_renews_the_cookie_for_29_minutes_more()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient();
        var token = await SignInAsync(client);

        clock.Advance(TimeSpan.FromMinutes(29));
        using var response = await client.PostWithTokenAsync(Path, token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        var renewed = Assert.Single(response.SetCookies());
        renewed.AssertIsTheSessionCookie();
        Assert.NotEqual(string.Empty, renewed.Value.Value);

        clock.Advance(TimeSpan.FromMinutes(29));
        using var me = await client.GetAsync(MePath, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_ping_30_minutes_and_1_second_after_the_last_request_gets_401()
    {
        using var client = app.CreateHttpsClient();
        var token = await SignInAsync(client);

        clock.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1));
        using var response = await client.PostWithTokenAsync(Path, token);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // Never idle for more than 20 minutes, the session still ends at the 12-hour limit, which Stay signed in cannot
    // extend (app-shell.md Tensions 2).
    [Fact]
    public async Task With_a_ping_every_20_minutes_one_at_12_hours_and_1_minute_gets_401_and_deletes_the_cookie()
    {
        using var client = app.CreateHttpsClient();
        var signedInAt = clock.GetUtcNow();
        var token = await SignInAsync(client);

        var twentyMinutes = TimeSpan.FromMinutes(20);
        for (var sinceSignIn = twentyMinutes; sinceSignIn <= TimeSpan.FromHours(12); sinceSignIn += twentyMinutes)
        {
            clock.SetUtcNow(signedInAt + sinceSignIn);
            using var response = await client.PostWithTokenAsync(Path, token);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        clock.SetUtcNow(signedInAt + TimeSpan.FromHours(12) + TimeSpan.FromMinutes(1));
        using var refused = await client.PostWithTokenAsync(Path, token);

        await refused.ReadProblemAsync(HttpStatusCode.Unauthorized);
        refused.AssertClearsTheSessionCookie();
    }

    // design.md §5.4 lists no event for it, whether the ping is let in or refused.
    [Fact]
    public async Task Pings_write_no_audit_event()
    {
        using var client = app.CreateHttpsClient();
        var token = await SignInAsync(client);
        var lastEventId = await LastEventIdAsync();

        clock.Advance(TimeSpan.FromSeconds(1));
        using (var letIn = await client.PostWithTokenAsync(Path, token))
        {
            Assert.Equal(HttpStatusCode.NoContent, letIn.StatusCode);
        }

        clock.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(1));
        using (var refused = await client.PostWithTokenAsync(Path, token))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // Returns the antiforgery token, which is issued to the user a request is signed in as, so is fetched after
    // signing in (tests/README.md). On a clock that has not moved, its request renews nothing, so the session's last
    // request is the sign-in.
    private async Task<string> SignInAsync(HttpClient client)
    {
        await client.SignInWithCookieAsync(userId);
        return await client.FetchTokenAsync();
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after its requests.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
