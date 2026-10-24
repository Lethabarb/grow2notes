using System.Net;
using System.Net.Http.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The four rate limits of design.md §9.9 with the design's numbers, which the test hosts lift for every other test
/// (<see cref="TestRateLimits"/>): sign-in and setup for each client address, the password step and the code step for
/// each account, the two exports for each user, and every other API endpoint for each user. Each test builds a host of
/// its own with the design's permits back (<see cref="RateLimitPermits.Design"/>) and a client address of its choice
/// (<see cref="ClientAddressHeader"/>), so its counts start at nothing, and it sends its requests one after another,
/// well inside the minute of a window. The numbers are written out here and not read from the permits, so a change to
/// the design's shows as a failing test. Addresses are the ones that RFC 5737 and RFC 3849 reserve for documents.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RateLimitsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string PasskeyOptionsPath = "/api/auth/passkey/options";

    private const string SetupStartPath = "/api/auth/setup/start";

    private const string SetupKeyPath = "/api/auth/setup/authenticator";

    private const string NoSuchDay = "2026-01-01";

    // A bad format, which the daily export refuses with a 400 before it reads a note or writes an event.
    private static readonly string DailyExportPath = $"/api/reports/daily/{NoSuchDay}/export?format=none";

    [Fact]
    public async Task Outside_tests_the_limits_are_design_md_9_9s()
    {
        await using var provider = new ServiceCollection().AddRateLimits().BuildServiceProvider();

        Assert.Equal(new RateLimitPermits(30, 10, 10, 300), provider.GetRequiredService<RateLimitPermits>());
    }

    [Fact]
    public async Task Sign_in_and_setup_endpoints_share_30_a_minute_for_each_client_address()
    {
        await using var host = Limited();
        using var client = host.CreateHttpsClient();
        var token = await client.FetchTokenAsync();

        for (var i = 0; i < 30; i++)
        {
            using var response = await SendAsync(client, "203.0.113.7", token, (i % 4) switch
            {
                0 => Post("/api/auth/login", new { email = $"{Guid.NewGuid():N}@example.org", password = "made-up" }),
                1 => Post(PasskeyOptionsPath),
                2 => Post(SetupStartPath, new { userId = Guid.NewGuid(), token = "made-up" }),
                _ => new(HttpMethod.Get, SetupKeyPath),
            });
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        using var thirtyFirst = await SendAsync(client, "203.0.113.7", token, Post(PasskeyOptionsPath));
        await AssertRefusedAsync(thirtyFirst);
        using var otherAddress = await SendAsync(client, "203.0.113.8", token, Post(PasskeyOptionsPath));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherAddress.StatusCode);
    }

    // The front end calls the app from a private address, as an IPv4-mapped IPv6 address, and gives the client's in
    // X-Forwarded-For, plain or mapped.
    [Fact]
    public async Task Behind_the_front_end_the_limit_keys_on_the_forwarded_client_address()
    {
        await using var host = Limited();
        using var client = host.CreateHttpsClient();
        var token = await client.FetchTokenAsync();

        for (var i = 0; i < 30; i++)
        {
            using var response = await SendAsync(
                client, "::ffff:10.0.0.1", token, Post(PasskeyOptionsPath), forwardedFor: ClientOf("203.0.113.7", i));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        using var thirtyFirst = await SendAsync(
            client, "::ffff:10.0.0.1", token, Post(PasskeyOptionsPath), forwardedFor: ClientOf("203.0.113.7", 30));
        await AssertRefusedAsync(thirtyFirst);
        using var otherClient = await SendAsync(
            client, "::ffff:10.0.0.1", token, Post(PasskeyOptionsPath), forwardedFor: "203.0.113.8");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherClient.StatusCode);
    }

    [Fact]
    public async Task The_password_step_allows_10_a_minute_for_each_account_by_its_normalised_email()
    {
        await using var host = Limited();
        var user = await host.AddPasswordUserAsync(sqlServer.Seeded.A);
        using var client = host.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(ClientAddressHeader.Name, "198.51.100.2");

        // The email in either letter case and with up to two trailing spaces, which SQL Server's comparison ignores.
        for (var i = 0; i < 10; i++)
        {
            var email = (i % 2 == 0 ? user.Email : user.Email.ToUpperInvariant()) + new string(' ', i % 3);
            using var wrong = await client.SendPasswordAsync(email, "Wrong-passw0rd");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var eleventh = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        await AssertRefusedAsync(eleventh);
        using var unknown = await client.SendPasswordAsync($"{Guid.NewGuid():N}@example.org", PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    // A code that is badly formed fails before Identity reads it, so the answer never depends on the clock, and it
    // counts towards no lockout.
    [Fact]
    public async Task The_code_step_allows_10_a_minute_for_each_pending_two_factor_user()
    {
        await using var host = Limited();
        var first = await host.AddPasswordUserAsync(sqlServer.Seeded.A);
        var second = await host.AddPasswordUserAsync(sqlServer.Seeded.A);
        using var client = host.CreateHttpsClient();
        client.DefaultRequestHeaders.Add(ClientAddressHeader.Name, "198.51.100.3");
        using var passwordStep = await client.SendPasswordAsync(first.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);

        for (var i = 0; i < 10; i++)
        {
            using var wrong = await client.SendCodeAsync("abcdef");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var eleventh = await client.SendCodeAsync("abcdef");
        await AssertRefusedAsync(eleventh);

        // The address is the same and the account is another. A client of its own holds the second user's step.
        using var other = host.CreateHttpsClient();
        other.DefaultRequestHeaders.Add(ClientAddressHeader.Name, "198.51.100.3");
        using var otherPasswordStep = await other.SendPasswordAsync(second.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, otherPasswordStep.StatusCode);
        using var otherCode = await other.SendCodeAsync("abcdef");
        Assert.Equal(HttpStatusCode.Unauthorized, otherCode.StatusCode);
    }

    [Fact]
    public async Task Daily_report_and_record_exports_share_10_a_minute_for_each_user()
    {
        await using var host = Limited();
        var participantExport = $"/api/reports/participants/{sqlServer.Seeded.A.ParticipantId}/export";
        using var manager = host.CreateClient().SignInAs(Caller.ManagerOfA, sqlServer.Seeded);

        for (var i = 0; i < 5; i++)
        {
            using var daily = await manager.GetAsync(DailyExportPath, TestContext.Current.CancellationToken);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, daily.StatusCode);
            using var record = await manager.GetAsync(participantExport, TestContext.Current.CancellationToken);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, record.StatusCode);
        }

        using var eleventh = await manager.GetAsync(DailyExportPath, TestContext.Current.CancellationToken);
        await AssertRefusedAsync(eleventh);

        // The check for notes is not an export, and another user's count is their own.
        using var hasNotes = await manager.GetAsync(
            $"/api/reports/daily/{NoSuchDay}/has-notes", TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, hasNotes.StatusCode);
        using var otherManager = host.CreateClient().SignInAs(Caller.ManagerOfB, sqlServer.Seeded);
        using var otherExport = await otherManager.GetAsync(DailyExportPath, TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherExport.StatusCode);
    }

    // The autosave's PUT has no token here, so the group's filter would answer it 400 if the limiter did not come first.
    [Fact]
    public async Task Everything_else_allows_300_a_minute_for_each_user_autosave_included()
    {
        await using var host = Limited();
        using var worker = host.CreateClient().SignInAs(Caller.WorkerOfA, sqlServer.Seeded);

        for (var i = 0; i < 300; i++)
        {
            using var me = await worker.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, me.StatusCode);
        }

        using var autosave = await worker.PutAsJsonAsync(
            $"/api/participants/{sqlServer.Seeded.A.ParticipantId}/notes/2026-10-05/draft",
            new { },
            TestContext.Current.CancellationToken);
        await AssertRefusedAsync(autosave);
        using var manager = host.CreateClient().SignInAs(Caller.ManagerOfA, sqlServer.Seeded);
        using var otherUser = await manager.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherUser.StatusCode);
    }

    // A host with the design's permits in place of the lifted ones, and the client address that a request names.
    private WebApplicationFactory<Program> Limited() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(RateLimitPermits.Design);
            services.AddClientAddressHeader();
        }));

    // The client behind the front end, in the plain form and the mapped form of an IPv4 address, by turns.
    private static string ClientOf(string address, int request) => request % 2 == 0 ? address : $"::ffff:{address}";

    private static HttpRequestMessage Post(string path, object? body = null) =>
        new(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body) };

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, string address, string token, HttpRequestMessage request, string? forwardedFor = null)
    {
        using (request)
        {
            request.Headers.Add(ClientAddressHeader.Name, address);
            request.Headers.Add(AntiforgeryTokens.Header, token);
            if (forwardedFor is not null)
            {
                request.Headers.Add("X-Forwarded-For", forwardedFor);
            }

            return await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
    }

    // What every refusal is: a 429 that says to wait the 60 seconds of the window, as problem details, never cached.
    private static async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        await response.ReadProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter?.Delta);
        Assert.True(response.Headers.NonValidated.TryGetValues("Cache-Control", out var cacheControl));
        Assert.Equal("no-store", cacheControl.ToString());
    }
}
