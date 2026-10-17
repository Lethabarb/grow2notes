using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// Reset sign-in (design.md §4.11, §4.13 <i>Lost phone</i>, §8.6 <i>Reset sign-in</i> and <i>Last active manager</i>,
/// A26): <c>POST /api/admin/users/{id}/setup-link</c> for an Active user, called as the SPA calls it, with an
/// antiforgery token and the user's ETag in <c>If-Match</c>, through the test-only sign-in. Each test adds the users it
/// resets, with their ways to sign in, as setup leaves them; a test of the last-manager rule adds an organisation of
/// its own too, so that it alone decides who is an Active manager there, as other tests add managers to the seeded
/// ones. Each test's app records the emails it sends (<see cref="RecordingEmailSender"/>), runs on a fake clock, and
/// signs in with the session cookie where a test needs it (<see cref="SignedInDevices"/>). The refusals that come
/// before the user's status is read are <see cref="UserSetupLinkEndpointTests"/>', which hold for a reset alike.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SignInResetTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string SetupPasskeyOptionsPath = "/api/auth/setup/passkey/options";

    private const string SetupPasskeyPath = "/api/auth/setup/passkey";

    private const string SetupPasswordPath = "/api/auth/setup/password";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisation a;
    private readonly WebApplicationFactory<Program> app;

    public SignInResetTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        a = sqlServer.Seeded.A;
        app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
    }

    // S00.06.04's first criterion. The dates of the invite and activation, the confirmed address and the lockout stay
    // as they were, as a resend leaves them, so the record shows only what the reset changed.
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public async Task A_reset_leaves_no_way_to_sign_in_makes_the_worker_Invited_and_emails_a_link_that_starts_setup(
        bool passwordAndCode, int passkeys)
    {
        var worker = await AddUserAsync(
            a.OrganisationId, UserRole.Worker, UserStatus.Active, passwordAndCode, passkeys);
        var before = await StoredAsync(worker.Id);
        Assert.Equal(passwordAndCode, before is { PasswordHash: not null, TwoFactorEnabled: true });
        Assert.Equal(passkeys, before.Passkeys);

        using var response = await PostAsync(app, a.ManagerId, worker.Id, IfMatch.ETag(before.ConcurrencyStamp));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        var after = await StoredAsync(worker.Id);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.NotEqual(before.AuthenticatorKey, after.AuthenticatorKey);
        Assert.Equal(
            before with
            {
                Status = UserStatus.Invited,
                SecurityStamp = after.SecurityStamp,
                ConcurrencyStamp = after.ConcurrencyStamp,
                AuthenticatorKey = after.AuthenticatorKey,
                PasswordHash = null,
                TwoFactorEnabled = false,
                Passkeys = 0,
            },
            after);

        // Who sent it to whom, by ID, with no details, as for a resend (A28).
        Assert.Equal(
            [
                new StoredEvent(
                    a.OrganisationId, clock.GetUtcNow().UtcDateTime, a.ManagerId, AuditEventTypes.User.SetupLinkSent,
                    AuditEntityTypes.User, worker.Id, ParticipantId: null, Details: null),
            ],
            await StoredEventsAboutAsync(worker.Id));

        var sent = Assert.Single(Sent(app));
        var link = SetupLinks.FromEmail(sent);
        Assert.Equal(
            SetupLinkEmail.To(
                worker.Email, $"{Grow2NotesFactory.Origin}/setup#u={worker.Id}&t={Uri.EscapeDataString(link.Token)}"),
            sent);
        using var client = app.CreateHttpsClient();
        await client.StartSetupAsync(link);
    }

    // S00.06.04's second criterion: the new stamp ends both sessions, whatever each device asks next.
    [Fact]
    public async Task Each_device_the_worker_is_signed_in_on_gets_401_on_its_next_request()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        using var devices = await SignedInDevices.SignInAsync(app, clock, worker.Id);

        using var response = await PostAsync(app, a.ManagerId, worker.Id, await ETagAsync(worker.Id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await devices.AssertNextRequestsAsync(
            HttpStatusCode.Unauthorized, SignedInDevices.Me, SignedInDevices.Ping);
    }

    // S00.06.04's fourth criterion, by S00.04.02's path. Completion signs the person in again, with the passkey they
    // add, which the test-only handler stands in for (tests/README.md).
    [Fact]
    public async Task After_a_reset_the_worker_sets_up_again_with_a_passkey_and_is_signed_in()
    {
        await using var withPasskeys = app.WithWebHostBuilder(builder => builder.UseTestPasskeyHandler());
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        using var reset = await PostAsync(withPasskeys, a.ManagerId, worker.Id, await ETagAsync(worker.Id));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        using var client = withPasskeys.CreateHttpsClient();
        var (token, _) = await client.StartSetupAsync(SetupLinks.FromEmail(Assert.Single(Sent(withPasskeys))));
        using (var options = await client.PostWithTokenAsync(SetupPasskeyOptionsPath, token))
        {
            Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        }

        using (var completed = await client.PostWithTokenAsync(
            SetupPasskeyPath, token, new { credentialJson = TestPasskeyHandler.Credential, name = "Grow2Notes" }))
        {
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        }

        await AssertSignedInAsync(client, worker.Id);
        var stored = await StoredAsync(worker.Id);
        Assert.Equal(UserStatus.Active, stored.Status);
        Assert.Null(stored.PasswordHash);
        Assert.Equal(1, stored.Passkeys);
    }

    // S00.06.04's fourth criterion, by S00.04.03's path, for a person who had passkeys: setup signs them in, and they
    // sign in with the new password and a code from the new key on another device.
    [Fact]
    public async Task After_a_reset_the_worker_sets_up_again_with_a_password_and_a_code_and_signs_in_with_them()
    {
        var worker = await AddUserAsync(
            a.OrganisationId, UserRole.Worker, UserStatus.Active, passwordAndCode: false, passkeys: 2);
        using var reset = await PostAsync(app, a.ManagerId, worker.Id, await ETagAsync(worker.Id));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        using var setUp = app.CreateHttpsClient();
        var (token, _) = await setUp.StartSetupAsync(SetupLinks.FromEmail(Assert.Single(Sent(app))));
        var key = (await StoredAsync(worker.Id)).AuthenticatorKey;
        Assert.NotNull(key);
        var body = new { password = PasswordSignIn.Password, totpCode = AuthenticatorCodes.Now(key) };
        using (var completed = await setUp.PostWithTokenAsync(SetupPasswordPath, token, body))
        {
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        }

        await AssertSignedInAsync(setUp, worker.Id);
        using var another = app.CreateHttpsClient();
        using var signIn = await another.SignInWithPasswordAsync(
            worker.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(key));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        await AssertSignedInAsync(another, worker.Id);
    }

    // S00.06.04's third criterion (A26). An Invited manager has not set up, a Deactivated one cannot sign in, and a
    // worker is no manager, so none of them counts.
    [Fact]
    public async Task The_only_active_manager_resetting_themselves_gets_409_user_last_manager_and_nothing_is_done()
    {
        var organisationId = await AddOrganisationAsync();
        var manager = await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);
        await InviteManagerAsync(organisationId, manager.Id);
        await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Deactivated);
        await AddUserAsync(organisationId, UserRole.Worker, UserStatus.Active);
        var before = await StoredAsync(manager.Id);

        using var response = await PostAsync(app, manager.Id, manager.Id, IfMatch.ETag(before.ConcurrencyStamp));

        await AssertCodeAsync(response, HttpStatusCode.Conflict, "user.last_manager");
        Assert.Equal(before, await StoredAsync(manager.Id));
        Assert.Empty(await StoredEventsAboutAsync(manager.Id));
        Assert.Empty(Sent(app));
    }

    [Fact]
    public async Task With_a_second_active_manager_a_manager_s_reset_of_their_own_sign_in_is_made()
    {
        var organisationId = await AddOrganisationAsync();
        var manager = await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);
        await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);

        using var response = await PostAsync(app, manager.Id, manager.Id, await ETagAsync(manager.Id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(UserStatus.Invited, (await StoredAsync(manager.Id)).Status);
        var written = Assert.Single(await StoredEventsAboutAsync(manager.Id));
        Assert.Equal(manager.Id, written.ActorUserId);
        Assert.Equal(AuditEventTypes.User.SetupLinkSent, written.EventType);
        Assert.Equal(manager.Email, Assert.Single(Sent(app)).To);
    }

    // users.md's race: each manager's page still shows the other Active. The first's check has passed and its reset is
    // held before its first save, so the second's check waits on the organisation's lock that the first holds, and then
    // finds the first's reset committed, so an Active manager remains. Without the lock, the second's check would pass
    // as the first's did, and both resets would be made.
    [Fact]
    public async Task Two_managers_resetting_each_other_at_once_give_one_204_and_one_409_user_last_manager()
    {
        var organisationId = await AddOrganisationAsync();
        var first = await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);
        var second = await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);
        var (firstETag, secondETag) = (await ETagAsync(first.Id), await ETagAsync(second.Id));
        var gate = new HoldsTheFirstSaveOfTheUser(second.Id);
        await using var gated = gate.On(app);

        var firstResets = PostAsync(gated, first.Id, second.Id, secondETag);
        Task<HttpResponseMessage> secondResets;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            secondResets = PostAsync(gated, second.Id, first.Id, firstETag);
            while (!secondResets.IsCompleted && !await AnyRequestWaitsForAnUpdateLockAsync())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }

        using var firstResponse = await firstResets;
        using var secondResponse = await secondResets;
        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        await AssertCodeAsync(secondResponse, HttpStatusCode.Conflict, "user.last_manager");
        Assert.Equal(UserStatus.Active, (await StoredAsync(first.Id)).Status);
        Assert.Equal(UserStatus.Invited, (await StoredAsync(second.Id)).Status);
        Assert.Empty(await StoredEventsAboutAsync(first.Id));
        Assert.Single(await StoredEventsAboutAsync(second.Id));
        Assert.Equal(second.Email, Assert.Single(Sent(gated)).To);
    }

    // Two managers reset the same worker at once, both with the ETag that their pages loaded: the second's reset
    // commits while the first's first save is held, and that save's concurrency check then matches no row. That is a
    // user changed since they were loaded, as a stale If-Match is, so it gets 412, never 500, with nothing of it saved,
    // and the second's link stands.
    [Fact]
    public async Task A_reset_whose_first_save_finds_the_worker_changed_since_loaded_gets_412_and_sends_nothing()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var gate = new HoldsTheFirstSaveOfTheUser(worker.Id);
        await using var gated = gate.On(app);
        var loaded = await ETagAsync(worker.Id);

        var first = PostAsync(gated, a.ManagerId, worker.Id, loaded);
        StoredUser afterTheSecond;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await PostAsync(gated, a.ManagerId, worker.Id, loaded);
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
            afterTheSecond = await StoredAsync(worker.Id);
        }
        finally
        {
            gate.Release();
        }

        using var firstResponse = await first;
        await AssertCodeAsync(firstResponse, HttpStatusCode.PreconditionFailed, "precondition.failed");
        Assert.Equal(afterTheSecond, await StoredAsync(worker.Id));
        Assert.Single(await StoredEventsAboutAsync(worker.Id));
        var sent = Assert.Single(Sent(gated));
        using var client = gated.CreateHttpsClient();
        await client.StartSetupAsync(SetupLinks.FromEmail(sent));
    }

    // The email goes before the commit, as for a resend, so a send that fails rolls back the whole reset, and the
    // person signs in as before (T01's Notes).
    [Fact]
    public async Task A_sender_that_fails_gives_500_and_leaves_the_worker_as_they_were()
    {
        await using var failing = factory.WithWebHostBuilder(builder => builder
            .UseClock(clock)
            .ConfigureTestServices(FailingEmailSender.ReplaceAppSender));
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active, passkeys: 1);
        var before = await StoredAsync(worker.Id);

        using var response = await PostAsync(failing, a.ManagerId, worker.Id, IfMatch.ETag(before.ConcurrencyStamp));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Equal(before, await StoredAsync(worker.Id));
        Assert.Empty(await StoredEventsAboutAsync(worker.Id));
        using var client = failing.CreateHttpsClient();
        using var signIn = await client.SignInWithPasswordAsync(
            worker.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(worker.AuthenticatorKey));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // For an email change on a Deactivated person, which S00.06.06 sends through the reset step: they stay Deactivated
    // until a manager reactivates them (D75). Reset sign-in itself never reaches the step for one.
    [Fact]
    public async Task The_reset_step_leaves_a_Deactivated_person_Deactivated_with_no_way_to_sign_in()
    {
        var person = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Deactivated, passkeys: 1);
        var before = await StoredAsync(person.Id);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(person.Id.ToString());
            Assert.NotNull(user);
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<SignInReset>().ResetAsync(user));
        }

        var after = await StoredAsync(person.Id);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.NotEqual(before.AuthenticatorKey, after.AuthenticatorKey);
        Assert.Equal(
            before with
            {
                SecurityStamp = after.SecurityStamp,
                ConcurrencyStamp = after.ConcurrencyStamp,
                AuthenticatorKey = after.AuthenticatorKey,
                PasswordHash = null,
                TwoFactorEnabled = false,
                Passkeys = 0,
            },
            after);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static IReadOnlyCollection<EmailMessage> Sent(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<RecordingEmailSender>().Sent;

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        Assert.Equal(code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // A second on, so that the session check compares the cookie's stamp with the row's, which completion rotated.
    private async Task AssertSignedInAsync(HttpClient client, Guid userId)
    {
        clock.Advance(TimeSpan.FromSeconds(1));
        using var me = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var session = await me.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(userId.ToString(), session.GetProperty("userId").GetString());
    }

    // Posts as the SPA does, signed in as callerId, with the antiforgery token and ifMatch.
    private static async Task<HttpResponseMessage> PostAsync(
        WebApplicationFactory<Program> host, Guid callerId, Guid userId, string ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(callerId);
        var token = await client.FetchTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{userId}/setup-link");
        request.Headers.Add(AntiforgeryTokens.Header, token);
        request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private async Task<Guid> AddOrganisationAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Sign-in reset test",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organisation.Id;
    }

    // A user as setup leaves one (design.md §8.1), with an address that no other test or run uses: with a password, an
    // authenticator key and two-factor on, as its password path leaves them, unless passwordAndCode is false, and with
    // that many passkeys besides, as its passkey path leaves one each, with the key the invite made.
    private async Task<AddedUser> AddUserAsync(
        Guid organisationId, UserRole role, UserStatus status, bool passwordAndCode = true, int passkeys = 0)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = "Sam Taylor",
            Role = role,
            Status = status,
            InvitedAtUtc = now,
            ActivatedAtUtc = now,
            DeactivatedAtUtc = status == UserStatus.Deactivated ? now : null,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        AssertSucceeded(passwordAndCode
            ? await users.CreateAsync(user, PasswordSignIn.Password)
            : await users.CreateAsync(user));
        AssertSucceeded(await users.ResetAuthenticatorKeyAsync(user));
        AssertSucceeded(await users.SetTwoFactorEnabledAsync(user, passwordAndCode));
        for (var i = 0; i < passkeys; i++)
        {
            (await TestAuthenticator.RegisterAsync(app.Services, user.Id)).Dispose();
        }

        var key = await users.GetAuthenticatorKeyAsync(user);
        Assert.NotNull(key);
        return new(user.Id, email, key);
    }

    // Through the invite core, so Invited with no way to sign in yet.
    private async Task InviteManagerAsync(Guid organisationId, Guid invitedByUserId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var invite = await scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
            organisationId, "Alex Morgan", $"{Guid.NewGuid():N}@example.org", UserRole.Manager, invitedByUserId);
        Assert.True(invite.Succeeded, string.Join(" ", invite.Errors.Select(error => error.Code)));
    }

    // Whether a request on the database waits for an update lock, as the last-manager rule's check waits for the
    // organisation's while another change holds it.
    private async Task<bool> AnyRequestWaitsForAnUpdateLockAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value]
                FROM sys.dm_exec_requests
                WHERE [wait_type] = 'LCK_M_U' AND [database_id] = DB_ID()
                """)
            .SingleAsync(TestContext.Current.CancellationToken) > 0;
    }

    // The ETag that GET /api/admin/users gives the SPA for the user now (D78).
    private async Task<string> ETagAsync(Guid userId) => IfMatch.ETag((await StoredAsync(userId)).ConcurrencyStamp);

    // In a scope of its own, so it reads what has committed.
    private async Task<StoredUser> StoredAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        Assert.NotNull(user.ConcurrencyStamp);
        return new(
            user.Status,
            user.SecurityStamp,
            user.ConcurrencyStamp,
            await users.GetAuthenticatorKeyAsync(user),
            user.PasswordHash,
            user.TwoFactorEnabled,
            (await users.GetPasskeysAsync(user)).Count,
            user.InvitedAtUtc,
            user.ActivatedAtUtc,
            user.DeactivatedAtUtc,
            user.EmailConfirmed,
            user.LockoutEnd);
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

    private sealed record AddedUser(Guid Id, string Email, string AuthenticatorKey);

    private sealed record StoredUser(
        UserStatus Status,
        string? SecurityStamp,
        string ConcurrencyStamp,
        string? AuthenticatorKey,
        string? PasswordHash,
        bool TwoFactorEnabled,
        int Passkeys,
        DateTime InvitedAtUtc,
        DateTime? ActivatedAtUtc,
        DateTime? DeactivatedAtUtc,
        bool EmailConfirmed,
        DateTimeOffset? LockoutEnd);

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
