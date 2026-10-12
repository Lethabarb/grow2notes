using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/passkey</c> (design.md §6.2, §8.2) signing in an Active user, and failing to, called as the SPA
/// calls it (<see cref="PasskeySignIn"/>): after the passkey options, with an assertion over their challenge that the
/// test authenticator signs (<see cref="TestAuthenticator"/>), for a passkey stored for a user that the test adds to
/// seeded organisation A or B. The app runs on a fake clock (<see cref="TestClock"/>), which moves a second before a
/// later request of the session, and its log entries are recorded. The endpoint matrix covers who may call it, and
/// <see cref="PasskeySignInRequestTests"/> a request without a credential.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PasskeySignInEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string MePath = "/api/auth/me";

    private const string UnknownCredentialMessage = "Passkey sign-in with an unknown credential";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly RecordingLoggerProvider logs = new();
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public PasskeySignInEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(
            builder => builder.UseClock(clock).ConfigureLogging(logging => logging.AddProvider(logs)));
    }

    // The answer is design.md §6.2's Me, as GET /api/auth/me gives it. The clock starts at 13:30 UTC on 9 October,
    // which is half past midnight on 10 October in Melbourne.
    [Fact]
    public async Task An_Active_user_s_passkey_signs_them_in_and_the_answer_says_who_they_are()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        foreach (var (organisation, role, displayName) in new[]
        {
            (seeded.A, UserRole.Worker, "Sam Taylor"),
            (seeded.A, UserRole.Manager, "Alex Morgan"),
            (seeded.B, UserRole.Manager, "Jamie Park"),
        })
        {
            var userId = await AddUserAsync(organisation, role, displayName);
            using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
            using var client = app.CreateHttpsClient();

            using var response = await client.SignInWithPasskeyAsync(authenticator);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            (string Name, string? Value)[] expected =
            [
                ("userId", userId.ToString()),
                ("displayName", displayName),
                ("role", role.ToString()),
                ("organisationName", await OrganisationNameAsync(organisation.OrganisationId)),
                ("today", "2026-10-10"),
            ];
            var me = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.Equal(
                expected, me.EnumerateObject().Select(property => (property.Name, property.Value.GetString())));
        }
    }

    [Fact]
    public async Task The_sign_in_sets_the_session_cookie_with_its_attributes()
    {
        var userId = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var client = app.CreateHttpsClient();

        using var response = await client.SignInWithPasskeyAsync(authenticator);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SessionCookieOf(response).AssertIsTheSessionCookie();
    }

    // The session check refuses a session with no sign-in time, so the 200 shows that the sign-in went through the
    // app's sign-in manager, which adds it. A second later, the check runs (tests/README.md).
    [Fact]
    public async Task A_later_request_with_the_session_cookie_alone_gets_the_user_from_api_auth_me()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var userId = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var signInClient = app.CreateHttpsClient();
        using var signIn = await signInClient.SignInWithPasskeyAsync(authenticator);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        var cookie = SessionCookieOf(signIn);
        clock.Advance(TimeSpan.FromSeconds(1));

        // Without the antiforgery cookie, or any other the sign-in's client holds.
        using var client = app.CreateClient(
            new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, MePath);
        request.Headers.Add(HeaderNames.Cookie, new CookieHeaderValue(cookie.Name, cookie.Value).ToString());

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(userId, me.GetProperty("userId").GetGuid());
    }

    // Each event names the person as the actor and the User it is about, with no details (S00.04.04's Notes), at the
    // time of the sign-in, in the person's own organisation.
    [Fact]
    public async Task Each_sign_in_writes_one_signin_succeeded_event_in_the_user_s_organisation()
    {
        var userOfA = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        var userOfB = await AddUserAsync(seeded.B, UserRole.Worker, "Jamie Park");
        using var authenticatorOfA = await TestAuthenticator.RegisterAsync(app.Services, userOfA);
        using var authenticatorOfB = await TestAuthenticator.RegisterAsync(app.Services, userOfB);
        var firstAt = clock.GetUtcNow().UtcDateTime;

        await SignInAsync(authenticatorOfA);
        clock.Advance(TimeSpan.FromSeconds(1));
        var secondAt = clock.GetUtcNow().UtcDateTime;
        await SignInAsync(authenticatorOfA);
        await SignInAsync(authenticatorOfB);

        Assert.Equal(
            [
                SignedIn(seeded.A.OrganisationId, userOfA, firstAt),
                SignedIn(seeded.A.OrganisationId, userOfA, secondAt),
            ],
            await StoredEventsAboutAsync(userOfA));
        Assert.Equal([SignedIn(seeded.B.OrganisationId, userOfB, secondAt)], await StoredEventsAboutAsync(userOfB));
    }

    // The passkey options require user verification (design.md §8.4), so an authenticator that checked only that
    // someone was present fails Identity's check.
    [Fact]
    public async Task An_assertion_without_user_verification_is_a_failed_sign_in_on_the_passkey_s_account()
    {
        var userId = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var client = app.CreateHttpsClient();

        using var response = await client.SignInWithPasskeyAsync(
            challenge => authenticator.GetAssertion(challenge, userVerified: false));

        await AssertFailedSignInAsync(response, userId);
    }

    [Fact]
    public async Task An_assertion_signed_with_another_key_is_a_failed_sign_in_on_the_passkey_s_account()
    {
        var userId = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var anotherKey = authenticator.WithAnotherKey();
        using var client = app.CreateHttpsClient();

        using var response = await client.SignInWithPasskeyAsync(challenge => anotherKey.GetAssertion(challenge));

        await AssertFailedSignInAsync(response, userId);
    }

    // Each request for options replaces the ceremony in Identity's two-factor cookie, challenge and all.
    [Fact]
    public async Task An_assertion_over_the_challenge_of_replaced_options_is_a_failed_sign_in_on_the_passkey_s_account()
    {
        var userId = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        var replaced = await client.FetchPasskeyChallengeAsync(token);
        await client.FetchPasskeyChallengeAsync(token);

        using var response = await client.SendPasskeyCredentialAsync(authenticator.GetAssertion(replaced), token);

        await AssertFailedSignInAsync(response, userId);
    }

    // With no ceremony in Identity's two-factor cookie, Identity throws rather than fails, which the app's sign-in
    // manager makes a failed check (S00.04.04's Notes). With no options asked for, the challenge is one that no options
    // gave.
    [Fact]
    public async Task A_known_passkey_sent_without_asking_for_options_is_a_failed_sign_in_not_a_500()
    {
        var userId = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        var challenge = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        using var response = await client.SendPasskeyCredentialAsync(authenticator.GetAssertion(challenge), token);

        await AssertFailedSignInAsync(response, userId);
    }

    // The two-factor cookie that holds the ceremony lasts 5 minutes on the app's clock (D69), after which Identity
    // finds no ceremony, as above.
    [Fact]
    public async Task A_known_passkey_sent_5_minutes_and_1_second_after_its_options_is_a_failed_sign_in_not_a_500()
    {
        var userId = await AddUserAsync(seeded.A, UserRole.Worker, "Sam Taylor");
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var client = app.CreateHttpsClient();
        var token = await client.FetchTokenAsync();
        var challenge = await client.FetchPasskeyChallengeAsync(token);
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        using var response = await client.SendPasskeyCredentialAsync(authenticator.GetAssertion(challenge), token);

        await AssertFailedSignInAsync(response, userId);
    }

    [Fact]
    public async Task A_passkey_that_no_account_holds_gets_a_401_counted_by_one_log_entry_and_no_event()
    {
        using var authenticator = new TestAuthenticator();

        await AssertUnknownCredentialAsync(challenge => authenticator.GetAssertion(challenge));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("""{"type":"public-key"}""")]
    [InlineData("""{"id":"not base64url"}""")]
    public Task A_credential_with_no_ID_that_can_be_read_gets_a_401_counted_by_one_log_entry_and_no_event(
        string credentialJson) =>
        AssertUnknownCredentialAsync(_ => credentialJson);

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static SetCookieHeaderValue SessionCookieOf(HttpResponseMessage response) =>
        Assert.Single(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);

    private static StoredEvent SignedIn(Guid organisationId, Guid userId, DateTime atUtc) =>
        AboutTheUser(AuditEventTypes.Auth.SignInSucceeded, organisationId, userId, atUtc);

    // Each sign-in event names the person as the actor and the User it is about, with no details (S00.04.04's Notes).
    private static StoredEvent AboutTheUser(string eventType, Guid organisationId, Guid userId, DateTime atUtc) =>
        new(organisationId, atUtc, userId, eventType, AuditEntityTypes.User, userId,
            ParticipantId: null, Details: null);

    // A failed sign-in on the account of the user, whom the test added to A: the 401 problem, no session, and the one
    // event that records it, at the time of the sign-in.
    private async Task AssertFailedSignInAsync(HttpResponseMessage response, Guid userId)
    {
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal(
            [
                AboutTheUser(
                    AuditEventTypes.Auth.SignInFailed, seeded.A.OrganisationId, userId, clock.GetUtcNow().UtcDateTime),
            ],
            await StoredEventsAboutAsync(userId));
    }

    // A credential of no account is counted in telemetry only (design.md §5.4, §9.5), by a log entry that holds its
    // fixed message and no value of the request. Identity is never asked about it: asking would clear the ceremony's
    // cookie, as Identity reads the ceremony, so the answer sets no cookie at all.
    private async Task AssertUnknownCredentialAsync(Func<string, string> credentialFor)
    {
        using var client = app.CreateHttpsClient();
        var lastEventId = await LastEventIdAsync();

        using var response = await client.SignInWithPasskeyAsync(credentialFor);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(response.SetCookies());
        var entry = Assert.Single(
            logs.Entries, logged => logged.Category.StartsWith("Grow2Notes.", StringComparison.Ordinal));
        Assert.Equal(typeof(PasskeySignInEndpoint).FullName, entry.Category);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(UnknownCredentialMessage, entry.Message);
        Assert.Null(entry.Exception);
        Assert.Equal([new("{OriginalFormat}", UnknownCredentialMessage)], entry.Values);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // An Active user, as setup leaves one (design.md §8.1), with an address of its own, which no other test or run of
    // this one uses. The seeded users have no passkey, and other tests may rely on that (tests/README.md).
    private async Task<Guid> AddUserAsync(SeededOrganisation organisation, UserRole role, string displayName)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = organisation.OrganisationId,
            DisplayName = displayName,
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = now,
            InvitedByUserId = organisation.ManagerId,
            ActivatedAtUtc = now,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = app.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user.Id;
    }

    private async Task SignInAsync(TestAuthenticator authenticator)
    {
        using var client = app.CreateHttpsClient();
        using var response = await client.SignInWithPasskeyAsync(authenticator);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // An organisation is the tenant, not tenant-owned, so reading one needs no tenant.
    private async Task<string> OrganisationNameAsync(Guid organisationId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .Where(organisation => organisation.Id == organisationId)
            .Select(organisation => organisation.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // Read in SQL, so that neither the tenant filter nor a tracked row decides what is seen, in the order written.
    private async Task<List<StoredEvent>> StoredEventsAboutAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                       [ParticipantId], [Details]
                FROM [AuditEvent]
                WHERE [EntityId] = {userId}
                ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after a request.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed record StoredEvent(
        Guid OrganisationId,
        DateTime OccurredAtUtc,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);
}
