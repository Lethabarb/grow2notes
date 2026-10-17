using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.OperatorCommands;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform.OperatorCommands;

/// <summary>
/// <c>admin signout-all</c> (design.md §7.4, §9.10 <i>Contain</i>), run through the operator commands' entry as the
/// app's binary runs it, with writers in place of the console (<see cref="AdminCommandRuns"/>). It gives every user in
/// the database a new security stamp, the seeded users' too, on which other tests rely (tests/README.md), so each test
/// runs it on a migrated database of its own, which holds only the organisations and users the test adds, and on a fake
/// clock, signing in with the session cookie (<see cref="SignedInDevices"/>).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SignOutAllCommandTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private readonly FakeTimeProvider clock = new(TestClock.Start);

    // S00.03.06's second criterion: every session of every user, in every organisation, gets 401 on its next request,
    // whatever it asks; an Invited user's link stops working, as it holds the stamp; and no way of signing in is taken
    // away, so a person with a password signs in with it and a code again.
    [Fact]
    public async Task Every_session_gets_401_next_and_each_organisation_gets_one_admin_signout_all_with_no_actor()
    {
        await using var app = await OnDatabaseOfItsOwnAsync("SignOutAllEndsEverySession");
        var a = await AddOrganisationAsync(app);
        var b = await AddOrganisationAsync(app);
        var managerOfA = await AddUserAsync(app, a, UserRole.Manager);
        var workerOfA = await AddUserAsync(app, a, UserRole.Worker);
        var managerOfB = await AddUserAsync(app, b, UserRole.Manager);
        var invited = await InviteAsync(app, a, managerOfA.Id);
        using var devicesOfManagerOfA = await SignedInDevices.SignInAsync(app, clock, managerOfA.Id);
        using var devicesOfWorkerOfA = await SignedInDevices.SignInAsync(app, clock, workerOfA.Id);
        using var devicesOfManagerOfB = await SignedInDevices.SignInAsync(app, clock, managerOfB.Id);
        Guid[] everyone = [managerOfA.Id, workerOfA.Id, managerOfB.Id, Guid.Parse(invited.UserId)];
        var stampsBefore = await SecurityStampsAsync(app, everyone);
        var ranAtUtc = clock.GetUtcNow().UtcDateTime;

        var run = await app.RunAdminCommandAsync(["signout-all"]);

        Assert.Equal((0, "", ""), (run.ExitCode, run.Output, run.Error));
        foreach (var devices in new[] { devicesOfManagerOfA, devicesOfWorkerOfA, devicesOfManagerOfB })
        {
            await devices.AssertNextRequestsAsync(
                HttpStatusCode.Unauthorized, SignedInDevices.Me, SignedInDevices.Ping);
        }

        var stampsAfter = await SecurityStampsAsync(app, everyone);
        Assert.All(everyone, userId => Assert.NotEqual(stampsBefore[userId], stampsAfter[userId]));

        // The database is the test's own, so these are all the events of their type, by the organisation's ID alone.
        Assert.Equal(
            new[] { a, b }.Order().Select(organisationId => new StoredEvent(
                organisationId, ranAtUtc, ActorUserId: null, AuditEventTypes.Admin.SignOutAll,
                AuditEntityTypes.Organisation, organisationId, ParticipantId: null, Details: null, IpAddress: null)),
            await StoredEventsAsync(app, AuditEventTypes.Admin.SignOutAll));
        Assert.Empty(app.Services.GetRequiredService<RecordingEmailSender>().Sent);

        using var client = app.CreateHttpsClient();
        await client.AssertSetupLinkEndedAsync(invited);
        using var signIn = await client.SignInWithPasswordAsync(
            workerOfA.Email, PasswordSignIn.Password, AuthenticatorCodes.Now(workerOfA.AuthenticatorKey));
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task An_option_is_refused_with_exit_code_1_and_the_usage_line_and_nothing_is_written()
    {
        await using var app = await OnDatabaseOfItsOwnAsync("SignOutAllRefused");
        var organisationId = await AddOrganisationAsync(app);
        var manager = await AddUserAsync(app, organisationId, UserRole.Manager);
        var stampBefore = await SecurityStampsAsync(app, [manager.Id]);

        var run = await app.RunAdminCommandAsync(["signout-all", "--organisation", "Riverside Supports"]);

        Assert.Equal(
            (1, "",
                $"Refused: \"--organisation\" is not an option of admin signout-all. Nothing was written." +
                $"{Environment.NewLine}{SignOutAllCommand.Usage}{Environment.NewLine}"),
            (run.ExitCode, run.Output, run.Error));
        Assert.Equal(stampBefore, await SecurityStampsAsync(app, [manager.Id]));
        Assert.Empty(await StoredEventsAsync(app, AuditEventTypes.Admin.SignOutAll));
    }

    // The addresses are personal, and an unused link holds its setup token (design.md §9.5), escaped, as its base64
    // almost always has a +, / or = in it, which the link has as %XX.
    [Fact]
    public async Task It_logs_no_address_or_link()
    {
        var logs = new RecordingLoggerProvider();
        await using var database = await OnDatabaseOfItsOwnAsync("SignOutAllLogsNoAddress");
        await using var app = database.WithWebHostBuilder(
            builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        var organisationId = await AddOrganisationAsync(app);
        var manager = await AddUserAsync(app, organisationId, UserRole.Manager);
        var invited = await InviteAsync(app, organisationId, manager.Id);

        var run = await app.RunAdminCommandAsync(["signout-all"]);

        Assert.Equal(0, run.ExitCode);
        string[] personal = [manager.Email, invited.Email, invited.Token, Uri.EscapeDataString(invited.Token)];
        Assert.DoesNotContain(logs.Entries, entry => personal.Any(entry.Holds));
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private async Task<Guid> AddOrganisationAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Sign-out of everyone test",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organisation.Id;
    }

    // An Active user, as setup's password path leaves one (design.md §8.1), with an address of their own: with a
    // password, an authenticator key and two-factor on.
    private async Task<AddedUser> AddUserAsync(WebApplicationFactory<Program> app, Guid organisationId, UserRole role)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = "Sam Taylor",
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = now,
            ActivatedAtUtc = now,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        AssertSucceeded(await users.CreateAsync(user, PasswordSignIn.Password));
        AssertSucceeded(await users.ResetAuthenticatorKeyAsync(user));
        AssertSucceeded(await users.SetTwoFactorEnabledAsync(user, true));
        var key = await users.GetAuthenticatorKeyAsync(user);
        Assert.NotNull(key);
        return new(user.Id, email, key);
    }

    // Through the invite core, so Invited, with a setup link not yet used.
    private static async Task<SetupLink> InviteAsync(
        WebApplicationFactory<Program> app, Guid organisationId, Guid invitedByUserId)
    {
        var email = $"{Guid.NewGuid():N}@example.org";
        await using var scope = app.Services.CreateAsyncScope();
        var invite = await scope.ServiceProvider.GetRequiredService<Invitations>().InviteAsync(
            organisationId, "Alex Morgan", email, UserRole.Worker, invitedByUserId);
        Assert.True(invite.Succeeded, string.Join(" ", invite.Errors.Select(error => error.Code)));
        return SetupLinks.Read(invite.SetupLink, email);
    }

    // A migrated database of the test's own, with nothing seeded, and the app on it with the session cookie's test-only
    // sign-in, on the test's clock.
    private async Task<WebApplicationFactory<Program>> OnDatabaseOfItsOwnAsync(string name)
    {
        var connectionString = await sqlServer.CreateDatabaseAsync(name, TestContext.Current.CancellationToken);
        return factory.WithWebHostBuilder(builder => builder
            .MapCookieSignIn()
            .UseClock(clock)
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                [new("ConnectionStrings:Grow2Notes", connectionString)])));
    }

    // In a scope of its own, so it reads what has committed.
    private static async Task<Dictionary<Guid, string?>> SecurityStampsAsync(
        WebApplicationFactory<Program> app, Guid[] userIds)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.SecurityStamp, TestContext.Current.CancellationToken);
    }

    // Read in SQL, so that neither the tenant filter nor a tracked row decides what is seen, then put in the order of
    // their organisations' IDs as .NET compares them, which is not SQL Server's order.
    private static async Task<List<StoredEvent>> StoredEventsAsync(
        WebApplicationFactory<Program> app, string eventType)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                       [ParticipantId], [Details], [IpAddress]
                FROM [AuditEvent]
                WHERE [EventType] = {eventType}
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
        return [.. stored.OrderBy(written => written.OrganisationId)];
    }

    private sealed record AddedUser(Guid Id, string Email, string AuthenticatorKey);

    private sealed record StoredEvent(
        Guid OrganisationId,
        DateTime OccurredAtUtc,
        Guid? ActorUserId,
        string EventType,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details,
        string? IpAddress);
}
