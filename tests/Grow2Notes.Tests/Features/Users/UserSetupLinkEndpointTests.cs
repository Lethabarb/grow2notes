using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Users;

/// <summary>
/// <c>POST /api/admin/users/{id}/setup-link</c> for an Invited user (design.md §4.11 <i>Resend invite</i>, §6.6, §6.7,
/// A53), called as the SPA calls it, with an antiforgery token and the user's ETag in <c>If-Match</c>, as the seeded
/// organisations' users through the test-only sign-in. Each test invites a user of its own into organisation A through
/// the invite core (<see cref="SetupLinks.InviteAsync"/>), which writes no audit event and sends no email, so the
/// events about the user and the emails its app sent are the resend's alone. Each test's app records the emails it
/// sends (<see cref="RecordingEmailSender"/>) and runs on a fake clock. The refusals that come before the user's status
/// is read hold for an Active user's sign-in reset alike, whose own tests are <see cref="SignInResetTests"/>'. The
/// endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UserSetupLinkEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string PasskeyOptionsPath = "/api/auth/setup/passkey/options";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public UserSetupLinkEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
    }

    // The new link replaces the old one (S00.06.03's first criterion), so only the new link starts setup.
    [Fact]
    public async Task A_resend_emails_a_new_link_that_starts_setup_and_the_old_link_gets_410()
    {
        var old = await app.InviteAsync(seeded.A);
        var before = await StoredAsync(old.UserId);

        using var response = await PostAsync(app, Caller.ManagerOfA, old.UserId, IfMatch.ETag(before.ConcurrencyStamp));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        // Still Invited, with a new key, which rotated the stamp that the old link's token holds.
        var after = await StoredAsync(old.UserId);
        Assert.Equal(UserStatus.Invited, after.Status);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.NotEqual(before.AuthenticatorKey, after.AuthenticatorKey);

        var sent = Assert.Single(Sent(app));
        var link = SetupLinks.FromEmail(sent);
        Assert.Equal(
            SetupLinkEmail.To(
                old.Email, $"{Grow2NotesFactory.Origin}/setup#u={old.UserId}&t={Uri.EscapeDataString(link.Token)}"),
            sent);

        using var client = app.CreateHttpsClient();
        await client.AssertSetupLinkEndedAsync(old);
        await client.StartSetupAsync(link);
    }

    // The resend rotates the security stamp that the enrolment cookie holds, so a setup that the old link started ends,
    // though its cookie is within its 30 minutes (A53).
    [Fact]
    public async Task A_browser_that_started_setup_with_the_old_link_gets_401_from_the_passkey_options()
    {
        var old = await app.InviteAsync(seeded.A);
        using var browser = app.CreateHttpsClient();
        var (token, _) = await browser.StartSetupAsync(old);
        using (var beforeTheResend = await browser.PostWithTokenAsync(PasskeyOptionsPath, token))
        {
            Assert.Equal(HttpStatusCode.OK, beforeTheResend.StatusCode);
        }

        using var resend = await PostAsync(app, Caller.ManagerOfA, old.UserId, await ETagAsync(old.UserId));
        Assert.Equal(HttpStatusCode.NoContent, resend.StatusCode);

        using var afterTheResend = await browser.PostWithTokenAsync(PasskeyOptionsPath, token);

        await afterTheResend.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // S00.06.03's second criterion. The event names the user and the actor by ID, and holds no address (A28).
    [Fact]
    public async Task One_user_setup_link_sent_is_written_with_the_manager_as_actor_and_no_details()
    {
        var invited = await app.InviteAsync(seeded.A);

        using var response = await PostAsync(app, Caller.ManagerOfA, invited.UserId, await ETagAsync(invited.UserId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            [
                new StoredEvent(
                    seeded.A.OrganisationId, clock.GetUtcNow().UtcDateTime, seeded.A.ManagerId,
                    AuditEventTypes.User.SetupLinkSent, AuditEntityTypes.User, Guid.Parse(invited.UserId),
                    ParticipantId: null, Details: null),
            ],
            await StoredEventsAboutAsync(invited.UserId));
    }

    [Fact]
    public async Task A_missing_If_Match_gets_428_and_nothing_is_done()
    {
        var invited = await app.InviteAsync(seeded.A);

        await AssertRefusedAsync(
            Caller.ManagerOfA, invited.UserId, ifMatch: null, HttpStatusCode.PreconditionRequired,
            "precondition.required");
    }

    // As when another manager changed the user after this one's page loaded them. The version is checked before the
    // status, so a user who has left Invited since then gets 412 too, never 409, as the screen offers Resend invite
    // only to an Invited user (T01's Notes).
    [Theory]
    [InlineData("Invited")]
    [InlineData("Deactivated")]
    public async Task A_stale_If_Match_gets_412_and_nothing_is_done(string status)
    {
        var invited = await app.InviteAsync(seeded.A);
        var stale = await ETagAsync(invited.UserId);
        await ChangeUserAsync(invited.UserId, user =>
        {
            user.DisplayName = "Sam Taylor-Jones";
            user.Status = Enum.Parse<UserStatus>(status);
        });

        await AssertRefusedAsync(
            Caller.ManagerOfA, invited.UserId, stale, HttpStatusCode.PreconditionFailed, "precondition.failed");
    }

    // Users have no tenant query filter, so the handler names the organisation itself (design.md §5.9 item 5). B's
    // manager sends A's user's own ETag, so only the organisation can refuse it; and the user is looked for before
    // If-Match is read, so a request that sends none is not found either, never told it lacks one.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Another_organisation_s_user_is_not_found_and_nothing_is_done(bool sendsTheETag)
    {
        var invitedIntoA = await app.InviteAsync(seeded.A);

        await AssertRefusedAsync(
            Caller.ManagerOfB, invitedIntoA.UserId, sendsTheETag ? await ETagAsync(invitedIntoA.UserId) : null,
            HttpStatusCode.NotFound, code: null);
    }

    [Fact]
    public async Task An_ID_no_user_has_is_not_found()
    {
        var before = await LastEventIdAsync();

        using var response = await PostAsync(
            app, Caller.ManagerOfA, Guid.NewGuid().ToString(), IfMatch.ETag(Guid.NewGuid().ToString()));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await LastEventIdAsync());
        Assert.Empty(Sent(app));
    }

    // A Deactivated user is reactivated first (D75); an Active user's sign-in is reset instead (SignInResetTests).
    [Fact]
    public async Task A_Deactivated_user_gets_409_user_not_active_and_nothing_is_done()
    {
        var invited = await app.InviteAsync(seeded.A);
        await ChangeUserAsync(invited.UserId, user => user.Status = UserStatus.Deactivated);

        await AssertRefusedAsync(
            Caller.ManagerOfA, invited.UserId, await ETagAsync(invited.UserId), HttpStatusCode.Conflict,
            "user.not_active");
    }

    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_done()
    {
        var invited = await app.InviteAsync(seeded.A);

        await AssertRefusedAsync(
            Caller.WorkerOfA, invited.UserId, await ETagAsync(invited.UserId), HttpStatusCode.Forbidden, code: null);
    }

    // The email goes before the commit, so a send that fails rolls back the new key and the event, and the old link
    // still works (T01's Notes). The sender's exception holds the address and the link, which stay out of the logs
    // (design.md §9.5).
    [Fact]
    public async Task A_sender_that_fails_gives_500_with_nothing_changed_or_written_and_no_address_or_link_logged()
    {
        var logs = new RecordingLoggerProvider();
        await using var failing = factory.WithWebHostBuilder(builder => builder
            .UseClock(clock)
            .ConfigureLogging(logging => logging.AddProvider(logs))
            .ConfigureTestServices(FailingEmailSender.ReplaceAppSender));
        var invited = await failing.InviteAsync(seeded.A);
        var before = await StoredAsync(invited.UserId);

        using var response = await PostAsync(
            failing, Caller.ManagerOfA, invited.UserId, IfMatch.ETag(before.ConcurrencyStamp));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Equal(before, await StoredAsync(invited.UserId));
        Assert.Empty(await StoredEventsAboutAsync(invited.UserId));
        Assert.Contains(logs.Entries, entry => entry.Exception is EmailNotSentException);
        string[] secrets = [invited.Email, $"{Grow2NotesFactory.Origin}/setup"];
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

        using var client = failing.CreateHttpsClient();
        await client.StartSetupAsync(invited);
    }

    // Two managers resend at once, both with the ETag that their pages loaded: the first's transaction has loaded the
    // user, and the second's resend commits before the first's save, whose concurrency check then matches no row.
    // That is a user changed since they were loaded, as a stale If-Match is, so it gets 412, never 500, and the
    // second's link stands.
    [Fact]
    public async Task A_resend_whose_save_finds_the_user_changed_since_they_were_loaded_gets_412_and_sends_nothing()
    {
        var invited = await app.InviteAsync(seeded.A);
        var gate = new HoldsTheFirstSaveOfTheUser(Guid.Parse(invited.UserId));
        await using var gated = gate.On(app);
        var loaded = await ETagAsync(invited.UserId);

        var first = PostAsync(gated, Caller.ManagerOfA, invited.UserId, loaded);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await PostAsync(gated, Caller.ManagerOfA, invited.UserId, loaded);
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var firstResponse = await first;
        using var problem = JsonDocument.Parse(
            await firstResponse.ReadProblemAsync(HttpStatusCode.PreconditionFailed));
        Assert.Equal("precondition.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Single(await StoredEventsAboutAsync(invited.UserId));
        var sent = Assert.Single(Sent(gated));
        using var client = gated.CreateHttpsClient();
        await client.StartSetupAsync(SetupLinks.FromEmail(sent));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static IReadOnlyCollection<EmailMessage> Sent(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<RecordingEmailSender>().Sent;

    // Posts as the SPA does, with the antiforgery token and, unless it is null, ifMatch.
    private async Task<HttpResponseMessage> PostAsync(
        WebApplicationFactory<Program> host, Caller caller, string userId, string? ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(caller, seeded);
        var token = await client.FetchTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{userId}/setup-link");
        request.Headers.Add(AntiforgeryTokens.Header, token);
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // A refusal, with its code, if any, that changes the user in no way, writes no event about them and sends nothing.
    private async Task AssertRefusedAsync(
        Caller caller, string userId, string? ifMatch, HttpStatusCode status, string? code)
    {
        var before = await StoredAsync(userId);

        using var response = await PostAsync(app, caller, userId, ifMatch);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        Assert.Equal(
            code,
            problem.RootElement.TryGetProperty(ErrorCode.Extension, out var answered) ? answered.GetString() : null);
        Assert.Equal(before, await StoredAsync(userId));
        Assert.Empty(await StoredEventsAboutAsync(userId));
        Assert.Empty(Sent(app));
    }

    // The ETag that GET /api/admin/users gives the SPA for the user now (D78).
    private async Task<string> ETagAsync(string userId) => IfMatch.ETag((await StoredAsync(userId)).ConcurrencyStamp);

    // As another request would change them, through Identity, which gives the user a new concurrency stamp.
    private async Task ChangeUserAsync(string userId, Action<ApplicationUser> change)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        change(user);
        var updated = await users.UpdateAsync(user);
        Assert.True(updated.Succeeded, string.Join(" ", updated.Errors.Select(error => error.Code)));
    }

    // In a scope of its own, so it reads what has committed.
    private async Task<StoredUser> StoredAsync(string userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        Assert.NotNull(user);
        Assert.NotNull(user.ConcurrencyStamp);
        return new(user.Status, user.SecurityStamp, user.ConcurrencyStamp, await users.GetAuthenticatorKeyAsync(user));
    }

    // Read in SQL, so that neither the tenant filter nor a tracked row decides what is seen, in the order written.
    private async Task<List<StoredEvent>> StoredEventsAboutAsync(string userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                       [ParticipantId], [Details]
                FROM [AuditEvent]
                WHERE [EntityId] = {Guid.Parse(userId)}
                ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The ID of the last event written, by any test: the collection's tests run one at a time.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed record StoredUser(
        UserStatus Status, string? SecurityStamp, string ConcurrencyStamp, string? AuthenticatorKey);

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
