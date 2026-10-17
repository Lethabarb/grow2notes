using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Email;
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
/// <c>admin reset-signin</c> (design.md §7.4, §8.6 <i>Break-glass</i>, A26), run through the operator commands' entry
/// as the app's binary runs it, with writers in place of the console (<see cref="AdminCommandRuns"/>). Each test adds
/// the users it resets, with their ways to sign in, as setup leaves them, or invites them through the invite core
/// (<see cref="SetupLinks.InviteAsync"/>), which writes no audit event; a test of the last manager adds an organisation
/// of its own too, so that it alone decides who is an Active manager there. Each test's app records the emails it sends
/// (<see cref="RecordingEmailSender"/>), runs on a fake clock, and signs in with the session cookie where a test needs
/// it (<see cref="SignedInDevices"/>).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ResetSignInCommandTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    // Stand for addresses of the run's own in RefusedArguments, whose cases are the same each time they are listed: an
    // Active worker's and a Deactivated worker's, whom the run adds, and one that no account has.
    private const string ActiveAddress = "<active>";

    private const string DeactivatedAddress = "<deactivated>";

    private const string NoAccountAddress = "<no account>";

    private readonly FakeTimeProvider clock = new(TestClock.Start);
    private readonly SeededOrganisation a;
    private readonly WebApplicationFactory<Program> app;

    public ResetSignInCommandTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        a = sqlServer.Seeded.A;
        app = factory.WithWebHostBuilder(builder => builder.MapCookieSignIn().UseClock(clock));
    }

    // Each with the whole of what it writes on the error writer: the usage line follows when an argument is at fault.
    public static TheoryData<string[], string> RefusedArguments { get; } = new()
    {
        { ["reset-signin"], RefusalWithUsage("No address was given.") },
        { ["reset-signin", ""], RefusalWithUsage("The address is empty.") },
        { ["reset-signin", "   "], RefusalWithUsage("The address is empty.") },
        {
            ["reset-signin", ActiveAddress, NoAccountAddress],
            RefusalWithUsage("More than one argument was given, but admin reset-signin takes one address.")
        },
        // An address typed with a space in it, without quotes, so that it reaches the command as two arguments.
        {
            ["reset-signin", "sam", "taylor@example.org"],
            RefusalWithUsage("More than one argument was given, but admin reset-signin takes one address.")
        },
        { ["reset-signin", NoAccountAddress], Refusal("No account has that address.") },
        {
            ["reset-signin", DeactivatedAddress],
            Refusal("The account is Deactivated, so a manager reactivates it in the app first.")
        },
    };

    // S00.03.06's first criterion, for the organisation's only Active manager, whom the app's Reset sign-in refuses
    // with 409 user.last_manager (A26). The dates of the invite and activation, the confirmed address and the lockout
    // stay as they were, as a manager's reset leaves them (SignInResetTests).
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    public async Task For_the_only_active_manager_it_resets_their_sign_in_prints_only_a_link_and_ends_their_sessions(
        bool passwordAndCode, int passkeys)
    {
        var organisationId = await AddOrganisationAsync();
        var manager = await AddUserAsync(
            organisationId, UserRole.Manager, UserStatus.Active, passwordAndCode, passkeys);
        using var devices = await SignedInDevices.SignInAsync(app, clock, manager.Id);
        var before = await StoredAsync(manager.Id);
        Assert.Equal(passwordAndCode, before is { PasswordHash: not null, TwoFactorEnabled: true });
        Assert.Equal(passkeys, before.Passkeys);

        var run = await app.RunAdminCommandAsync(["reset-signin", manager.Email]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        var setupLink = run.SingleOutputLine();
        Assert.StartsWith(
            $"{Grow2NotesFactory.Origin}/setup#u={manager.Id:D}&t=", setupLink, StringComparison.Ordinal);

        var after = await StoredAsync(manager.Id);
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

        // By the user's ID alone, with no actor, no details and no client address (A28).
        Assert.Equal(
            [
                new StoredEvent(
                    organisationId, clock.GetUtcNow().UtcDateTime, ActorUserId: null,
                    AuditEventTypes.Admin.SignInReset, AuditEntityTypes.User, manager.Id, ParticipantId: null,
                    Details: null, IpAddress: null),
            ],
            await StoredEventsAboutAsync(manager.Id));

        // The operator gives the person the printed link, so no email is sent.
        Assert.Empty(Sent(app));
        await devices.AssertNextRequestsAsync(
            HttpStatusCode.Unauthorized, SignedInDevices.Me, SignedInDevices.Ping);
        using var client = app.CreateHttpsClient();
        await client.StartSetupAsync(SetupLinks.Read(setupLink, manager.Email));
    }

    // The case of a lost bootstrap link (ops/break-glass.md): the new key ends the link printed before.
    [Fact]
    public async Task For_an_Invited_user_it_prints_a_new_link_and_the_old_one_gets_410()
    {
        var oldLink = await app.InviteAsync(a);
        var userId = Guid.Parse(oldLink.UserId);

        var run = await app.RunAdminCommandAsync(["reset-signin", oldLink.Email]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        var setupLink = run.SingleOutputLine();
        Assert.StartsWith($"{Grow2NotesFactory.Origin}/setup#u={userId:D}&t=", setupLink, StringComparison.Ordinal);
        Assert.Equal(UserStatus.Invited, (await StoredAsync(userId)).Status);
        var written = Assert.Single(await StoredEventsAboutAsync(userId));
        Assert.Equal((null, AuditEventTypes.Admin.SignInReset), (written.ActorUserId, written.EventType));
        Assert.Empty(Sent(app));

        using var client = app.CreateHttpsClient();
        await client.AssertSetupLinkEndedAsync(oldLink);
        await client.StartSetupAsync(SetupLinks.Read(setupLink, oldLink.Email));
    }

    // Identity finds an account by its normalised address, as sign-in does (A27).
    [Fact]
    public async Task The_address_in_another_letter_case_finds_the_user()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);

        var run = await app.RunAdminCommandAsync(["reset-signin", worker.Email.ToUpperInvariant()]);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.StartsWith(
            $"{Grow2NotesFactory.Origin}/setup#u={worker.Id:D}&t=", run.SingleOutputLine(), StringComparison.Ordinal);
        Assert.Equal(UserStatus.Invited, (await StoredAsync(worker.Id)).Status);
        Assert.Equal(
            AuditEventTypes.Admin.SignInReset, Assert.Single(await StoredEventsAboutAsync(worker.Id)).EventType);
    }

    [Theory]
    [MemberData(nameof(RefusedArguments))]
    public async Task A_refused_run_exits_with_1_says_why_and_writes_nothing(string[] arguments, string error)
    {
        var active = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var deactivated = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Deactivated, passkeys: 1);
        var before = (await ReadUserAsync(active.Id), await ReadUserAsync(deactivated.Id));
        var counts = await CountRowsAsync();
        string[] given =
        [
            .. arguments.Select(argument => argument switch
            {
                ActiveAddress => active.Email,
                DeactivatedAddress => deactivated.Email,
                NoAccountAddress => NewAddress(),
                _ => argument,
            }),
        ];

        var run = await app.RunAdminCommandAsync(given);

        Assert.Equal((1, "", error), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(counts, await CountRowsAsync());
        Assert.Equal(before, (await ReadUserAsync(active.Id), await ReadUserAsync(deactivated.Id)));
        Assert.Empty(Sent(app));
    }

    // Refused as admin bootstrap refuses it, before anything is changed.
    [Theory]
    [InlineData("", "App:Origin is not set")]
    [InlineData("grow2notes.example", "App:Origin is not an absolute address")]
    public async Task Without_an_absolute_App_Origin_it_exits_with_1_says_why_and_writes_nothing(
        string origin, string reason)
    {
        await using var withOrigin = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection([new("App:Origin", origin)])));
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var before = await ReadUserAsync(worker.Id);
        var counts = await CountRowsAsync();

        var run = await withOrigin.RunAdminCommandAsync(["reset-signin", worker.Email]);

        Assert.Equal(
            (1, "", Refusal($"{reason}, so no setup link can be made.")), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(counts, await CountRowsAsync());
        Assert.Equal(before, await ReadUserAsync(worker.Id));
    }

    // A manager changes the worker in the app while the command's first save is held, after it loaded them: that save
    // matches no row, as for two resets at once in the app, so the run is refused, the manager's change stands, and a
    // run made again reads the worker as they are now.
    [Fact]
    public async Task When_the_app_changes_the_user_while_it_runs_it_is_refused_and_writes_nothing()
    {
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var gate = new HoldsTheFirstSaveOfTheUser(worker.Id);
        await using var gated = gate.On(app);

        var running = gated.RunAdminCommandAsync(["reset-signin", worker.Email]);
        string changed;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await RenameAsync(worker.Id);
            changed = await ReadUserAsync(worker.Id);
        }
        finally
        {
            gate.Release();
        }

        var run = await running;
        Assert.Equal(
            (1, "", Refusal("The account changed while the command ran, so run it again.")),
            (run.ExitCode, run.Output, run.Error));
        Assert.Equal(changed, await ReadUserAsync(worker.Id));
        Assert.Empty(await StoredEventsAboutAsync(worker.Id));
    }

    // The link holds the setup token, and the address is personal (design.md §9.5). The token is looked for as the link
    // holds it too, escaped, since its base64 almost always has a +, / or = in it, which the link has as %XX.
    [Fact]
    public async Task Neither_a_run_nor_a_refused_one_logs_the_address_or_the_link()
    {
        var logs = new RecordingLoggerProvider();
        await using var logged = app.WithWebHostBuilder(
            builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        var worker = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Active);
        var deactivated = await AddUserAsync(a.OrganisationId, UserRole.Worker, UserStatus.Deactivated);
        var noAccount = NewAddress();

        var done = await logged.RunAdminCommandAsync(["reset-signin", worker.Email]);
        var refusedAsDeactivated = await logged.RunAdminCommandAsync(["reset-signin", deactivated.Email]);
        var refusedAsNoAccount = await logged.RunAdminCommandAsync(["reset-signin", noAccount]);

        Assert.Equal((0, 1, 1), (done.ExitCode, refusedAsDeactivated.ExitCode, refusedAsNoAccount.ExitCode));
        var setupLink = done.SingleOutputLine();
        var token = SetupLinks.Read(setupLink, worker.Email).Token;
        string[] personal =
            [worker.Email, deactivated.Email, noAccount, setupLink, token, Uri.EscapeDataString(token)];
        Assert.DoesNotContain(logs.Entries, entry => personal.Any(entry.Holds));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static IReadOnlyCollection<EmailMessage> Sent(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<RecordingEmailSender>().Sent;

    // What a refusal writes on the error writer, with the usage line or without it.
    private static string Refusal(string reason) => $"Refused: {reason} Nothing was written.{Environment.NewLine}";

    private static string RefusalWithUsage(string reason) =>
        $"{Refusal(reason)}{ResetSignInCommand.Usage}{Environment.NewLine}";

    // An address that no other test or run of this one uses.
    private static string NewAddress() => $"{Guid.NewGuid():N}@example.org";

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private async Task<Guid> AddOrganisationAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation
        {
            Name = "Break-glass reset test",
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organisation.Id;
    }

    // A user as setup leaves one (design.md §8.1), with an address that no other test or run uses: with a password, an
    // authenticator key and two-factor on, as its password path leaves them, unless passwordAndCode is false, and with
    // that many passkeys besides, as its passkey path leaves one each, with the key the invite made; and, when
    // Deactivated, the time of it, as a deactivation leaves them.
    private async Task<AddedUser> AddUserAsync(
        Guid organisationId, UserRole role, UserStatus status, bool passwordAndCode = true, int passkeys = 0)
    {
        var email = NewAddress();
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

        return new(user.Id, email);
    }

    // As a manager's change in the app would, through Identity, which gives the user a new concurrency stamp. It makes
    // its save with no transaction open, so the held save's interceptor lets it pass.
    private async Task RenameAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.DisplayName = "Sam Taylor-Jones";
        AssertSucceeded(await users.UpdateAsync(user));
    }

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

    // Every column of the user's row, and of their rows in AspNetUserTokens and AspNetUserPasskeys, which hold their
    // authenticator key and passkeys, as SQL Server writes them in JSON.
    private async Task<string> ReadUserAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return Assert.Single(await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT (SELECT [User].*,
                               (SELECT [Token].* FROM [AspNetUserTokens] AS [Token]
                                WHERE [Token].[UserId] = [User].[Id]
                                ORDER BY [Token].[LoginProvider], [Token].[Name]
                                FOR JSON PATH) AS [Tokens],
                               (SELECT [Passkey].* FROM [AspNetUserPasskeys] AS [Passkey]
                                WHERE [Passkey].[UserId] = [User].[Id]
                                ORDER BY [Passkey].[CredentialId]
                                FOR JSON PATH) AS [Passkeys]
                        FROM [AspNetUsers] AS [User]
                        WHERE [User].[Id] = {userId}
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS [Value]
                """).ToListAsync(TestContext.Current.CancellationToken));
    }

    // Every user and audit event in the shared database. The tests of the collection run one at a time, so only the run
    // between two counts can change them.
    private async Task<RowCounts> CountRowsAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return new(
            await db.Users.CountAsync(cancellationToken),
            // In SQL, since the tenant filter would count one organisation's events only.
            Assert.Single(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [AuditEvent]")
                .ToListAsync(cancellationToken)));
    }

    // Read in SQL, so that neither the tenant filter nor a tracked row decides what is seen, in the order written.
    private async Task<List<StoredEvent>> StoredEventsAboutAsync(Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                       [ParticipantId], [Details], [IpAddress]
                FROM [AuditEvent]
                WHERE [EntityId] = {userId}
                ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record RowCounts(int Users, int AuditEvents);

    private sealed record AddedUser(Guid Id, string Email);

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
        string? Details,
        string? IpAddress);
}
