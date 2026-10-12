using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// The limits of a password sign-in (design.md §8, §8.4): the code step lasts 5 minutes from the accepted password,
/// and a wrong code does not extend it (D69); 5 failed attempts, wrong passwords and wrong codes together, lock
/// sign-in for 15 minutes (A25, §9.9), but wrong passwords for an account with no password lock nothing (A55); and
/// failed attempts on one account at once are each refused and audited as one alone is. The two steps are called as
/// the SPA calls them (<see cref="PasswordSignIn"/>), for users that the test adds to seeded organisation A as setup's
/// password path leaves them, or its passkey path. The app runs on a fake clock (<see cref="TestClock"/>), which times
/// Identity's two-factor cookie, and so the code step; Identity checks the codes and times the lockout on the system
/// clock, so the codes are computed, and the lockout's end is read, against the real time.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class PasswordSignInLimitsTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string WrongPassword = "Another passw0rd";

    private const int MaxFailedAccessAttempts = 5;

    private static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(15);

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public PasswordSignInLimitsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
    }

    // D69's 5 minutes, which every test of the two-factor cookie takes from the constant that Program.cs sets on it.
    [Fact]
    public async Task The_right_code_4_minutes_and_59_seconds_after_the_password_signs_in()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), TwoFactorCookie.Lifetime);
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        await SendRightPasswordAsync(client, user);
        clock.Advance(TwoFactorCookie.Lifetime - TimeSpan.FromSeconds(1));

        using var response = await client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.SetCookies(), cookie => cookie.Name.Value == CookieSignIn.SessionCookie);
    }

    // Identity's cookies slide by default: one used after half its lifetime is renewed for a whole one, so with
    // sliding left on, the wrong code at 3 minutes would give the step until 8 minutes. A wrong code before half the
    // 5 minutes would not show it. The expired step names no account, so its 401 writes no event (S00.04.05's Notes).
    [Fact]
    public async Task After_a_wrong_code_at_3_minutes_the_right_code_at_5_minutes_and_1_second_gets_a_401()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        await SendRightPasswordAsync(client, user);
        var wrongCodeAt = TimeSpan.FromMinutes(3);
        clock.Advance(wrongCodeAt);
        await SendRefusedAsync(() => client.SendCodeAsync(WrongCodeOf(user)));
        clock.Advance(TwoFactorCookie.Lifetime + TimeSpan.FromSeconds(1) - wrongCodeAt);

        await SendRefusedAsync(() => client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey)));

        Assert.Equal(
            [AboutTheUser(AuditEventTypes.Auth.MfaFailed, user.Id, TestClock.Start + wrongCodeAt)],
            await StoredEventsAboutAsync(user.Id));
    }

    // The attempt that locks the account writes auth.locked_out in place of its auth.signin_failed, and so does each
    // attempt refused while it lasts, the right password included, which then starts no code step (S00.04.05's Notes).
    [Fact]
    public async Task Five_wrong_passwords_lock_sign_in_for_15_minutes_and_the_right_password_then_gets_a_401()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await SendRefusedAsync(() => client.SendPasswordAsync(user.Email, WrongPassword));
        }

        await SendLockingAttemptAsync(() => client.SendPasswordAsync(user.Email, WrongPassword), user.Id);

        await SendRefusedAsync(() => client.SendPasswordAsync(user.Email, PasswordSignIn.Password));

        Assert.Equal(
            [
                .. Enumerable.Repeat(AboutTheUser(AuditEventTypes.Auth.SignInFailed, user.Id, TestClock.Start), 4),
                AboutTheUser(AuditEventTypes.Auth.LockedOut, user.Id, TestClock.Start),
                AboutTheUser(AuditEventTypes.Auth.LockedOut, user.Id, TestClock.Start),
            ],
            await StoredEventsAboutAsync(user.Id));
    }

    // Identity counts a wrong code as it counts a wrong password (design.md §9.9), and for a user with two-factor on
    // it clears the count only once the code signs them in, so the right password between them clears nothing. Then
    // neither the right password nor the right code, in the code step still open, signs in.
    [Fact]
    public async Task Three_wrong_passwords_the_right_password_and_two_wrong_codes_lock_sign_in_the_same_way()
    {
        var user = await app.AddPasswordUserAsync(seeded.A);
        using var client = app.CreateHttpsClient();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await SendRefusedAsync(() => client.SendPasswordAsync(user.Email, WrongPassword));
        }

        await SendRightPasswordAsync(client, user);
        await SendRefusedAsync(() => client.SendCodeAsync(WrongCodeOf(user)));

        await SendLockingAttemptAsync(() => client.SendCodeAsync(WrongCodeOf(user)), user.Id);

        await SendRefusedAsync(() => client.SendPasswordAsync(user.Email, PasswordSignIn.Password));
        await SendRefusedAsync(() => client.SendCodeAsync(AuthenticatorCodes.Now(user.AuthenticatorKey)));

        Assert.Equal(
            [
                .. Enumerable.Repeat(AboutTheUser(AuditEventTypes.Auth.SignInFailed, user.Id, TestClock.Start), 3),
                AboutTheUser(AuditEventTypes.Auth.MfaFailed, user.Id, TestClock.Start),
                AboutTheUser(AuditEventTypes.Auth.LockedOut, user.Id, TestClock.Start),
                AboutTheUser(AuditEventTypes.Auth.LockedOut, user.Id, TestClock.Start),
                AboutTheUser(AuditEventTypes.Auth.LockedOut, user.Id, TestClock.Start),
            ],
            await StoredEventsAboutAsync(user.Id));
    }

    // An account set up with a passkey has no password, so no password can be right for it, and counting guesses at
    // it would let anyone who knows its email lock its passkey out (A55). Each guess is still a failed sign-in on it.
    [Fact]
    public async Task Wrong_passwords_for_an_account_with_no_password_lock_nothing_and_its_passkey_still_signs_in()
    {
        var (userId, email) = await AddUserWithNoPasswordAsync();
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, userId);
        using var guesser = app.CreateHttpsClient();
        for (var attempt = 1; attempt <= MaxFailedAccessAttempts; attempt++)
        {
            await SendRefusedAsync(() => guesser.SendPasswordAsync(email, WrongPassword));
        }

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            Assert.Equal(0, await users.GetAccessFailedCountAsync(user));
            Assert.Null(await users.GetLockoutEndDateAsync(user));
        }

        using var client = app.CreateHttpsClient();
        using var signIn = await client.SignInWithPasskeyAsync(authenticator);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal(
            [
                .. Enumerable.Repeat(
                    AboutTheUser(AuditEventTypes.Auth.SignInFailed, userId, TestClock.Start), MaxFailedAccessAttempts),
                AboutTheUser(AuditEventTypes.Auth.SignInSucceeded, userId, TestClock.Start),
            ],
            await StoredEventsAboutAsync(userId));
    }

    // Attempts on one account at once each load the user before any saves its failed-attempt count, so Identity's
    // concurrency check refuses every save but the first. Each is still the 401 of a failed sign-in, with its event,
    // not a 500 from the audit writer's save sending the refused count again.
    [Fact]
    public async Task Wrong_passwords_sent_at_once_are_each_a_failed_sign_in_with_its_event()
    {
        const int atOnce = 4;
        var user = await app.AddPasswordUserAsync(seeded.A);
        var gate = new HoldsTheUsersSavesUntilAllArrive(user.Id, atOnce);
        await using var gated = WithGate(gate);
        gate.Arm();
        var clients = Enumerable.Range(0, atOnce).Select(_ => gated.CreateHttpsClient()).ToList();
        try
        {
            // One client each, as each fetches its own antiforgery cookie and token at once.
            await SendRefusedAtOnceAsync(clients.Select(client => client.SendPasswordAsync(user.Email, WrongPassword)));
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }

        Assert.Equal(
            Enumerable.Repeat(AboutTheUser(AuditEventTypes.Auth.SignInFailed, user.Id, TestClock.Start), atOnce),
            await StoredEventsAboutAsync(user.Id));
    }

    // As with the passwords above, for the codes of one code step, sent at once from the device that holds it.
    [Fact]
    public async Task Wrong_codes_sent_at_once_are_each_a_failed_code_with_its_event()
    {
        const int atOnce = 2;
        var user = await app.AddPasswordUserAsync(seeded.A);
        var gate = new HoldsTheUsersSavesUntilAllArrive(user.Id, atOnce);
        await using var gated = WithGate(gate);
        using var client = gated.CreateHttpsClient();
        await SendRightPasswordAsync(client, user);
        gate.Arm();

        await SendRefusedAtOnceAsync(Enumerable.Range(0, atOnce).Select(_ => client.SendCodeAsync(WrongCodeOf(user))));

        Assert.Equal(
            Enumerable.Repeat(AboutTheUser(AuditEventTypes.Auth.MfaFailed, user.Id, TestClock.Start), atOnce),
            await StoredEventsAboutAsync(user.Id));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // Identity accepts the codes of the steps either side of now, so the right key's code for a step ten minutes away
    // is a wrong one, which Identity counts towards the lockout.
    private static string WrongCodeOf(PasswordUser user) =>
        AuthenticatorCodes.At(user.AuthenticatorKey, DateTimeOffset.UtcNow.AddMinutes(10));

    // The client then holds Identity's two-factor cookie, which holds the code step, from the fake clock's now.
    private static async Task SendRightPasswordAsync(HttpClient client, PasswordUser user)
    {
        using var response = await client.SendPasswordAsync(user.Email, PasswordSignIn.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task SendRefusedAsync(Func<Task<HttpResponseMessage>> send)
    {
        using var response = await send();
        await AssertRefusedAsync(response);
    }

    // Each request is sent before any answer is read, and each answer is refused as one alone is.
    private static async Task SendRefusedAtOnceAsync(IEnumerable<Task<HttpResponseMessage>> send)
    {
        var responses = await Task.WhenAll(send);
        try
        {
            foreach (var response in responses)
            {
                await AssertRefusedAsync(response);
            }
        }
        finally
        {
            Array.ForEach(responses, response => response.Dispose());
        }
    }

    // The 401 of a failed sign-in, which sets no cookie: no session, and no two-factor cookie, new or renewed.
    private static async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Empty(response.SetCookies());
    }

    // Each sign-in event names the person as the actor and the User it is about, with no details (S00.04.04's Notes),
    // in organisation A, where the tests add their users.
    private StoredEvent AboutTheUser(string eventType, Guid userId, DateTimeOffset at) =>
        new(seeded.A.OrganisationId, at.UtcDateTime, userId, eventType, AuditEntityTypes.User, userId,
            ParticipantId: null, Details: null);

    // The app with each save of the user's row held by the gate.
    private WebApplicationFactory<Program> WithGate(HoldsTheUsersSavesUntilAllArrive gate) =>
        app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(gate))));

    // As the invite and setup's passkey path leave a person (design.md §8.1): Active, with the authenticator key that
    // the invite stores, no password and two-factor off, and with an address that no other test or run uses.
    private async Task<(Guid Id, string Email)> AddUserWithNoPasswordAsync()
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        await using var scope = app.Services.CreateAsyncScope();
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = seeded.A.OrganisationId,
            DisplayName = PasswordSignIn.DisplayName,
            Role = UserRole.Worker,
            Status = UserStatus.Active,
            InvitedAtUtc = now,
            InvitedByUserId = seeded.A.ManagerId,
            ActivatedAtUtc = now,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        AssertSucceeded(await users.CreateAsync(user));
        AssertSucceeded(await users.ResetAuthenticatorKeyAsync(user));
        return (user.Id, email);
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // The fifth failed attempt, refused as the others are, locks the account until 15 minutes after it by the system
    // clock, on which Identity times the lockout.
    private async Task SendLockingAttemptAsync(Func<Task<HttpResponseMessage>> send, Guid userId)
    {
        var before = DateTimeOffset.UtcNow;
        await SendRefusedAsync(send);
        var after = DateTimeOffset.UtcNow;

        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        var lockoutEnd = await users.GetLockoutEndDateAsync(user);
        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd.Value, before + LockoutTime, after + LockoutTime);
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

    /// <summary>
    /// Holds each save that changes the row of the user whose ID it is given, once the test arms it, until
    /// <paramref name="count"/> are waiting, then lets them all go, so that each attempt has loaded the user before any
    /// saves, as when attempts on one account arrive at once. Every later save passes.
    /// </summary>
    private sealed class HoldsTheUsersSavesUntilAllArrive(Guid userId, int count) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrived;
        private volatile bool armed;

        public void Arm() => armed = true;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (armed
                && !allArrived.Task.IsCompleted
                && eventData.Context is { } context
                && context.ChangeTracker.Entries<ApplicationUser>().Any(
                    entry => entry.State == EntityState.Modified && entry.Entity.Id == userId))
            {
                if (Interlocked.Increment(ref arrived) == count)
                {
                    allArrived.SetResult();
                }

                await allArrived.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return result;
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
