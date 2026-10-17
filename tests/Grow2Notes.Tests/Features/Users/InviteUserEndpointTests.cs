using System.Globalization;
using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
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
/// <c>POST /api/admin/users</c> (design.md §4.11 <i>Invite user</i>, §6.6, §8.1 steps 1 and 2), called as the SPA
/// calls it, with an antiforgery token, as the seeded organisations' users through the test-only sign-in. Each test's
/// app records the emails it sends (<see cref="RecordingEmailSender"/>) and runs on a fake clock. An invite that
/// succeeds invites an address of its own, and one that is refused leaves every table as it was: the collection's
/// tests run one at a time, so the users and events are counted before and after. The endpoint matrix covers who may
/// call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class InviteUserEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/admin/users";

    private const string DisplayName = "Avery Brooks";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly Grow2NotesFactory factory;
    private readonly SeededOrganisations seeded;
    private readonly WebApplicationFactory<Program> app;

    public InviteUserEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        this.factory = factory;
        seeded = sqlServer.Seeded;
        app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
    }

    [Theory]
    [InlineData(Caller.ManagerOfA, "Worker")]
    [InlineData(Caller.ManagerOfB, "Manager")]
    public async Task A_manager_s_invite_creates_an_Invited_user_in_their_organisation_with_its_event_and_email(
        Caller caller, string role)
    {
        var organisation = caller == Caller.ManagerOfA ? seeded.A : seeded.B;
        var email = NewAddress();

        using var response = await PostAsync(app, caller, Body(DisplayName, email, role));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));

        // Read in a scope of its own, so both have committed.
        var user = await FindUserAsync(email);
        Assert.NotNull(user);
        var now = clock.GetUtcNow().UtcDateTime;
        Assert.Equal(
            new StoredUser(
                organisation.OrganisationId, DisplayName, Enum.Parse<UserRole>(role), UserStatus.Invited, now,
                organisation.ManagerId, ActivatedAtUtc: null, UserName: email, Email: email, EmailConfirmed: false,
                PasswordHash: null),
            StoredUser.Of(user));
        Assert.Equal(
            [
                new StoredEvent(
                    organisation.OrganisationId, now, organisation.ManagerId, AuditEventTypes.User.Invited,
                    AuditEntityTypes.User, user.Id, ParticipantId: null,
                    $$$"""{"new":{"email":"{{{email}}}","displayName":"{{{DisplayName}}}","role":"{{{role}}}"}}"""),
            ],
            await StoredEventsAboutAsync(user.Id));

        // The one email is the setup-link email to the address, whose link is the new user's and starts their setup.
        var sent = Assert.Single(Sent(app));
        var link = SetupLinks.FromEmail(sent);
        Assert.Equal(user.Id.ToString("D"), link.UserId);
        Assert.Equal(
            SetupLinkEmail.To(
                email, $"{Grow2NotesFactory.Origin}/setup#u={link.UserId}&t={Uri.EscapeDataString(link.Token)}"),
            sent);
        using var setupClient = app.CreateHttpsClient();
        await setupClient.StartSetupAsync(link);
    }

    // A27: one account per address in the whole app, compared whatever the letter case. B's seeded manager holds it.
    [Fact]
    public async Task An_address_another_organisation_s_user_holds_in_other_letter_case_is_409_with_nothing_done()
    {
        var heldByB = await StoredEmailAsync(seeded.B.ManagerId);
        var before = await TablesAsync();

        using var response = await PostAsync(
            app, Caller.ManagerOfA, Body(DisplayName, heldByB.ToUpperInvariant(), "Worker"));

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        Assert.Equal("user.email_in_use", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(seeded.B.ManagerId, (await FindUserAsync(heldByB))?.Id);
        await AssertNothingDoneAsync(before);
    }

    // design.md §8.1 step 1: an Invited user cannot sign in (Grow2NotesSignInManager) until setup makes them Active.
    [Fact]
    public async Task The_invited_person_cannot_sign_in()
    {
        var email = NewAddress();

        using var response = await PostAsync(app, Caller.ManagerOfA, Body(DisplayName, email, "Worker"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var invited = await signInManager.UserManager.FindByEmailAsync(email);
        Assert.NotNull(invited);
        Assert.False(await signInManager.CanSignInAsync(invited));
    }

    [Fact]
    public async Task A_worker_gets_a_403_and_nothing_is_done()
    {
        var before = await TablesAsync();

        using var response = await PostAsync(app, Caller.WorkerOfA, Body(DisplayName, NewAddress(), "Worker"));

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        await AssertNothingDoneAsync(before);
    }

    [Theory]
    [MemberData(nameof(InvalidUserDetails.Names), MemberType = typeof(InvalidUserDetails))]
    public Task A_missing_or_overlong_name_gets_a_422_for_displayName_and_nothing_is_done(
        string? displayName, string error) =>
        AssertInvalidAsync(Body(displayName, NewAddress(), "Worker"), ("displayName", error));

    [Theory]
    [MemberData(nameof(InvalidUserDetails.Emails), MemberType = typeof(InvalidUserDetails))]
    public Task A_missing_overlong_or_malformed_address_gets_a_422_for_email_and_nothing_is_done(
        string? email, string error) =>
        AssertInvalidAsync(Body(DisplayName, email, "Worker"), ("email", error));

    [Theory]
    [MemberData(nameof(InvalidUserDetails.Roles), MemberType = typeof(InvalidUserDetails))]
    public Task A_missing_role_or_one_that_is_neither_Worker_nor_Manager_gets_a_422_for_role_and_nothing_is_done(
        string? role) =>
        AssertInvalidAsync(Body(DisplayName, NewAddress(), role), ("role", UserDetailsChecks.RoleMissing));

    // So the screen can show every error at once.
    [Fact]
    public Task Every_field_at_fault_is_named_at_once() =>
        AssertInvalidAsync(
            Body(displayName: "", email: "", role: null),
            ("displayName", UserDetailsChecks.NameMissing),
            ("email", UserDetailsChecks.EmailMissing),
            ("role", UserDetailsChecks.RoleMissing));

    // The email goes before the commit, so a send that fails rolls back the user and their event (T01's Notes). The
    // sender's exception holds the address and the link, which the app keeps out of its logs (design.md §9.5).
    [Fact]
    public async Task A_sender_that_fails_gives_a_500_with_no_user_or_event_and_no_address_or_link_in_the_logs()
    {
        var logs = new RecordingLoggerProvider();
        await using var failing = factory.WithWebHostBuilder(builder => builder
            .UseClock(clock)
            .ConfigureLogging(logging => logging.AddProvider(logs))
            .ConfigureTestServices(FailingEmailSender.ReplaceAppSender));
        var email = NewAddress();
        var before = await TablesAsync();

        using var response = await PostAsync(failing, Caller.ManagerOfA, Body(DisplayName, email, "Worker"));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Null(await FindUserAsync(email));
        Assert.Equal(before, await TablesAsync());
        Assert.Contains(logs.Entries, entry => entry.Exception is EmailNotSentException);
        string[] secrets = [email, $"{Grow2NotesFactory.Origin}/setup"];
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

    // An address that no other test or run of this one uses (A35).
    private static string NewAddress() => $"avery.brooks.{Guid.NewGuid():N}@example.org";

    // The body as the SPA sends it, with a null value left out, as a field that is missing.
    private static Dictionary<string, string> Body(string? displayName, string? email, string? role) =>
        new (string Name, string? Value)[] { ("displayName", displayName), ("email", email), ("role", role) }
            .Where(field => field.Value is not null)
            .ToDictionary(field => field.Name, field => field.Value!);

    // A request whose fields are refused gets them in its 422, never a 400 or a 500, and changes nothing.
    private async Task AssertInvalidAsync(
        Dictionary<string, string> body, params (string Field, string Error)[] expected)
    {
        var before = await TablesAsync();

        using var response = await PostAsync(app, Caller.ManagerOfA, body);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            expected.Select(error => $"{error.Field}: {error.Error}"),
            problem.RootElement.GetProperty("errors").EnumerateObject().SelectMany(field =>
                field.Value.EnumerateArray().Select(message => $"{field.Name}: {message.GetString()}")));
        await AssertNothingDoneAsync(before);
    }

    // No user or event was added, and no email sent.
    private async Task AssertNothingDoneAsync(Tables before)
    {
        Assert.Equal(before, await TablesAsync());
        Assert.Empty(Sent(app));
    }

    private async Task<HttpResponseMessage> PostAsync(
        WebApplicationFactory<Program> host, Caller caller, Dictionary<string, string> body)
    {
        using var client = host.CreateHttpsClient().SignInAs(caller, seeded);
        var token = await client.FetchTokenAsync();
        return await client.PostWithTokenAsync(Path, token, body);
    }

    private static IReadOnlyCollection<EmailMessage> Sent(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<RecordingEmailSender>().Sent;

    private async Task<ApplicationUser?> FindUserAsync(string email)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    private async Task<string> StoredEmailAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = (await users.FindByIdAsync(userId.ToString()))?.Email;
        Assert.NotNull(email);
        return email;
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

    // How many users there are, and the ID of the last event written, by any test.
    private async Task<Tables> TablesAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<Tables>($"""
                SELECT (SELECT COUNT(*) FROM [AspNetUsers]) AS [Users],
                       (SELECT MAX([Id]) FROM [AuditEvent]) AS [LastEventId]
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Tables(int Users, long? LastEventId);

    private sealed record StoredEvent(
        Guid OrganisationId,
        DateTime OccurredAtUtc,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);

    private sealed record StoredUser(
        Guid OrganisationId,
        string DisplayName,
        UserRole Role,
        UserStatus Status,
        DateTime InvitedAtUtc,
        Guid? InvitedByUserId,
        DateTime? ActivatedAtUtc,
        string? UserName,
        string? Email,
        bool EmailConfirmed,
        string? PasswordHash)
    {
        public static StoredUser Of(ApplicationUser user) => new(
            user.OrganisationId, user.DisplayName, user.Role, user.Status, user.InvitedAtUtc, user.InvitedByUserId,
            user.ActivatedAtUtc, user.UserName, user.Email, user.EmailConfirmed, user.PasswordHash);
    }
}
