using System.Data.Common;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Email;
using Grow2Notes.Web.Platform.OperatorCommands;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform.OperatorCommands;

/// <summary>
/// <c>admin bootstrap</c> (design.md §7.4), run through the operator commands' entry as the app's binary runs it, with
/// writers in place of the console (<see cref="AdminCommandRuns"/>). Each run has made-up names and an address of its
/// own, which no other test or run of this one uses. A test that reads the emails sent, or that needs none sent, runs
/// on an app of its own, whose <see cref="RecordingEmailSender"/> starts empty. The rows are read afresh, in a scope
/// of their own, after each run.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class BootstrapCommandTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const string OrganisationName = "Riverside Supports";

    private const string ManagerName = "Sam Taylor";

    // Stands for an address of the run's own in RefusedArguments, whose cases are the same each time they are listed.
    private const string Address = "<address>";

    // Each with the whole of what it writes on the error writer: the usage line follows when an argument is at fault.
    public static TheoryData<string[], string> RefusedArguments { get; } = new()
    {
        // Refused by the entry, which then says how each command is used, one to a line.
        { [], RefusalWithEveryUsage("No command was given.") },
        { ["create-organisation"], RefusalWithEveryUsage("\"create-organisation\" is not a command.") },
        { ["bootstrap"], RefusalWithUsage("--organisation is missing.") },
        { Bootstrap(organisation: null), RefusalWithUsage("--organisation is missing.") },
        { Bootstrap(managerName: null), RefusalWithUsage("--manager-name is missing.") },
        { Bootstrap(managerEmail: null), RefusalWithUsage("--manager-email is missing.") },
        { Bootstrap(organisation: ""), RefusalWithUsage("--organisation is empty.") },
        { Bootstrap(managerName: "   "), RefusalWithUsage("--manager-name is empty.") },
        { Bootstrap(managerEmail: ""), RefusalWithUsage("--manager-email is empty.") },
        {
            Bootstrap(organisation: new string('R', Limits.OrganisationName + 1)),
            RefusalWithUsage("--organisation is longer than 200 characters.")
        },
        {
            Bootstrap(managerName: new string('S', Limits.DisplayName + 1)),
            RefusalWithUsage("--manager-name is longer than 100 characters.")
        },
        {
            Bootstrap(managerEmail: new string('s', Limits.Email + 1 - "@example.org".Length) + "@example.org"),
            RefusalWithUsage("--manager-email is longer than 256 characters.")
        },
        {
            [.. Bootstrap(), "--organization", "Hillside Supports"],
            RefusalWithUsage("\"--organization\" is not an option of admin bootstrap.")
        },
        // A name with a space, typed without quotes.
        {
            ["bootstrap", "--organisation", "Riverside", "Supports", "--manager-name", ManagerName, "--manager-email",
                Address],
            RefusalWithUsage("\"Supports\" is not an option of admin bootstrap.")
        },
        { [.. Bootstrap(), "--organisation"], RefusalWithUsage("--organisation has no value.") },
        {
            [.. Bootstrap(), "--organisation", "Hillside Supports"],
            RefusalWithUsage("--organisation is given more than once.")
        },
        // Refused by Identity, inside the transaction, after the organisation was saved; the arguments were not at
        // fault, so no usage line follows.
        { Bootstrap(managerEmail: "not-an-address"), Refusal("Identity refused the manager's account: InvalidEmail.") },
    };

    [Fact]
    public async Task One_run_creates_the_organisation_and_its_Invited_manager_writes_the_event_and_emails_the_link()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(TestClock.Start);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var email = NewAddress();

        var run = await app.RunAdminCommandAsync(Bootstrap(managerEmail: email));

        // Nothing is printed: the link is in the email alone.
        Assert.Equal((0, "", ""), (run.ExitCode, run.Output, run.Error));
        var manager = await FindUserAsync(email);
        var message = Assert.Single(Sent(app));
        Assert.Equal((email, SetupLinkEmail.Subject), (message.To, message.Subject));
        Assert.Contains(
            $"{Grow2NotesFactory.Origin}/setup#u={manager.Id:D}&t=", message.Text, StringComparison.Ordinal);
        Assert.True(await VerifySetupTokenAsync(app.Services, manager.Id, SetupLinks.FromEmail(message).Token));

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisationId = manager.OrganisationId;
        Assert.Equal(
            new StoredOrganisation(OrganisationName, TestClock.Start.UtcDateTime),
            await db.Organisations.Where(organisation => organisation.Id == organisationId)
                .Select(organisation => new StoredOrganisation(organisation.Name, organisation.CreatedAtUtc))
                .SingleAsync(cancellationToken));

        // The organisation's only user.
        Assert.Equal(
            new StoredUser(
                manager.Id, ManagerName, UserRole.Manager, UserStatus.Invited, TestClock.Start.UtcDateTime,
                InvitedByUserId: null, email),
            await db.Users.Where(user => user.OrganisationId == organisationId)
                .Select(user => new StoredUser(
                    user.Id, user.DisplayName, user.Role, user.Status, user.InvitedAtUtc, user.InvitedByUserId,
                    user.Email))
                .SingleAsync(cancellationToken));

        // Read in SQL, so that neither the tenant filter nor a tracked row decides what is seen.
        Assert.Equal(
            new StoredEvent(
                organisationId, TestClock.Start.UtcDateTime, ActorUserId: null, AuditEventTypes.Admin.Bootstrap,
                AuditEntityTypes.Organisation, organisationId, ParticipantId: null,
                $$"""{"userId":"{{manager.Id:D}}"}""", IpAddress: null),
            Assert.Single(await db.Database.SqlQuery<StoredEvent>($"""
                SELECT [OrganisationId], [OccurredAtUtc], [ActorUserId], [EventType], [EntityType], [EntityId],
                       [ParticipantId], [Details], [IpAddress]
                FROM [AuditEvent]
                WHERE [EntityId] = {organisationId}
                """).ToListAsync(cancellationToken)));
    }

    // Added with the organisation (A43), whose admin.bootstrap event covers it, so it has no event of its own; the
    // invite core writes none either.
    [Fact]
    public async Task One_run_gives_the_organisation_exactly_one_Every_note_group_and_writes_no_event_for_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(TestClock.Start);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var email = NewAddress();

        var run = await app.RunAdminCommandAsync(Bootstrap(managerEmail: email));

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        var organisationId = (await FindUserAsync(email)).OrganisationId;
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        // Read in SQL, so that neither the tenant filter nor a tracked row decides what is seen.
        Assert.Equal(
            new StoredGroup(
                "Every note", SortOrder: 0, IsEveryNote: true, ArchivedAtUtc: null, TestClock.Start.UtcDateTime),
            Assert.Single(await db.Database.SqlQuery<StoredGroup>($"""
                SELECT [Name], [SortOrder], [IsEveryNote], [ArchivedAtUtc], [CreatedAtUtc]
                FROM [CommonItemGroup]
                WHERE [OrganisationId] = {organisationId}
                """).ToListAsync(cancellationToken)));
        Assert.Equal(
            [AuditEventTypes.Admin.Bootstrap],
            await db.Database.SqlQuery<string>($"""
                SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [OrganisationId] = {organisationId}
                """).ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task A_second_run_with_the_address_in_another_letter_case_is_refused_and_changes_nothing()
    {
        await using var app = factory.WithWebHostBuilder(_ => { });
        var email = NewAddress();
        var first = await app.RunAdminCommandAsync(Bootstrap(managerEmail: email));
        Assert.Equal(0, first.ExitCode);
        var firstLink = SetupLinks.FromEmail(Assert.Single(Sent(app)));
        var managerId = (await FindUserAsync(email)).Id;
        var managerRows = await ReadUserAsync(managerId);
        var counts = await CountRowsAsync();

        var second = await app.RunAdminCommandAsync(
            Bootstrap("Hillside Supports", "Alex Morgan", managerEmail: email.ToUpperInvariant()));

        Assert.Equal((1, ""), (second.ExitCode, second.Output));
        Assert.Equal(
            Refusal("The address in --manager-email already has an account, and an address can have only one."),
            second.Error);

        // The common item groups among them: the second run saved its Every note group before the invite was refused,
        // in the transaction that the refusal rolled back, so it left none.
        Assert.Equal(counts, await CountRowsAsync());

        // Its security stamp among them, so the first run's link still works, and the second run sent no email.
        Assert.Equal(managerRows, await ReadUserAsync(managerId));
        Assert.Single(Sent(app));
        Assert.True(await VerifySetupTokenAsync(app.Services, managerId, firstLink.Token));
    }

    [Theory]
    [MemberData(nameof(RefusedArguments))]
    public async Task A_refused_command_exits_with_1_says_why_sends_no_email_and_writes_nothing(
        string[] arguments, string error)
    {
        await using var app = factory.WithWebHostBuilder(_ => { });
        var counts = await CountRowsAsync();

        var run = await app.RunAdminCommandAsync(
            [.. arguments.Select(argument => argument == Address ? NewAddress() : argument)]);

        Assert.Equal((1, "", error), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(counts, await CountRowsAsync());
        Assert.Empty(Sent(app));
    }

    // An address without its scheme is not absolute, and the invite core could make no link from it.
    [Theory]
    [InlineData("", "App:Origin is not set")]
    [InlineData("grow2notes.example", "App:Origin is not an absolute address")]
    public async Task Without_an_absolute_App_Origin_it_exits_with_1_says_why_sends_no_email_and_writes_nothing(
        string origin, string reason)
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection([new("App:Origin", origin)])));
        var counts = await CountRowsAsync();

        var run = await app.RunAdminCommandAsync(Bootstrap(managerEmail: NewAddress()));

        Assert.Equal(
            (1, "", Refusal($"{reason}, so no setup link can be made.")), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(counts, await CountRowsAsync());
        Assert.Empty(Sent(app));
    }

    // The event is written inside the transaction, so a failure after it rolls back the organisation and the invite
    // with it, and the address is free for the run to be made again. The failure comes before the email, so none is
    // sent.
    [Fact]
    public async Task When_the_work_fails_after_the_event_is_written_nothing_is_kept_sent_or_printed()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IAuditWriter>(
                provider => new FailsAfterWriting(ActivatorUtilities.CreateInstance<AuditWriter>(provider)))));
        var counts = await CountRowsAsync();
        var email = NewAddress();
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Assert.ThrowsAsync<WorkFailedException>(
            () => app.RunAdminCommandAsync(Bootstrap(managerEmail: email), output, error));

        Assert.Equal(("", ""), (output.ToString(), error.ToString()));
        Assert.Equal(counts, await CountRowsAsync());
        Assert.Null(await FindUserOrNullAsync(email));
        Assert.Empty(Sent(app));
    }

    // The email goes before the commit, so a send that fails rolls back the organisation, its group, the manager and
    // the event (T01's Notes), and the address is free for the run to be made again.
    [Fact]
    public async Task When_the_email_is_not_sent_it_exits_with_1_says_why_and_writes_nothing()
    {
        await using var failing = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(FailingEmailSender.ReplaceAppSender));
        var counts = await CountRowsAsync();
        var email = NewAddress();

        var run = await failing.RunAdminCommandAsync(Bootstrap(managerEmail: email));

        Assert.Equal(
            (1, "", Refusal("The setup-link email could not be sent.")), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(counts, await CountRowsAsync());
        Assert.Null(await FindUserOrNullAsync(email));

        await using var working = factory.WithWebHostBuilder(_ => { });
        var again = await working.RunAdminCommandAsync(Bootstrap(managerEmail: email));
        Assert.Equal((0, "", ""), (again.ExitCode, again.Output, again.Error));
        Assert.Equal(email, Assert.Single(Sent(working)).To);
    }

    // A transient failure at the commit runs the whole work again, after the first email was sent. The first attempt's
    // organisation and manager were rolled back, so its link answers 410 and the second attempt's works (T01's Notes).
    [Fact]
    public async Task When_the_commit_fails_transiently_one_organisation_is_added_and_the_last_link_emailed_works()
    {
        var commits = new FirstCommitFails();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(commits))));
        var counts = await CountRowsAsync();
        var email = NewAddress();

        var run = await app.RunAdminCommandAsync(Bootstrap(managerEmail: email));

        Assert.Equal((0, "", ""), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(2, commits.Attempts);
        Assert.Equal(
            new RowCounts(
                counts.Organisations + 1, counts.CommonItemGroups + 1, counts.Users + 1, counts.AuditEvents + 1),
            await CountRowsAsync());
        var manager = await FindUserAsync(email);
        var sent = Sent(app).ToList();
        Assert.Equal([email, email], sent.Select(message => message.To));
        var (first, last) = (SetupLinks.FromEmail(sent[0]), SetupLinks.FromEmail(sent[1]));
        Assert.Equal(manager.Id.ToString("D"), last.UserId);
        Assert.True(await VerifySetupTokenAsync(app.Services, manager.Id, last.Token));
        using var client = app.CreateHttpsClient();
        await client.AssertSetupLinkEndedAsync(first);
    }

    [Fact]
    public async Task Names_and_an_address_at_their_longest_are_stored_whole()
    {
        var organisationName = OrganisationName.PadRight(Limits.OrganisationName, 's');
        var managerName = ManagerName.PadRight(Limits.DisplayName, 'r');
        var email = $"sam.taylor.{Guid.NewGuid():N}".PadRight(Limits.Email - "@example.org".Length, 'r') +
            "@example.org";

        var run = await factory.RunAdminCommandAsync(Bootstrap(organisationName, managerName, email));

        Assert.Equal(0, run.ExitCode);
        var manager = await FindUserAsync(email);
        Assert.Equal((managerName, email), (manager.DisplayName, manager.Email));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(
            organisationName,
            await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
                .Where(organisation => organisation.Id == manager.OrganisationId)
                .Select(organisation => organisation.Name)
                .SingleAsync(TestContext.Current.CancellationToken));
    }

    // The link holds the setup token, and the names and the address are personal (design.md §9.5). The token is looked
    // for as the link holds it too, escaped, since its base64 almost always has a +, / or = in it, which the link has
    // as %XX. A send that fails throws an exception whose message holds the address and the link, which must not reach
    // the logs either.
    [Fact]
    public async Task Neither_a_run_nor_a_refused_or_failed_one_logs_the_names_the_address_or_the_link()
    {
        var logs = new RecordingLoggerProvider();
        await using var app = factory.WithWebHostBuilder(
            builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        await using var failing = app.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(FailingEmailSender.ReplaceAppSender));
        var email = NewAddress();
        var failedEmail = NewAddress();

        var done = await app.RunAdminCommandAsync(Bootstrap(managerEmail: email));
        var refused = await app.RunAdminCommandAsync(Bootstrap(managerEmail: email));
        var failed = await failing.RunAdminCommandAsync(Bootstrap(managerEmail: failedEmail));

        Assert.Equal((0, 1, 1), (done.ExitCode, refused.ExitCode, failed.ExitCode));
        var token = SetupLinks.FromEmail(Assert.Single(Sent(app))).Token;
        string[] personal =
        [
            OrganisationName, ManagerName, email, failedEmail, $"{Grow2NotesFactory.Origin}/setup", token,
            Uri.EscapeDataString(token),
        ];
        Assert.DoesNotContain(logs.Entries, entry => personal.Any(entry.Holds));
    }

    // admin bootstrap's arguments, with each option whose value is not null.
    private static string[] Bootstrap(
        string? organisation = OrganisationName, string? managerName = ManagerName, string? managerEmail = Address) =>
    [
        "bootstrap",
        .. Option("--organisation", organisation),
        .. Option("--manager-name", managerName),
        .. Option("--manager-email", managerEmail),
    ];

    private static string[] Option(string name, string? value) => value is null ? [] : [name, value];

    // What a refusal writes on the error writer, with the usage line or without it.
    private static string Refusal(string reason) => $"Refused: {reason} Nothing was written.{Environment.NewLine}";

    private static string RefusalWithUsage(string reason) =>
        $"{Refusal(reason)}{BootstrapCommand.Usage}{Environment.NewLine}";

    private static string RefusalWithEveryUsage(string reason) =>
        $"{RefusalWithUsage(reason)}{ResetSignInCommand.Usage}{Environment.NewLine}" +
        $"{SignOutAllCommand.Usage}{Environment.NewLine}{McpClientCommand.Usage}{Environment.NewLine}";

    private static IReadOnlyCollection<EmailMessage> Sent(WebApplicationFactory<Program> host) =>
        host.Services.GetRequiredService<RecordingEmailSender>().Sent;

    // An address that no other test or run of this one uses.
    private static string NewAddress() => $"sam.taylor.{Guid.NewGuid():N}@example.org";

    private async Task<ApplicationUser> FindUserAsync(string email)
    {
        var user = await FindUserOrNullAsync(email);
        Assert.NotNull(user);
        return user;
    }

    private async Task<ApplicationUser?> FindUserOrNullAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    // Every column of the user's row, and of their rows in AspNetUserTokens, which hold the authenticator key, as SQL
    // Server writes them in JSON.
    private async Task<string> ReadUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return Assert.Single(await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"""
                SELECT (SELECT [User].*,
                               (SELECT [Token].* FROM [AspNetUserTokens] AS [Token]
                                WHERE [Token].[UserId] = [User].[Id]
                                ORDER BY [Token].[LoginProvider], [Token].[Name]
                                FOR JSON PATH) AS [Tokens]
                        FROM [AspNetUsers] AS [User]
                        WHERE [User].[Id] = {userId}
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER) AS [Value]
                """).ToListAsync(TestContext.Current.CancellationToken));
    }

    // Every organisation, common item group, user and audit event in the shared database. The tests of the collection
    // run one at a time, so only the run between two counts can change them.
    private async Task<RowCounts> CountRowsAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        // The tenant-owned tables in SQL, since the tenant filter would count one organisation's rows only.
        return new(
            await db.Organisations.CountAsync(cancellationToken),
            Assert.Single(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [CommonItemGroup]")
                .ToListAsync(cancellationToken)),
            await db.Users.CountAsync(cancellationToken),
            Assert.Single(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [AuditEvent]")
                .ToListAsync(cancellationToken)));
    }

    private static async Task<bool> VerifySetupTokenAsync(IServiceProvider services, Guid userId, string token)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return await users.VerifyUserTokenAsync(
            user, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, token);
    }

    private sealed record RowCounts(int Organisations, int CommonItemGroups, int Users, int AuditEvents);

    private sealed record StoredOrganisation(string Name, DateTime CreatedAtUtc);

    private sealed record StoredGroup(
        string Name, int SortOrder, bool IsEveryNote, DateTime? ArchivedAtUtc, DateTime CreatedAtUtc);

    private sealed record StoredUser(
        Guid Id,
        string DisplayName,
        UserRole Role,
        UserStatus Status,
        DateTime InvitedAtUtc,
        Guid? InvitedByUserId,
        string? Email);

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

    // The app's writer, after which the work fails, as though something after the event had.
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
            throw new WorkFailedException();
        }
    }

    // A type only this test throws, so no exception from EF Core or Identity can satisfy the assertion by accident.
    private sealed class WorkFailedException() : Exception("The work failed after the event was written.");

    // Fails the first commit before it reaches the server, with an exception that the SQL Server strategy treats as
    // transient, as TransactionsAndRetriesTests shows, so the strategy runs the work again.
    private sealed class FirstCommitFails : DbTransactionInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts == 1)
            {
                throw new TimeoutException();
            }

            return ValueTask.FromResult(result);
        }
    }
}
