using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login/totp</c> (design.md §6.2, §8.2), the code step of a password sign-in, called as the SPA
/// calls it (<see cref="PasswordSignIn"/>), after the password step, for users that the test adds to seeded
/// organisation A or B as setup's password path leaves them (§8.1). The codes are computed from the user's
/// authenticator key on the real clock, which Identity checks them against (<see cref="AuthenticatorCodes"/>), while
/// the app runs on a fake clock (<see cref="TestClock"/>), which moves a second before a later request of the session.
/// Its log entries are recorded, Identity's at every level. The endpoint matrix covers who may call it, and
/// <see cref="TotpSignInRequestTests"/> a request without a code.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TotpSignInEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string MePath = "/api/auth/me";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly RecordingLoggerProvider logs = new();
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    // Identity, which checks the code, logs only at Debug, below the app's levels, so its entries are recorded at every
    // level, for the test that no entry holds the code; the app's levels still decide the rest.
    public TotpSignInEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.UseClock(clock).ConfigureLogging(logging => logging
            .AddProvider(logs)
            .AddFilter<RecordingLoggerProvider>("Microsoft.AspNetCore.Identity", LogLevel.Trace)));
    }

    // The answer is design.md §6.2's Me, as GET /api/auth/me gives it. The clock starts at 13:30 UTC on 9 October,
    // which is half past midnight on 10 October in Melbourne.
    [Fact]
    public async Task The_right_code_after_the_password_signs_the_user_in_and_the_answer_says_who_they_are()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();

        using var response = await client.SignInWithPasswordAsync(
            user.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(user.AuthenticatorKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        (string Name, string? Value)[] expected =
        [
            ("userId", user.Id.ToString()),
            ("displayName", PasswordSignIn.DisplayName),
            ("role", nameof(UserRole.Worker)),
            ("organisationName", await OrganisationNameAsync(seeded.A.OrganisationId)),
            ("today", "2026-10-10"),
        ];
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(expected, me.EnumerateObject().Select(property => (property.Name, property.Value.GetString())));
    }

    // Identity clears its two-factor cookie as it signs the person in, so the code step is over: the same code again
    // names no account. Every other cookie that the sign-in sets is a deletion, so the browser keeps the session
    // alone: no "remember this device" cookie (A22), with which a later password alone would sign in (design.md §8.1).
    [Fact]
    public async Task The_sign_in_sets_the_session_cookie_and_clears_the_code_step_so_the_same_code_again_gets_a_401()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        var code = AuthenticatorCodes.Now(user.AuthenticatorKey);
        var twoFactorCookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.TwoFactorUserIdScheme).Cookie.Name;
        using var client = app.CreateHttpsClient();

        using var response = await client.SignInWithPasswordAsync(user.Email, PasswordSignIn.Password, code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SessionCookieOf(response).AssertIsTheSessionCookie();
        var others = response.SetCookies().Where(cookie => cookie.Name.Value != CookieSignIn.SessionCookie).ToList();
        Assert.Contains(others, cookie => cookie.Name.Value == twoFactorCookie);
        Assert.All(others, cookie =>
        {
            Assert.Equal(string.Empty, cookie.Value.Value);
            Assert.Equal(DateTimeOffset.UnixEpoch, cookie.Expires);
        });

        using var again = await client.SendCodeAsync(code);

        await again.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(again.SetCookies());
        Assert.Equal(
            [AboutTheUser(AuditEventTypes.Auth.SignInSucceeded, seeded.A.OrganisationId, user.Id)],
            await StoredEventsAboutAsync(user.Id));
    }

    // The session check refuses a session with no sign-in time, so the 200 shows that the sign-in went through the
    // app's sign-in manager, which adds it. A second later, the check runs (tests/README.md).
    [Fact]
    public async Task A_later_request_with_the_session_cookie_alone_gets_the_user_from_api_auth_me()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var signInClient = app.CreateHttpsClient();
        using var signIn = await signInClient.SignInWithPasswordAsync(
            user.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(user.AuthenticatorKey));
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
        Assert.Equal(user.Id, me.GetProperty("userId").GetGuid());
    }

    // Each event names the person as the actor and the User it is about, with no details (S00.04.04's Notes), at the
    // time of the sign-in, in the person's own organisation. The password step writes none.
    [Fact]
    public async Task Each_sign_in_writes_one_signin_succeeded_event_in_the_user_s_organisation()
    {
        var userOfA = await app.AddPasswordUserAsync(seeded.A);
        var userOfB = await app.AddPasswordUserAsync(seeded.B);

        await SignInAsync(userOfA);
        await SignInAsync(userOfB);

        Assert.Equal(
            [AboutTheUser(AuditEventTypes.Auth.SignInSucceeded, seeded.A.OrganisationId, userOfA.Id)],
            await StoredEventsAboutAsync(userOfA.Id));
        Assert.Equal(
            [AboutTheUser(AuditEventTypes.Auth.SignInSucceeded, seeded.B.OrganisationId, userOfB.Id)],
            await StoredEventsAboutAsync(userOfB.Id));
    }

    // As people type a code: grouped as the app shows it, with a space or a hyphen, or in full-width digits from a
    // phone keyboard set to another script. The code check reads each as its six digits (totp-setup.md).
    [Fact]
    public async Task A_code_typed_with_a_space_or_a_hyphen_or_in_full_width_digits_signs_in()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);

        foreach (var typed in new Func<string, string>[]
        {
            code => $"{code[..3]} {code[3..]}",
            code => $"{code[..3]}-{code[3..]}",
            code => string.Concat(code.Select(digit => (char)(digit - '0' + '０'))),
        })
        {
            using var client = app.CreateHttpsClient();

            using var response = await client.SignInWithPasswordAsync(
                user.Email, PasswordSignIn.Password, typed(AuthenticatorCodes.Now(user.AuthenticatorKey)));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            SessionCookieOf(response).AssertIsTheSessionCookie();
        }
    }

    // Identity accepts the codes of the steps either side of now, so a code of the right key for a step ten minutes
    // away is a wrong one, which Identity counts towards the lockout, with the password failures (design.md §9.9).
    [Fact]
    public async Task A_wrong_code_is_a_failed_code_that_counts_towards_the_lockout()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();

        using var response = await client.SignInWithPasswordAsync(
            user.Email,
            PasswordSignIn.Password,
            AuthenticatorCodes.At(user.AuthenticatorKey, DateTimeOffset.UtcNow.AddMinutes(10)));

        await AssertFailedSignInAsync(response, user.Id, AuditEventTypes.Auth.MfaFailed);
        Assert.Equal(1, await AccessFailedCountAsync(user.Id));
    }

    // Refused by the code check before Identity, which would read "12345" as the code 012345 and count any wrong code
    // towards the lockout (totp-setup.md Implementation notes).
    [Theory]
    [InlineData("12345")]
    [InlineData("abcdef")]
    public async Task A_badly_formed_code_is_a_failed_code_that_does_not_count_towards_the_lockout(string code)
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();

        using var response = await client.SignInWithPasswordAsync(user.Email, PasswordSignIn.Password, code);

        await AssertFailedSignInAsync(response, user.Id, AuditEventTypes.Auth.MfaFailed);
        Assert.Equal(0, await AccessFailedCountAsync(user.Id));
    }

    // No password step came first, so the request names no account, and there is nothing to audit (S00.04.05's
    // Notes).
    [Fact]
    public async Task A_code_with_no_password_step_before_it_gets_a_401_and_no_event()
    {
        using var client = app.CreateHttpsClient();
        var lastEventId = await LastEventIdAsync();

        using var response = await client.SendCodeAsync("123456");

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(response.SetCookies());
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // A passkey ceremony and the code step share Identity's two-factor cookie, so asking for passkey options replaces
    // the code step with a ceremony, which names no account (S00.04.04's Notes).
    [Fact]
    public async Task The_right_code_sent_after_passkey_options_replaced_the_code_step_gets_a_401_and_no_event()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        await client.FetchPasskeyChallengeAsync(await client.FetchTokenAsync());

        using var response = await client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey));

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Empty(await StoredEventsAboutAsync(user.Id));
    }

    // Only an Active user may sign in (design.md §8.2). Identity's two-factor cookie holds only the user's ID and is
    // not checked against the security stamp, so the status check that Identity repeats at the code step is what
    // refuses a deactivated user.
    [Fact]
    public async Task A_user_deactivated_between_the_steps_gets_a_failed_sign_in()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        AssertSucceeded(await WithUserAsync(user.Id, (users, loaded) =>
        {
            loaded.Status = UserStatus.Deactivated;
            return users.UpdateAsync(loaded);
        }));

        using var response = await client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey));

        await AssertFailedSignInAsync(response, user.Id, AuditEventTypes.Auth.SignInFailed);
    }

    // A lockout refuses even the right code (A25). Identity's UserManager compares the lockout's end with
    // DateTimeOffset.UtcNow, not the app's TimeProvider, so the end is set by the system clock, 15 minutes on, as
    // A25's lockout sets it.
    [Fact]
    public async Task A_user_locked_out_between_the_steps_gets_a_401_and_a_locked_out_event()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        using var passwordStep = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, passwordStep.StatusCode);
        AssertSucceeded(await WithUserAsync(
            user.Id, (users, loaded) => users.SetLockoutEndDateAsync(loaded, DateTimeOffset.UtcNow.AddMinutes(15))));

        using var response = await client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey));

        await AssertFailedSignInAsync(response, user.Id, AuditEventTypes.Auth.LockedOut);
    }

    // design.md §9.5: no log entry holds a secret. Identity notes each code that it refuses, at Debug.
    [Fact]
    public async Task No_log_entry_holds_the_code()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        var rightCode = AuthenticatorCodes.Now(user.AuthenticatorKey);
        var wrongCode = AuthenticatorCodes.At(user.AuthenticatorKey, DateTimeOffset.UtcNow.AddMinutes(10));
        var badlyFormedCode = "12345";

        foreach (var (code, status) in new[]
        {
            (wrongCode, HttpStatusCode.Unauthorized),
            (badlyFormedCode, HttpStatusCode.Unauthorized),
            (rightCode, HttpStatusCode.OK),
        })
        {
            using var client = app.CreateHttpsClient();
            using var response = await client.SignInWithPasswordAsync(user.Email, PasswordSignIn.Password, code);
            Assert.Equal(status, response.StatusCode);
        }

        Assert.NotEmpty(logs.Entries);
        string[] secrets = [rightCode, wrongCode, badlyFormedCode];
        Assert.All(logs.Entries, entry =>
        {
            string?[] logged =
            [
                entry.Message,
                entry.Exception?.ToString(),
                .. entry.Values.Select(value => Convert.ToString(value.Value, CultureInfo.InvariantCulture)),
            ];
            Assert.DoesNotContain(
                logged,
                text => text is not null && secrets.Any(secret => text.Contains(secret, StringComparison.Ordinal)));
        });
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static SetCookieHeaderValue SessionCookieOf(HttpResponseMessage response) =>
        Assert.Single(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // Each sign-in event names the person as the actor and the User it is about, with no details (S00.04.04's Notes),
    // at the time of the attempt.
    private StoredEvent AboutTheUser(string eventType, Guid organisationId, Guid userId) =>
        new(organisationId, clock.GetUtcNow().UtcDateTime, userId, eventType, AuditEntityTypes.User, userId,
            ParticipantId: null, Details: null);

    // A failed sign-in on the account of the user, whom the test added to A: the 401 problem, no session, and the one
    // event that records it, the password step having written none.
    private async Task AssertFailedSignInAsync(HttpResponseMessage response, Guid userId, string eventType)
    {
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal([AboutTheUser(eventType, seeded.A.OrganisationId, userId)], await StoredEventsAboutAsync(userId));
    }

    private async Task SignInAsync(PasswordUser user)
    {
        using var client = app.CreateHttpsClient();
        using var response = await client.SignInWithPasswordAsync(
            user.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(user.AuthenticatorKey));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<int> AccessFailedCountAsync(Guid userId) =>
        WithUserAsync(userId, (users, user) => users.GetAccessFailedCountAsync(user));

    // Runs work with the app's UserManager on the user, loaded afresh, as a sign-in loads the user it signs in.
    private async Task<T> WithUserAsync<T>(
        Guid userId, Func<UserManager<ApplicationUser>, ApplicationUser, Task<T>> work)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return await work(users, user);
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
