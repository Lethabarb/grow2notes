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
/// <c>PUT /api/admin/users/{id}</c> (design.md §4.11 <i>Edit</i>, §6.6, §8.4, §8.6, A26, A27, A53, D75, D76), called
/// as the SPA calls it, with an antiforgery token, the user's ETag in <c>If-Match</c> and the body that Edit user
/// sends, through the test-only sign-in. Each test adds the users it changes, with their ways to sign in, as setup
/// leaves them, or invites them through the invite core (<see cref="SetupLinks.InviteAsync"/>), which writes no audit
/// event and sends no email, so the events about a user and the emails its app sent are the test's alone; a test of
/// the last-manager rule adds an organisation of its own too, so that it alone decides who is an Active manager there.
/// Each test's app records the emails it sends (<see cref="RecordingEmailSender"/>), runs on a fake clock, and signs
/// in with the session cookie where a test needs it (<see cref="SignedInDevices"/>). The endpoint matrix covers who may
/// call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UserEditEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Name = "Sam Taylor";

    private const string NewName = "Sam Taylor-Jones";

    private const string SetupPasskeyOptionsPath = "/api/auth/setup/passkey/options";

    private const string SetupPasswordPath = "/api/auth/setup/password";

    private static readonly NextRequest UserList = NextRequest.Get("/api/admin/users");

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly SeededOrganisation a;
    private readonly WebApplicationFactory<Program> app;

    public UserEditEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        a = seeded.A;
        app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
    }

    // S00.06.06's first criterion: user.updated holds the old and new values of the two that changed alone, named as
    // GET /api/admin/users names them. The save leaves the stamp, so the worker's sessions go on, as a manager's now,
    // with the new name (§8.4).
    [Fact]
    public async Task A_new_name_and_role_write_user_updated_with_those_two_alone_send_nothing_and_keep_the_sessions()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        using var devices = await SignedInDevices.SignInAsync(app, clock, worker.Id);
        var before = await StoredAsync(worker.Id);

        using var response = await PutAsync(
            app, a.ManagerId, worker.Id, Body(NewName, worker.Email, "Manager"),
            IfMatch.ETag(before.ConcurrencyStamp));

        await AssertOkAsync(response);
        var after = await StoredAsync(worker.Id);
        Assert.Equal(
            before with { DisplayName = NewName, Role = UserRole.Manager, ConcurrencyStamp = after.ConcurrencyStamp },
            after);
        Assert.Equal(
            [
                new StoredEvent(
                    a.OrganisationId, clock.GetUtcNow().UtcDateTime, a.ManagerId, AuditEventTypes.User.Updated,
                    AuditEntityTypes.User, worker.Id, ParticipantId: null,
                    Details(
                        """{"displayName":"Sam Taylor","role":"Worker"}""",
                        """{"displayName":"Sam Taylor-Jones","role":"Manager"}""")),
            ],
            await StoredEventsAboutAsync(worker.Id));
        Assert.Empty(Sent(app));
        await devices.AssertNextAnswersAsync(HttpStatusCode.OK, me =>
        {
            Assert.Equal(NewName, me.GetProperty("displayName").GetString());
            Assert.Equal("Manager", me.GetProperty("role").GetString());
        });
    }

    // S00.06.06's third criterion (§8.4): each request rebuilds the role from the user's row, and the change leaves the
    // stamp, so each session goes on as a worker's, refused a manager's endpoint on its next request.
    [Fact]
    public async Task A_manager_made_a_worker_gets_403_from_a_manager_endpoint_on_each_device_s_next_request()
    {
        var manager = await AddUserAsync(a.OrganisationId, UserRole.Manager, UserStatus.Active);
        using var devices = await SignedInDevices.SignInAsync(app, clock, manager.Id);
        await devices.AssertNextRequestsAsync(HttpStatusCode.OK, UserList, UserList);

        using var response = await PutAsync(
            app, a.ManagerId, manager.Id, Body(Name, manager.Email, "Worker"), await ETagAsync(manager.Id));

        await AssertOkAsync(response);
        await devices.AssertNextRequestsAsync(HttpStatusCode.Forbidden, UserList, UserList);
        await devices.AssertNextAnswersAsync(
            HttpStatusCode.OK, me => Assert.Equal("Worker", me.GetProperty("role").GetString()));
    }

    // S00.06.06's second criterion: a new address resets sign-in as Reset sign-in does (S00.06.04), ending both
    // sessions, and the link goes to the new address alone; setting up from it confirms the address, which signs the
    // worker in from then on.
    [Fact]
    public async Task A_new_address_resets_an_Active_worker_s_sign_in_and_sends_the_link_there_alone_which_sets_up()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active, passkeys: 1);
        using var devices = await SignedInDevices.SignInAsync(app, clock, worker.Id);
        var before = await StoredAsync(worker.Id);
        Assert.True(before is { PasswordHash: not null, TwoFactorEnabled: true, Passkeys: 1 });
        var address = NewAddress();

        using var response = await PutAsync(
            app, a.ManagerId, worker.Id, Body(Name, address, "Worker"), IfMatch.ETag(before.ConcurrencyStamp));

        await AssertOkAsync(response);
        var after = await StoredAsync(worker.Id);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.NotEqual(before.AuthenticatorKey, after.AuthenticatorKey);
        Assert.Equal(
            before with
            {
                Email = address,
                UserName = address,
                EmailConfirmed = false,
                Status = UserStatus.Invited,
                SecurityStamp = after.SecurityStamp,
                ConcurrencyStamp = after.ConcurrencyStamp,
                AuthenticatorKey = after.AuthenticatorKey,
                PasswordHash = null,
                TwoFactorEnabled = false,
                Passkeys = 0,
            },
            after);
        var now = clock.GetUtcNow().UtcDateTime;
        Assert.Equal(
            [
                new StoredEvent(
                    a.OrganisationId, now, a.ManagerId, AuditEventTypes.User.Updated, AuditEntityTypes.User,
                    worker.Id, ParticipantId: null, Details(EmailValue(worker.Email), EmailValue(address))),
                new StoredEvent(
                    a.OrganisationId, now, a.ManagerId, AuditEventTypes.User.SetupLinkSent, AuditEntityTypes.User,
                    worker.Id, ParticipantId: null, Details: null),
            ],
            await StoredEventsAboutAsync(worker.Id));
        var sent = Assert.Single(Sent(app));
        var link = SetupLinks.FromEmail(sent);
        Assert.Equal(
            SetupLinkEmail.To(
                address, $"{Grow2NotesFactory.Origin}/setup#u={worker.Id}&t={Uri.EscapeDataString(link.Token)}"),
            sent);
        await devices.AssertNextRequestsAsync(
            HttpStatusCode.Unauthorized, SignedInDevices.Me, SignedInDevices.Ping);

        var key = after.AuthenticatorKey;
        Assert.NotNull(key);
        using var setUp = app.CreateHttpsClient();
        var (token, _) = await setUp.StartSetupAsync(link);
        var body = new { password = PasswordSignIn.Password, totpCode = AuthenticatorCodes.Now(key) };
        using (var completed = await setUp.PostWithTokenAsync(SetupPasswordPath, token, body))
        {
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        }

        var setUpUser = await StoredAsync(worker.Id);
        Assert.Equal((UserStatus.Active, true), (setUpUser.Status, setUpUser.EmailConfirmed));
        using var another = app.CreateHttpsClient();
        using var signIn = await another.SignInWithPasswordAsync(
            address, PasswordSignIn.Password, AuthenticatorCodes.Now(key));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // For an Invited person, as for a resend, the new link's stamp ends the old link and a setup that the old link
    // started, though that setup's cookie is within its 30 minutes (A53, S00.06.04's Note from S00.04.02).
    [Fact]
    public async Task A_new_address_for_an_Invited_person_sends_a_new_link_there_and_ends_the_old_link_and_its_setup()
    {
        var old = await app.InviteAsync(a);
        var userId = Guid.Parse(old.UserId);
        using var browser = app.CreateHttpsClient();
        var (token, _) = await browser.StartSetupAsync(old);
        using (var beforeTheChange = await browser.PostWithTokenAsync(SetupPasskeyOptionsPath, token))
        {
            Assert.Equal(HttpStatusCode.OK, beforeTheChange.StatusCode);
        }

        var address = NewAddress();

        using var response = await PutAsync(
            app, a.ManagerId, userId, Body(Name, address, "Worker"), await ETagAsync(userId));

        await AssertOkAsync(response);
        var stored = await StoredAsync(userId);
        Assert.Equal((UserStatus.Invited, address, false), (stored.Status, stored.Email, stored.EmailConfirmed));
        Assert.Equal(
            [AuditEventTypes.User.Updated, AuditEventTypes.User.SetupLinkSent],
            (await StoredEventsAboutAsync(userId)).Select(written => written.EventType));
        var sent = Assert.Single(Sent(app));
        Assert.Equal(address, sent.To);
        using (var afterTheChange = await browser.PostWithTokenAsync(SetupPasskeyOptionsPath, token))
        {
            await afterTheChange.ReadProblemAsync(HttpStatusCode.Unauthorized);
        }

        using var client = app.CreateHttpsClient();
        await client.AssertSetupLinkEndedAsync(old);
        await client.StartSetupAsync(SetupLinks.FromEmail(sent));
    }

    // S00.06.06's second criterion and D75: the reset is made as for anyone else, but setup needs Status Invited, so no
    // link is sent and no user.setup_link_sent written, and the person stays Deactivated until a manager reactivates
    // them.
    [Fact]
    public async Task A_new_address_for_a_Deactivated_person_resets_their_sign_in_sends_nothing_and_leaves_them_so()
    {
        var person = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Deactivated, passkeys: 1);
        var before = await StoredAsync(person.Id);
        var address = NewAddress();

        using var response = await PutAsync(
            app, a.ManagerId, person.Id, Body(Name, address, "Worker"), IfMatch.ETag(before.ConcurrencyStamp));

        await AssertOkAsync(response);
        var after = await StoredAsync(person.Id);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.NotEqual(before.AuthenticatorKey, after.AuthenticatorKey);
        Assert.Equal(
            before with
            {
                Email = address,
                UserName = address,
                EmailConfirmed = false,
                SecurityStamp = after.SecurityStamp,
                ConcurrencyStamp = after.ConcurrencyStamp,
                AuthenticatorKey = after.AuthenticatorKey,
                PasswordHash = null,
                TwoFactorEnabled = false,
                Passkeys = 0,
            },
            after);
        var written = Assert.Single(await StoredEventsAboutAsync(person.Id));
        Assert.Equal(
            (AuditEventTypes.User.Updated, Details(EmailValue(person.Email), EmailValue(address))),
            (written.EventType, written.Details));
        Assert.Empty(Sent(app));
    }

    // S00.06.06's second criterion and D76: a change of letter case alone is no new address, so it is saved as typed,
    // with both spellings in user.updated, and resets nothing: the stamp, the sessions and the status stay.
    [Fact]
    public async Task A_change_of_letter_case_alone_is_saved_as_typed_and_resets_nothing()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        using var devices = await SignedInDevices.SignInAsync(app, clock, worker.Id);
        var before = await StoredAsync(worker.Id);
        var spelling = worker.Email.ToUpperInvariant();

        using var response = await PutAsync(
            app, a.ManagerId, worker.Id, Body(Name, spelling, "Worker"), IfMatch.ETag(before.ConcurrencyStamp));

        await AssertOkAsync(response);
        var after = await StoredAsync(worker.Id);
        Assert.Equal(
            before with { Email = spelling, UserName = spelling, ConcurrencyStamp = after.ConcurrencyStamp }, after);
        var written = Assert.Single(await StoredEventsAboutAsync(worker.Id));
        Assert.Equal(
            (AuditEventTypes.User.Updated, Details(EmailValue(worker.Email), EmailValue(spelling))),
            (written.EventType, written.Details));
        Assert.Empty(Sent(app));
        await devices.AssertNextRequestsAsync(HttpStatusCode.OK);
    }

    // With nothing changed, nothing is saved, not even a new concurrency stamp, and no event is written (T04's Notes).
    [Fact]
    public async Task A_PUT_of_the_stored_values_answers_200_and_changes_and_writes_nothing()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var before = await StoredAsync(worker.Id);

        using var response = await PutAsync(
            app, a.ManagerId, worker.Id, Body(Name, worker.Email, "Worker"), IfMatch.ETag(before.ConcurrencyStamp));

        await AssertOkAsync(response);
        Assert.Equal(before, await StoredAsync(worker.Id));
        Assert.Empty(await StoredEventsAboutAsync(worker.Id));
        Assert.Empty(Sent(app));
    }

    // S00.06.06's fourth criterion (A26), with a new name besides, which is not saved either.
    [Theory]
    [InlineData("Worker", false)]
    [InlineData("Support", false)]
    [InlineData("Manager", true)]
    public async Task The_only_active_manager_making_themselves_a_non_manager_or_giving_themselves_a_new_address_gets_409(
        string role, bool newAddress)
    {
        var manager = await AddTheOnlyActiveManagerAsync();
        var body = Body(NewName, newAddress ? NewAddress() : manager.Email, role);

        await AssertRefusedAsync(
            manager.Id, manager.Id, body, await ETagAsync(manager.Id), HttpStatusCode.Conflict, "user.last_manager");
    }

    // A26: Support never counts as a manager, so a manager who is not the last may become Support, and the stamp stays,
    // as for any role change.
    [Fact]
    public async Task A_manager_who_is_not_the_last_made_Support_is_saved_with_user_updated_from_Manager_to_Support()
    {
        var manager = await AddUserAsync(a.OrganisationId, UserRole.Manager, UserStatus.Active);

        using var response = await PutAsync(
            app, a.ManagerId, manager.Id, Body(Name, manager.Email, "Support"), await ETagAsync(manager.Id));

        await AssertOkAsync(response);
        Assert.Equal(UserRole.Support, (await StoredAsync(manager.Id)).Role);
        Assert.Equal(
            [
                new StoredEvent(
                    a.OrganisationId, clock.GetUtcNow().UtcDateTime, a.ManagerId, AuditEventTypes.User.Updated,
                    AuditEntityTypes.User, manager.Id, ParticipantId: null,
                    Details("""{"role":"Manager"}""", """{"role":"Support"}""")),
            ],
            await StoredEventsAboutAsync(manager.Id));
    }

    // A26 refuses only what would leave the organisation with no Active manager.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_only_active_manager_s_new_name_or_change_of_letter_case_is_saved(bool letterCase)
    {
        var manager = await AddTheOnlyActiveManagerAsync();
        var (displayName, email) = letterCase ? (Name, manager.Email.ToUpperInvariant()) : (NewName, manager.Email);

        using var response = await PutAsync(
            app, manager.Id, manager.Id, Body(displayName, email, "Manager"), await ETagAsync(manager.Id));

        await AssertOkAsync(response);
        var after = await StoredAsync(manager.Id);
        Assert.Equal((displayName, email, UserStatus.Active), (after.DisplayName, after.Email, after.Status));
        Assert.Equal(AuditEventTypes.User.Updated, Assert.Single(await StoredEventsAboutAsync(manager.Id)).EventType);
    }

    // S00.06.06's fourth criterion and A27: one account per address in the whole app, compared whatever the letter
    // case. B's seeded manager holds it.
    [Fact]
    public async Task An_address_another_organisation_s_user_holds_in_other_letter_case_gets_409_user_email_in_use()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var heldByB = (await StoredAsync(seeded.B.ManagerId)).Email;

        await AssertRefusedAsync(
            a.ManagerId, worker.Id, Body(Name, heldByB.ToUpperInvariant(), "Worker"), await ETagAsync(worker.Id),
            HttpStatusCode.Conflict, "user.email_in_use");
    }

    [Theory]
    [MemberData(nameof(InvalidUserDetails.Names), MemberType = typeof(InvalidUserDetails))]
    public Task A_missing_or_overlong_name_gets_a_422_for_displayName_and_nothing_is_done(
        string? displayName, string error) =>
        AssertInvalidAsync(worker => Body(displayName, worker.Email, "Worker"), ("displayName", error));

    [Theory]
    [MemberData(nameof(InvalidUserDetails.Emails), MemberType = typeof(InvalidUserDetails))]
    public Task A_missing_overlong_or_malformed_address_gets_a_422_for_email_and_nothing_is_done(
        string? email, string error) =>
        AssertInvalidAsync(_ => Body(Name, email, "Worker"), ("email", error));

    [Theory]
    [MemberData(nameof(InvalidUserDetails.Roles), MemberType = typeof(InvalidUserDetails))]
    public Task A_missing_role_or_one_that_is_none_of_Worker_Manager_and_Support_gets_a_422_for_role_and_nothing_is_done(
        string? role) =>
        AssertInvalidAsync(worker => Body(Name, worker.Email, role), ("role", UserDetailsChecks.RoleMissing));

    // So the screen can show every error at once.
    [Fact]
    public Task Every_field_at_fault_is_named_at_once() =>
        AssertInvalidAsync(
            _ => Body(displayName: "", email: "", role: null),
            ("displayName", UserDetailsChecks.NameMissing),
            ("email", UserDetailsChecks.EmailMissing),
            ("role", UserDetailsChecks.RoleMissing));

    // Identity's check of an address passes it and its user name's refuses it, and the one save checks both, so nothing
    // is saved: SetEmailAsync would have saved the address and a new stamp before the user name was refused (T04's
    // Notes).
    [Fact]
    public async Task A_new_address_with_a_letter_outside_ASCII_gets_a_422_and_keeps_the_stamp_sessions_and_status()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        using var devices = await SignedInDevices.SignInAsync(app, clock, worker.Id);

        var problem = await AssertRefusedAsync(
            a.ManagerId, worker.Id, Body(Name, "zoë@example.org", "Worker"), await ETagAsync(worker.Id),
            HttpStatusCode.UnprocessableEntity, "validation.failed");

        AssertErrors(problem, ("email", UserDetailsChecks.EmailNotAnAddress));
        await devices.AssertNextRequestsAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_missing_If_Match_gets_428_and_nothing_is_done()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);

        await AssertRefusedAsync(
            a.ManagerId, worker.Id, ANewNameRoleAndAddress(), ifMatch: null, HttpStatusCode.PreconditionRequired,
            "precondition.required");
    }

    // As when another manager changed the user after this one's page loaded them.
    [Fact]
    public async Task A_stale_If_Match_gets_412_and_nothing_is_done()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var stale = await ETagAsync(worker.Id);
        await RenameAsync(worker.Id);

        await AssertRefusedAsync(
            a.ManagerId, worker.Id, ANewNameRoleAndAddress(), stale, HttpStatusCode.PreconditionFailed,
            "precondition.failed");
    }

    // Two managers edit the same worker at once, both with the ETag that their pages loaded: the second's edit commits
    // while the first's save is held, and that save's concurrency check then matches no row. That is a user changed
    // since they were loaded, as a stale If-Match is, so it gets 412, never 500, with nothing of it saved or written.
    [Fact]
    public async Task An_edit_whose_save_finds_the_worker_changed_since_loaded_gets_412_and_does_nothing()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var gate = new HoldsTheFirstSaveOfTheUser(worker.Id);
        await using var gated = gate.On(app);
        var loaded = await ETagAsync(worker.Id);

        var first = PutAsync(gated, a.ManagerId, worker.Id, Body(NewName, worker.Email, "Worker"), loaded);
        StoredUser afterTheSecond;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await PutAsync(
                gated, a.ManagerId, worker.Id, Body("Sam Taylor-Smith", worker.Email, "Worker"), loaded);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            afterTheSecond = await StoredAsync(worker.Id);
        }
        finally
        {
            gate.Release();
        }

        using var firstResponse = await first;
        await AssertCodeAsync(firstResponse, HttpStatusCode.PreconditionFailed, "precondition.failed");
        Assert.Equal("Sam Taylor-Smith", afterTheSecond.DisplayName);
        Assert.Equal(afterTheSecond, await StoredAsync(worker.Id));
        Assert.Single(await StoredEventsAboutAsync(worker.Id));
    }

    // The email goes before the commit, as Reset sign-in's does, so a send that fails rolls back the whole change, and
    // the worker signs in as before, at their old address (T01's Notes).
    [Fact]
    public async Task A_new_address_whose_email_fails_gives_500_and_leaves_the_worker_as_they_were()
    {
        await using var failing = factory.WithWebHostBuilder(builder => builder
            .UseClock(clock)
            .ConfigureTestServices(FailingEmailSender.ReplaceAppSender));
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var before = await StoredAsync(worker.Id);

        using var response = await PutAsync(
            failing, a.ManagerId, worker.Id, Body(Name, NewAddress(), "Worker"), IfMatch.ETag(before.ConcurrencyStamp));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Equal(before, await StoredAsync(worker.Id));
        Assert.Empty(await StoredEventsAboutAsync(worker.Id));
        using var client = failing.CreateHttpsClient();
        using var signIn = await client.SignInWithPasswordAsync(
            worker.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(worker.AuthenticatorKey));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // Users have no tenant query filter, so the handler names the organisation itself (design.md §5.9 item 5). B's
    // manager sends A's user's own ETag, so only the organisation can refuse it.
    [Fact]
    public async Task Another_organisation_s_manager_gets_404_and_nothing_is_done()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);

        await AssertRefusedAsync(
            seeded.B.ManagerId, worker.Id, ANewNameRoleAndAddress(), await ETagAsync(worker.Id),
            HttpStatusCode.NotFound, code: null);
    }

    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_done()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);

        await AssertRefusedAsync(
            a.WorkerId, worker.Id, ANewNameRoleAndAddress(), await ETagAsync(worker.Id), HttpStatusCode.Forbidden,
            code: null);
    }

    [Fact]
    public async Task A_Support_person_gets_403_and_nothing_is_done()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);

        await AssertRefusedAsync(
            a.SupportId, worker.Id, ANewNameRoleAndAddress(), await ETagAsync(worker.Id), HttpStatusCode.Forbidden,
            code: null);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static IReadOnlyCollection<EmailMessage> Sent(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<RecordingEmailSender>().Sent;

    // An address that no other test or run uses (A35).
    private static string NewAddress() => $"{Guid.NewGuid():N}@example.org";

    // The body as Edit user sends it, with a null value left out, as a field that is missing.
    private static Dictionary<string, string> Body(string? displayName, string? email, string? role) =>
        new (string Name, string? Value)[] { ("displayName", displayName), ("email", email), ("role", role) }
            .Where(field => field.Value is not null)
            .ToDictionary(field => field.Name, field => field.Value!);

    // A change of every field, which would write events and send a link if it were made.
    private static Dictionary<string, string> ANewNameRoleAndAddress() => Body(NewName, NewAddress(), "Manager");

    // user.updated's details in ChangeDetails.Of's shape, from the old and new values of those that changed, as JSON.
    private static string Details(string old, string @new) => $"{{\"old\":{old},\"new\":{@new}}}";

    // An address alone, as JSON, as user.updated holds a change of address.
    private static string EmailValue(string email) => $"{{\"email\":\"{email}\"}}";

    private static async Task AssertOkAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        Assert.Equal(code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    private static void AssertErrors(JsonElement problem, params (string Field, string Error)[] expected) =>
        Assert.Equal(
            expected.Select(error => $"{error.Field}: {error.Error}"),
            problem.GetProperty("errors").EnumerateObject().SelectMany(field =>
                field.Value.EnumerateArray().Select(message => $"{field.Name}: {message.GetString()}")));

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // Puts as the SPA does, signed in as callerId, with the antiforgery token and, unless it is null, ifMatch.
    private static async Task<HttpResponseMessage> PutAsync(
        WebApplicationFactory<Program> host,
        Guid callerId,
        Guid userId,
        Dictionary<string, string> body,
        string? ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(callerId);
        var token = await client.FetchTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/users/{userId}")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add(AntiforgeryTokens.Header, token);
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // A request whose fields are refused gets them in its 422, in Invite user's words, never a 400 or a 500, and
    // changes, writes and sends nothing, for an Active worker whom the body is made for.
    private async Task AssertInvalidAsync(
        Func<AddedUser, Dictionary<string, string>> bodyFor, params (string Field, string Error)[] expected)
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);

        var problem = await AssertRefusedAsync(
            a.ManagerId, worker.Id, bodyFor(worker), await ETagAsync(worker.Id), HttpStatusCode.UnprocessableEntity,
            "validation.failed");

        AssertErrors(problem, expected);
    }

    // A refusal, with its code, if any, that changes the user in no way, writes no event about them and sends nothing.
    // It returns the problem, for a test that reads more of it.
    private async Task<JsonElement> AssertRefusedAsync(
        Guid callerId,
        Guid userId,
        Dictionary<string, string> body,
        string? ifMatch,
        HttpStatusCode status,
        string? code)
    {
        var before = await StoredAsync(userId);

        using var response = await PutAsync(app, callerId, userId, body, ifMatch);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        Assert.Equal(
            code,
            problem.RootElement.TryGetProperty(ErrorCode.Extension, out var answered) ? answered.GetString() : null);
        Assert.Equal(before, await StoredAsync(userId));
        Assert.Empty(await StoredEventsAboutAsync(userId));
        Assert.Empty(Sent(app));
        return problem.RootElement.Clone();
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private async Task<Guid> AddOrganisationAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Edit user test",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organisation.Id;
    }

    // In an organisation of its own: an Invited manager has not set up, a Deactivated one cannot sign in, and a worker
    // is no manager, so none of them counts.
    private async Task<AddedUser> AddTheOnlyActiveManagerAsync()
    {
        var organisationId = await AddOrganisationAsync();
        var manager = await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Active);
        await InviteManagerAsync(organisationId, manager.Id);
        await AddUserAsync(organisationId, UserRole.Manager, UserStatus.Deactivated);
        await AddUserAsync(organisationId, UserRole.Worker, UserStatus.Active);
        return manager;
    }

    // A user as setup leaves one (design.md §8.1), with an address that no other test or run uses: with a password, an
    // authenticator key and two-factor on, as its password path leaves them, and with that many passkeys besides, as
    // its passkey path leaves one each, with the key the invite made; and, when Deactivated, the time of it.
    private async Task<AddedUser> AddUserAsync(Guid organisationId, UserRole role, UserStatus status, int passkeys = 0)
    {
        var email = NewAddress();
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = Name,
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
        AssertSucceeded(await users.CreateAsync(user, PasswordSignIn.Password));
        AssertSucceeded(await users.ResetAuthenticatorKeyAsync(user));
        AssertSucceeded(await users.SetTwoFactorEnabledAsync(user, true));
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
            organisationId, "Alex Morgan", NewAddress(), UserRole.Manager, invitedByUserId);
        Assert.True(invite.Succeeded, string.Join(" ", invite.Errors.Select(error => error.Code)));
    }

    // As another manager's change would, through Identity, which gives the user a new concurrency stamp.
    private async Task RenameAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.DisplayName = "Sam Taylor-Smith";
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
        Assert.NotNull(user.Email);
        Assert.NotNull(user.UserName);
        Assert.NotNull(user.ConcurrencyStamp);
        return new(
            user.Email,
            user.UserName,
            user.EmailConfirmed,
            user.DisplayName,
            user.Role,
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
        string Email,
        string UserName,
        bool EmailConfirmed,
        string DisplayName,
        UserRole Role,
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
