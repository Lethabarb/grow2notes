using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Net.Http.Headers;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// <c>POST /api/auth/login</c> (design.md §6.2, §8.2), the password step of a password sign-in, called as the SPA
/// calls it (<see cref="PasswordSignIn"/>), for users that the test adds to seeded organisation A or B with a password
/// and an authenticator key, and two-factor on, as setup's password-and-app path leaves them, or off, as the
/// password-only path does (§8.1, D97). A password that Identity accepts from a user with two-factor on leads to
/// their second step, the code or, with a passkey, the passkey, and signs no one in; a user with it off is signed in
/// by the password alone, with the event of it (§8.1's invariant). The app runs on a fake clock
/// (<see cref="TestClock"/>), and its log entries are recorded. The endpoint matrix covers who may call it, and
/// <see cref="PasswordSignInRequestTests"/> a request without an email or a password.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PasswordSignInEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string WrongPassword = "Another passw0rd";

    private const string UnknownEmailMessage = "Password sign-in with an unknown email";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly RecordingLoggerProvider logs = new();
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public PasswordSignInEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(
            builder => builder.UseClock(clock).ConfigureLogging(logging => logging.AddProvider(logs)));
    }

    // Identity keeps the step in its two-factor cookie, for the code to finish it; no session starts until then, so
    // no event is written either (S00.04.05's Notes). The account is found by its address in any letter case.
    [Fact]
    public async Task The_right_password_gets_the_code_step_with_Identity_s_two_factor_cookie_and_no_session_or_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (_, email, _) = await app.AddPasswordUserAsync(seeded.A);
        var twoFactorCookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.TwoFactorUserIdScheme).Cookie.Name;
        var lastEventId = await LastEventIdAsync();

        foreach (var typed in new[] { email, email.ToUpperInvariant() })
        {
            using var client = app.CreateHttpsClient();

            using var response = await client.SendPasswordAsync(typed, PasswordSignIn.Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("""{"next":"totp"}""", await response.Content.ReadAsStringAsync(cancellationToken));
            Assert.Equal([twoFactorCookie], response.SetCookies().Select(cookie => cookie.Name.Value));
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // Each event names the person as the actor and the User it is about, with no details (S00.04.04's Notes), at the
    // time of the attempt, in the person's own organisation.
    [Fact]
    public async Task A_wrong_password_is_a_failed_sign_in_on_the_account_in_the_user_s_organisation()
    {
        var (userOfA, emailOfA, _) = await app.AddPasswordUserAsync(seeded.A);
        var (userOfB, emailOfB, _) = await app.AddPasswordUserAsync(seeded.B);
        using var clientOfA = app.CreateHttpsClient();
        using var clientOfB = app.CreateHttpsClient();

        using var responseOfA = await clientOfA.SendPasswordAsync(emailOfA, WrongPassword);
        using var responseOfB = await clientOfB.SendPasswordAsync(emailOfB, WrongPassword);

        await AssertFailedSignInAsync(responseOfA, userOfA, AuditEventTypes.Auth.SignInFailed);
        await AssertFailedSignInAsync(responseOfB, userOfB, AuditEventTypes.Auth.SignInFailed, seeded.B);
    }

    // Only an Active user may sign in (design.md §8.2). A Deactivated person keeps their password (§8.6).
    [Fact]
    public Task A_Deactivated_user_s_right_password_is_a_failed_sign_in_on_their_account() =>
        AssertStatusRefusesTheRightPasswordAsync(UserStatus.Deactivated);

    // An Invited user has not finished setup, or has had their sign-in reset.
    [Fact]
    public Task An_Invited_user_s_right_password_is_a_failed_sign_in_on_their_account() =>
        AssertStatusRefusesTheRightPasswordAsync(UserStatus.Invited);

    // A lockout refuses even the right password (A25), and each refusal while it lasts is audited as one.
    [Fact]
    public async Task A_locked_out_user_s_right_password_is_refused_with_a_locked_out_event()
    {
        var (userId, email, _) = await app.AddPasswordUserAsync(seeded.A);
        await LockOutAsync(userId);
        using var client = app.CreateHttpsClient();

        using var response = await client.SendPasswordAsync(email, PasswordSignIn.Password);

        await AssertFailedSignInAsync(response, userId, AuditEventTypes.Auth.LockedOut);
    }

    // An email of no account is counted in telemetry only (design.md §5.4, §9.5), by a log entry that holds its fixed
    // message and no value of the request, and Identity is never asked about it.
    [Fact]
    public async Task An_email_that_no_account_holds_gets_a_401_counted_by_one_log_entry_and_no_event()
    {
        using var client = app.CreateHttpsClient();
        var lastEventId = await LastEventIdAsync();

        using var response = await client.SendPasswordAsync(
            $"{Guid.NewGuid():N}@example.org", PasswordSignIn.Password);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(response.SetCookies());
        var entry = Assert.Single(
            logs.Entries, logged => logged.Category.StartsWith("Grow2Notes.", StringComparison.Ordinal));
        Assert.Equal(typeof(PasswordSignInEndpoint).FullName, entry.Category);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(UnknownEmailMessage, entry.Message);
        Assert.Null(entry.Exception);
        Assert.Equal([new("{OriginalFormat}", UnknownEmailMessage)], entry.Values);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // A person who set up with a password only has two-factor off, so Identity signs them straight in (D97), and the
    // step writes the event of the sign-in, as the code step does. The answer is who they are, and the session works.
    [Fact]
    public async Task The_right_password_of_a_user_without_two_factor_signs_them_in_with_its_event()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (userId, email, _) = await app.AddPasswordUserAsync(seeded.A, twoFactorEnabled: false);
        using var client = app.CreateHttpsClient();

        using var response = await client.SendPasswordAsync(email, PasswordSignIn.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(userId, me.GetProperty("userId").GetGuid());
        Assert.Equal(PasswordSignIn.DisplayName, me.GetProperty("displayName").GetString());
        SessionCookieOf(response).AssertIsTheSessionCookie();
        var signedInAt = clock.GetUtcNow().UtcDateTime;
        Assert.Equal(
            [AboutTheUser(AuditEventTypes.Auth.SignInSucceeded, seeded.A.OrganisationId, userId, signedInAt)],
            await StoredEventsAboutAsync(userId));

        // A second later, the session check runs (tests/README.md).
        clock.Advance(TimeSpan.FromSeconds(1));
        using var meAgain = await client.GetAsync("/api/auth/me", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, meAgain.StatusCode);
    }

    // A person who set up with a password and a passkey has two-factor on, and the passkey is their second step
    // (SecondStep); no session starts and nothing is written until the passkey signs them in.
    [Fact]
    public async Task A_password_user_with_a_passkey_is_asked_for_the_passkey_with_no_session_or_event()
    {
        var (userId, email, _) = await app.AddPasswordUserAsync(seeded.A);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        var lastEventId = await LastEventIdAsync();
        using var client = app.CreateHttpsClient();

        using var response = await client.SendPasswordAsync(email, PasswordSignIn.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"next":"passkey"}""", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // A25's lockout counts the password step's failures for an account that has a password, a password only included.
    [Fact]
    public async Task A_password_only_user_s_fifth_wrong_password_locks_them_out()
    {
        var (userId, email, _) = await app.AddPasswordUserAsync(seeded.A, twoFactorEnabled: false);

        for (var i = 0; i < 5; i++)
        {
            using var client = app.CreateHttpsClient();
            using var wrong = await client.SendPasswordAsync(email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        using var rightClient = app.CreateHttpsClient();
        using var right = await rightClient.SendPasswordAsync(email, PasswordSignIn.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, right.StatusCode);
        Assert.DoesNotContain(right.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
        Assert.Equal(
            [
                .. Enumerable.Repeat(AuditEventTypes.Auth.SignInFailed, 4),
                AuditEventTypes.Auth.LockedOut,
                AuditEventTypes.Auth.LockedOut,
            ],
            await EventTypesAboutAsync(userId));
    }

    [Fact]
    public async Task A_Deactivated_password_only_user_s_right_password_gets_401_and_no_session()
    {
        var (userId, email, _) = await app.AddPasswordUserAsync(
            seeded.A, UserStatus.Deactivated, twoFactorEnabled: false);
        using var client = app.CreateHttpsClient();

        using var response = await client.SendPasswordAsync(email, PasswordSignIn.Password);

        await AssertFailedSignInAsync(response, userId, AuditEventTypes.Auth.SignInFailed);
    }

    // The event is written after Identity's own save, with no transaction, so a writer that throws leaves the event but
    // the exception handler's 500 clears the response's headers, the session cookie among them: no session without its
    // event (PasskeySignInEndpoint).
    [Fact]
    public async Task If_its_event_cannot_be_written_a_password_only_sign_in_starts_no_session()
    {
        await using var failing = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IAuditWriter>(
                provider => new FailsAfterWriting(ActivatorUtilities.CreateInstance<AuditWriter>(provider)))));
        var (_, email, _) = await app.AddPasswordUserAsync(seeded.A, twoFactorEnabled: false);
        using var client = failing.CreateHttpsClient();

        using var response = await client.SendPasswordAsync(email, PasswordSignIn.Password);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.DoesNotContain(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    // design.md §9.5: no log entry holds personal data or a secret. Identity logs a lockout, and the app an unknown
    // email.
    [Fact]
    public async Task No_log_entry_holds_the_email_or_the_password()
    {
        var (_, email, _) = await app.AddPasswordUserAsync(seeded.A);
        var (_, passwordOnlyEmail, _) = await app.AddPasswordUserAsync(seeded.A, twoFactorEnabled: false);
        var (lockedOutUserId, lockedOutEmail, _) = await app.AddPasswordUserAsync(seeded.A);
        await LockOutAsync(lockedOutUserId);
        var unknownEmail = $"{Guid.NewGuid():N}@example.org";

        foreach (var (typed, password, status) in new[]
        {
            (email, PasswordSignIn.Password, HttpStatusCode.OK),
            (passwordOnlyEmail, PasswordSignIn.Password, HttpStatusCode.OK),
            (email, WrongPassword, HttpStatusCode.Unauthorized),
            (lockedOutEmail, PasswordSignIn.Password, HttpStatusCode.Unauthorized),
            (unknownEmail, PasswordSignIn.Password, HttpStatusCode.Unauthorized),
        })
        {
            using var client = app.CreateHttpsClient();
            using var response = await client.SendPasswordAsync(typed, password);
            Assert.Equal(status, response.StatusCode);
        }

        Assert.NotEmpty(logs.Entries);
        string[] secrets =
        [email, passwordOnlyEmail, lockedOutEmail, unknownEmail, PasswordSignIn.Password, WrongPassword];
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
                text => text is not null
                    && secrets.Any(secret => text.Contains(secret, StringComparison.OrdinalIgnoreCase)));
        });
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static SetCookieHeaderValue SessionCookieOf(HttpResponseMessage response) =>
        Assert.Single(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);

    // Each sign-in event names the person as the actor and the User it is about, with no details (S00.04.04's Notes).
    private static StoredEvent AboutTheUser(string eventType, Guid organisationId, Guid userId, DateTime atUtc) =>
        new(organisationId, atUtc, userId, eventType, AuditEntityTypes.User, userId,
            ParticipantId: null, Details: null);

    // A failed sign-in on the account of the user, whom the test added to the organisation, A unless it says
    // otherwise: the 401 problem, no cookie, and the one event that records it, at the time of the attempt.
    private async Task AssertFailedSignInAsync(
        HttpResponseMessage response, Guid userId, string eventType, SeededOrganisation? organisation = null)
    {
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(response.SetCookies());
        Assert.Equal(
            [
                AboutTheUser(
                    eventType, (organisation ?? seeded.A).OrganisationId, userId, clock.GetUtcNow().UtcDateTime),
            ],
            await StoredEventsAboutAsync(userId));
    }

    // The user differs from an Active one in status alone and is not locked out, so the status is all that can refuse
    // the right password.
    private async Task AssertStatusRefusesTheRightPasswordAsync(UserStatus status)
    {
        var (userId, email, _) = await app.AddPasswordUserAsync(seeded.A, status);
        using var client = app.CreateHttpsClient();

        using var response = await client.SendPasswordAsync(email, PasswordSignIn.Password);

        await AssertFailedSignInAsync(response, userId, AuditEventTypes.Auth.SignInFailed);
    }

    // Locked out as A25's lockout locks someone out, for 15 minutes. Identity's UserManager compares the lockout's end
    // with DateTimeOffset.UtcNow, not the app's TimeProvider, so the end is set by the system clock.
    private async Task LockOutAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        AssertSucceeded(await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15)));
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // The types of the events about the user, in the order written.
    private async Task<List<string>> EventTypesAboutAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {userId} ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
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

    // Writes the event, then throws, as the writer of a failed write would after the sign-in's own save.
    private sealed class FailsAfterWriting(IAuditWriter writer) : IAuditWriter
    {
        public async Task WriteAsync(
            Guid organisationId,
            Guid? actorUserId,
            string eventType,
            string? entityType,
            Guid? entityId,
            Guid? participantId,
            JsonObject? details,
            CancellationToken cancellationToken = default)
        {
            await writer.WriteAsync(
                organisationId, actorUserId, eventType, entityType, entityId, participantId, details,
                cancellationToken);
            throw new InvalidOperationException("The audit writer failed after writing the event.");
        }
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
