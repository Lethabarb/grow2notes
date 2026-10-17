using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// <c>POST /api/admin/users/{id}/deactivate</c> and <c>…/reactivate</c> (design.md §3.7, §4.11 <i>Deactivate</i> and
/// <i>Reactivate</i>, §6.6, §8.6 <i>Deactivate</i>, §14 M0, A26, D75), called as the SPA calls them, with an
/// antiforgery token and the user's ETag in <c>If-Match</c>, through the test-only sign-in. Each test adds the users
/// it changes, with their ways to sign in, as setup leaves them, or invites them through the invite core
/// (<see cref="SetupLinks.InviteAsync"/>), which writes no audit event and sends no email, so the events about a user
/// and the emails its app sent are the test's alone; a test of the last-manager rule adds an organisation of its own
/// too, so that it alone decides who is an Active manager there. Each test's app records the emails it sends
/// (<see cref="RecordingEmailSender"/>), runs on a fake clock, and signs in with the session cookie where a test needs
/// it (<see cref="SignedInDevices"/>). The endpoint matrix covers who may call them, and <see cref="SignedOutTests"/>
/// what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UserDeactivationEndpointsTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Deactivate = "deactivate";

    private const string Reactivate = "reactivate";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly SeededOrganisations seeded;
    private readonly SeededOrganisation a;
    private readonly WebApplicationFactory<Program> app;

    public UserDeactivationEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        seeded = sqlServer.Seeded;
        a = seeded.A;
        app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
    }

    // S00.06.05's first criterion. The rest of the record stays as it was, their ways to sign in among it, so that a
    // reactivation can give them back.
    [Fact]
    public async Task Deactivating_an_Active_worker_locks_them_out_with_a_new_stamp_and_writes_user_deactivated()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var before = await StoredAsync(worker.Id);

        using var response = await PostAsync(
            app, a.ManagerId, Deactivate, worker.Id, IfMatch.ETag(before.ConcurrencyStamp));

        await AssertNoContentAsync(response);
        var after = await StoredAsync(worker.Id);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(
            before with
            {
                Status = UserStatus.Deactivated,
                DeactivatedAtUtc = clock.GetUtcNow().UtcDateTime,
                LockoutEnd = DateTimeOffset.MaxValue,
                SecurityStamp = after.SecurityStamp,
                ConcurrencyStamp = after.ConcurrencyStamp,
            },
            after);

        // Who did it to whom, by ID, with no details (A28).
        Assert.Equal(
            [
                new StoredEvent(
                    a.OrganisationId, clock.GetUtcNow().UtcDateTime, a.ManagerId, AuditEventTypes.User.Deactivated,
                    AuditEntityTypes.User, worker.Id, ParticipantId: null, Details: null),
            ],
            await StoredEventsAboutAsync(worker.Id));
        Assert.Empty(Sent(app));
    }

    // S00.06.05's second criterion, and design.md §14 M0's "After deactivation, the user's next request is rejected
    // (tested)": the new stamp ends both sessions, whatever each device asks next, and the passkey that signed the
    // worker in just before no longer does.
    [Fact]
    public async Task Each_device_of_a_deactivated_worker_gets_401_on_its_next_request_and_their_passkey_too()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active, passwordAndCode: false);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, worker.Id);
        await AssertSignsInWithPasskeyAsync(authenticator, HttpStatusCode.OK);
        using var devices = await SignedInDevices.SignInAsync(app, clock, worker.Id);

        using var response = await PostAsync(app, a.ManagerId, Deactivate, worker.Id, await ETagAsync(worker.Id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await devices.AssertNextRequestsAsync(
            HttpStatusCode.Unauthorized, SignedInDevices.Me, SignedInDevices.Ping);
        await AssertSignsInWithPasskeyAsync(authenticator, HttpStatusCode.Unauthorized);
    }

    // users.md AC27: the new stamp ends the link, for which the setup page shows "This link has expired".
    [Fact]
    public async Task Deactivating_an_Invited_worker_makes_their_setup_link_get_410()
    {
        var invited = await app.InviteAsync(a);
        var userId = Guid.Parse(invited.UserId);

        using var response = await PostAsync(app, a.ManagerId, Deactivate, userId, await ETagAsync(userId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(UserStatus.Deactivated, (await StoredAsync(userId)).Status);
        using var client = app.CreateHttpsClient();
        await client.AssertSetupLinkEndedAsync(invited);
    }

    // S00.06.05's third criterion (A26). An Invited manager has not set up, a Deactivated one cannot sign in, and a
    // worker is no manager, so none of them counts.
    [Fact]
    public async Task The_only_active_manager_deactivating_themselves_gets_409_user_last_manager_and_nothing_is_done()
    {
        var organisationId = await AddOrganisationAsync();
        var manager = await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);
        await InviteManagerAsync(organisationId, manager.Id);
        await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Deactivated);
        await AddUserAsync(organisationId, UserRole.Worker, UserStatus.Active);

        await AssertRefusedAsync(
            manager.Id, Deactivate, manager.Id, await ETagAsync(manager.Id), HttpStatusCode.Conflict,
            "user.last_manager");
    }

    [Fact]
    public async Task With_a_second_active_manager_a_manager_s_deactivation_of_themselves_is_made()
    {
        var organisationId = await AddOrganisationAsync();
        var manager = await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);
        await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);

        using var response = await PostAsync(app, manager.Id, Deactivate, manager.Id, await ETagAsync(manager.Id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(UserStatus.Deactivated, (await StoredAsync(manager.Id)).Status);
        var written = Assert.Single(await StoredEventsAboutAsync(manager.Id));
        Assert.Equal(manager.Id, written.ActorUserId);
        Assert.Equal(AuditEventTypes.User.Deactivated, written.EventType);
    }

    // The event is written in the deactivation's transaction, so an audit writer that fails once it has written the
    // event rolls back the event and the whole change with it, and the worker's sessions go on.
    [Fact]
    public async Task A_deactivation_whose_audit_write_fails_gives_500_and_leaves_the_worker_as_they_were()
    {
        await using var failing = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IAuditWriter>(
                provider => new FailsAfterWriting(ActivatorUtilities.CreateInstance<AuditWriter>(provider)))));
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        using var devices = await SignedInDevices.SignInAsync(failing, clock, worker.Id);
        var before = await StoredAsync(worker.Id);

        using var response = await PostAsync(
            failing, a.ManagerId, Deactivate, worker.Id, IfMatch.ETag(before.ConcurrencyStamp));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Equal(before, await StoredAsync(worker.Id));
        Assert.Empty(await StoredEventsAboutAsync(worker.Id));
        await devices.AssertNextRequestsAsync(HttpStatusCode.OK);
    }

    // S00.06.05's fourth criterion, for a person who set up with a passkey, which the deactivation left them: Active
    // again, they sign in with it.
    [Fact]
    public async Task Reactivating_a_person_with_a_passkey_makes_them_Active_and_they_sign_in_with_it_again()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active, passwordAndCode: false);
        using var authenticator = await TestAuthenticator.RegisterAsync(app.Services, worker.Id);

        await AssertReactivatedAfterDeactivatingAsync(worker.Id, UserStatus.Active);

        await AssertSignsInWithPasskeyAsync(authenticator, HttpStatusCode.OK);
    }

    // S00.06.05's fourth criterion, for a person who set up with a password and an authenticator app.
    [Fact]
    public async Task Reactivating_a_person_with_a_password_makes_them_Active_and_they_sign_in_with_it_and_a_code()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);

        await AssertReactivatedAfterDeactivatingAsync(worker.Id, UserStatus.Active);

        using var client = app.CreateHttpsClient();
        using var signIn = await client.SignInWithPasswordAsync(
            worker.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(worker.AuthenticatorKey));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // S00.06.05's fourth criterion and D75, for a person deactivated before they set up: with no way to sign in, they
    // are Invited, their old link still ended, and Resend invite sends them one that starts setup.
    [Fact]
    public async Task Reactivating_a_person_deactivated_while_Invited_makes_them_Invited_and_Resend_invite_works()
    {
        var invited = await app.InviteAsync(a);
        var userId = Guid.Parse(invited.UserId);

        await AssertReactivatedAfterDeactivatingAsync(userId, UserStatus.Invited);

        using var client = app.CreateHttpsClient();
        await client.AssertSetupLinkEndedAsync(invited);
        using var resend = await PostAsync(app, a.ManagerId, "setup-link", userId, await ETagAsync(userId));
        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);
        var sent = Assert.Single(Sent(app));
        Assert.Equal(invited.Email, sent.To);
        await client.StartSetupAsync(SetupLinks.FromEmail(sent));
    }

    // As mcp-server.md §5.1 makes such a call succeed with nothing changed; the screen never sends one.
    [Theory]
    [InlineData(Deactivate, "Deactivated")]
    [InlineData(Reactivate, "Active")]
    public async Task A_call_on_a_user_already_in_that_state_answers_204_and_changes_and_writes_nothing(
        string action, string status)
    {
        var user = await AddUserAsync(a.OrganisationId, UserRole.Worker, Enum.Parse<UserStatus>(status));
        var before = await StoredAsync(user.Id);

        using var response = await PostAsync(app, a.ManagerId, action, user.Id, IfMatch.ETag(before.ConcurrencyStamp));

        await AssertNoContentAsync(response);
        Assert.Equal(before, await StoredAsync(user.Id));
        Assert.Empty(await StoredEventsAboutAsync(user.Id));
        Assert.Empty(Sent(app));
    }

    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task A_missing_If_Match_gets_428_and_nothing_is_done(string action)
    {
        var user = await AddUserToChangeAsync(action);

        await AssertRefusedAsync(
            a.ManagerId, action, user.Id, ifMatch: null, HttpStatusCode.PreconditionRequired,
            "precondition.required");
    }

    // As when another manager changed the user after this one's page loaded them.
    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task A_stale_If_Match_gets_412_and_nothing_is_done(string action)
    {
        var user = await AddUserToChangeAsync(action);
        var stale = await ETagAsync(user.Id);
        await RenameAsync(user.Id);

        await AssertRefusedAsync(
            a.ManagerId, action, user.Id, stale, HttpStatusCode.PreconditionFailed, "precondition.failed");
    }

    // Two managers make the same change at once, both with the ETag that their pages loaded: the second's commits while
    // the first's first save is held, and that save's concurrency check then matches no row. That is a user changed
    // since they were loaded, as a stale If-Match is, so it gets 412, never 500, with nothing of it saved or written.
    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task A_change_whose_first_save_finds_the_user_changed_since_loaded_gets_412_and_does_nothing(
        string action)
    {
        var user = await AddUserToChangeAsync(action);
        var gate = new HoldsTheFirstSaveOfTheUser(user.Id);
        await using var gated = gate.On(app);
        var loaded = await ETagAsync(user.Id);

        var first = PostAsync(gated, a.ManagerId, action, user.Id, loaded);
        StoredUser afterTheSecond;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await PostAsync(gated, a.ManagerId, action, user.Id, loaded);
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
            afterTheSecond = await StoredAsync(user.Id);
        }
        finally
        {
            gate.Release();
        }

        using var firstResponse = await first;
        using var problem = JsonDocument.Parse(
            await firstResponse.ReadProblemAsync(HttpStatusCode.PreconditionFailed));
        Assert.Equal("precondition.failed", problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(afterTheSecond, await StoredAsync(user.Id));
        Assert.Single(await StoredEventsAboutAsync(user.Id));
    }

    // Users have no tenant query filter, so the handler names the organisation itself (design.md §5.9 item 5). B's
    // manager sends A's user's own ETag, so only the organisation can refuse it.
    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task Another_organisation_s_manager_gets_404_and_nothing_is_done(string action)
    {
        var user = await AddUserToChangeAsync(action);

        await AssertRefusedAsync(
            seeded.B.ManagerId, action, user.Id, await ETagAsync(user.Id), HttpStatusCode.NotFound, code: null);
    }

    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task A_worker_gets_403_and_nothing_is_done(string action)
    {
        var user = await AddUserToChangeAsync(action);

        await AssertRefusedAsync(
            a.WorkerId, action, user.Id, await ETagAsync(user.Id), HttpStatusCode.Forbidden, code: null);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static IReadOnlyCollection<EmailMessage> Sent(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<RecordingEmailSender>().Sent;

    private static async Task AssertNoContentAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // Posts as the SPA does, signed in as callerId, with the antiforgery token and, unless it is null, ifMatch.
    private static async Task<HttpResponseMessage> PostAsync(
        WebApplicationFactory<Program> host, Guid callerId, string action, Guid userId, string? ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(callerId);
        var token = await client.FetchTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{userId}/{action}");
        request.Headers.Add(AntiforgeryTokens.Header, token);
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // A manager's deactivation, then their reactivation, each with the ETag that the list then gives: the reactivation
    // ends the lockout, clears the time of the deactivation, gives the status, keeps the rest of the record, the stamp
    // that the deactivation gave among it, writes one user.reactivated with the manager as actor, and sends nothing.
    private async Task AssertReactivatedAfterDeactivatingAsync(Guid userId, UserStatus status)
    {
        using (var deactivated = await PostAsync(app, a.ManagerId, Deactivate, userId, await ETagAsync(userId)))
        {
            Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        }

        var before = await StoredAsync(userId);
        Assert.Equal(UserStatus.Deactivated, before.Status);

        using var response = await PostAsync(
            app, a.ManagerId, Reactivate, userId, IfMatch.ETag(before.ConcurrencyStamp));

        await AssertNoContentAsync(response);
        var after = await StoredAsync(userId);
        Assert.Equal(
            before with
            {
                Status = status,
                DeactivatedAtUtc = null,
                LockoutEnd = null,
                ConcurrencyStamp = after.ConcurrencyStamp,
            },
            after);
        var events = await StoredEventsAboutAsync(userId);
        Assert.Equal(
            [AuditEventTypes.User.Deactivated, AuditEventTypes.User.Reactivated],
            events.Select(written => written.EventType));
        Assert.Equal(
            new StoredEvent(
                a.OrganisationId, clock.GetUtcNow().UtcDateTime, a.ManagerId, AuditEventTypes.User.Reactivated,
                AuditEntityTypes.User, userId, ParticipantId: null, Details: null),
            events[^1]);
        Assert.Empty(Sent(app));
    }

    // A refusal, with its code, if any, that changes the user in no way, writes no event about them and sends nothing.
    private async Task AssertRefusedAsync(
        Guid callerId, string action, Guid userId, string? ifMatch, HttpStatusCode status, string? code)
    {
        var before = await StoredAsync(userId);

        using var response = await PostAsync(app, callerId, action, userId, ifMatch);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        Assert.Equal(
            code,
            problem.RootElement.TryGetProperty(ErrorCode.Extension, out var answered) ? answered.GetString() : null);
        Assert.Equal(before, await StoredAsync(userId));
        Assert.Empty(await StoredEventsAboutAsync(userId));
        Assert.Empty(Sent(app));
    }

    private async Task AssertSignsInWithPasskeyAsync(TestAuthenticator authenticator, HttpStatusCode status)
    {
        using var client = app.CreateHttpsClient();
        using var signIn = await client.SignInWithPasskeyAsync(authenticator);
        Assert.Equal(status, signIn.StatusCode);
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private async Task<Guid> AddOrganisationAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Deactivation test",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organisation.Id;
    }

    // A user as setup leaves one (design.md §8.1), with an address that no other test or run uses: with a password, an
    // authenticator key and two-factor on, as its password path leaves them, unless passwordAndCode is false, for a
    // test that gives them a passkey; and, when Deactivated, the time of it, as a deactivation leaves them.
    private async Task<AddedUser> AddUserAsync(
        Guid organisationId, UserRole role, UserStatus status, bool passwordAndCode = true)
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
        var key = await users.GetAuthenticatorKeyAsync(user);
        Assert.NotNull(key);
        return new(user.Id, email, key);
    }

    // A worker of A whom the action changes: Active, to deactivate, or Deactivated, to reactivate.
    private Task<AddedUser> AddUserToChangeAsync(string action) => AddUserAsync(
        a.OrganisationId, UserRole.Worker, action == Deactivate ? UserStatus.Active : UserStatus.Deactivated);

    // Through the invite core, so Invited with no way to sign in yet.
    private async Task InviteManagerAsync(Guid organisationId, Guid invitedByUserId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var invite = await scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
            organisationId, "Alex Morgan", $"{Guid.NewGuid():N}@example.org", UserRole.Manager, invitedByUserId);
        Assert.True(invite.Succeeded, string.Join(" ", invite.Errors.Select(error => error.Code)));
    }

    // As another manager's change would, through Identity, which gives the user a new concurrency stamp.
    private async Task RenameAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.DisplayName = "Sam Taylor-Jones";
        AssertSucceeded(await users.UpdateAsync(user));
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
            user.DisplayName,
            user.SecurityStamp,
            user.ConcurrencyStamp,
            user.PasswordHash,
            user.TwoFactorEnabled,
            (await users.GetPasskeysAsync(user)).Count,
            user.ActivatedAtUtc,
            user.DeactivatedAtUtc,
            user.LockoutEnd,
            user.AccessFailedCount);
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
        string DisplayName,
        string? SecurityStamp,
        string ConcurrencyStamp,
        string? PasswordHash,
        bool TwoFactorEnabled,
        int Passkeys,
        DateTime? ActivatedAtUtc,
        DateTime? DeactivatedAtUtc,
        DateTimeOffset? LockoutEnd,
        int AccessFailedCount);

    private sealed record StoredEvent(
        Guid OrganisationId,
        DateTime OccurredAtUtc,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);

    // Writes the event, then throws, so that the event and everything saved before it are in the transaction.
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
}
