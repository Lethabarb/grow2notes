using System.Data.Common;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.OperatorCommands;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform.OperatorCommands;

/// <summary>
/// <c>admin bootstrap</c> (design.md §7.4), run through the operator commands' entry in a scope of its own, as the
/// app's binary runs it, with writers in place of the console. Each run has made-up names and an address of its own,
/// which no other test or run of this one uses. The rows are read afresh, in a scope of their own, after each run.
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
        { [], RefusalWithUsage("No command was given.") },
        { ["create-organisation"], RefusalWithUsage("\"create-organisation\" is not a command.") },
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
    public async Task One_run_creates_the_organisation_and_its_Invited_manager_writes_the_event_and_prints_the_link()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(TestClock.Start);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var email = NewAddress();

        var run = await RunAsync(app.Services, Bootstrap(managerEmail: email));

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        var manager = await FindUserAsync(email);
        var setupLink = ReadOnlyLine(run.Output);
        Assert.StartsWith($"{Grow2NotesFactory.Origin}/setup#u={manager.Id:D}&t=", setupLink, StringComparison.Ordinal);
        Assert.True(await VerifySetupTokenAsync(app.Services, manager.Id, TokenIn(setupLink)));

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

    [Fact]
    public async Task A_second_run_with_the_address_in_another_letter_case_is_refused_and_changes_nothing()
    {
        var email = NewAddress();
        var first = await RunAsync(factory.Services, Bootstrap(managerEmail: email));
        Assert.Equal(0, first.ExitCode);
        var managerId = (await FindUserAsync(email)).Id;
        var managerRows = await ReadUserAsync(managerId);
        var counts = await CountRowsAsync();

        var second = await RunAsync(
            factory.Services,
            Bootstrap("Hillside Supports", "Alex Morgan", managerEmail: email.ToUpperInvariant()));

        Assert.Equal((1, ""), (second.ExitCode, second.Output));
        Assert.Equal(
            Refusal("The address in --manager-email already has an account, and an address can have only one."),
            second.Error);
        Assert.Equal(counts, await CountRowsAsync());

        // Its security stamp among them, so the first run's link still works.
        Assert.Equal(managerRows, await ReadUserAsync(managerId));
        Assert.True(await VerifySetupTokenAsync(factory.Services, managerId, TokenIn(ReadOnlyLine(first.Output))));
    }

    [Theory]
    [MemberData(nameof(RefusedArguments))]
    public async Task A_refused_command_exits_with_1_says_why_and_writes_nothing(string[] arguments, string error)
    {
        var counts = await CountRowsAsync();

        var run = await RunAsync(
            factory.Services, [.. arguments.Select(argument => argument == Address ? NewAddress() : argument)]);

        Assert.Equal((1, "", error), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(counts, await CountRowsAsync());
    }

    // An address without its scheme is not absolute, and the invite core could make no link from it.
    [Theory]
    [InlineData("", "App:Origin is not set")]
    [InlineData("grow2notes.example", "App:Origin is not an absolute address")]
    public async Task Without_an_absolute_App_Origin_it_exits_with_1_says_why_and_writes_nothing(
        string origin, string reason)
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            configuration => configuration.AddInMemoryCollection([new("App:Origin", origin)])));
        var counts = await CountRowsAsync();

        var run = await RunAsync(app.Services, Bootstrap(managerEmail: NewAddress()));

        Assert.Equal(
            (1, "", Refusal($"{reason}, so no setup link can be made.")), (run.ExitCode, run.Output, run.Error));
        Assert.Equal(counts, await CountRowsAsync());
    }

    // The event is written inside the transaction, so a failure after it rolls back the organisation and the invite
    // with it, and the address is free for the run to be made again.
    [Fact]
    public async Task When_the_work_fails_after_the_event_is_written_nothing_is_kept_and_nothing_is_printed()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IAuditWriter>(
                provider => new FailsAfterWriting(ActivatorUtilities.CreateInstance<AuditWriter>(provider)))));
        var counts = await CountRowsAsync();
        var email = NewAddress();
        using var output = new StringWriter();
        using var error = new StringWriter();

        await Assert.ThrowsAsync<WorkFailedException>(
            () => RunAsync(app.Services, Bootstrap(managerEmail: email), output, error));

        Assert.Equal(("", ""), (output.ToString(), error.ToString()));
        Assert.Equal(counts, await CountRowsAsync());
        Assert.Null(await FindUserOrNullAsync(email));
    }

    // A transient failure at the commit runs the whole work again, after the setup link was made. The first attempt's
    // organisation and manager were rolled back, so the link printed must be the second attempt's.
    [Fact]
    public async Task When_the_commit_fails_transiently_one_organisation_is_added_and_its_managers_link_is_printed()
    {
        var commits = new FirstCommitFails();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(commits))));
        var counts = await CountRowsAsync();
        var email = NewAddress();

        var run = await RunAsync(app.Services, Bootstrap(managerEmail: email));

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.Equal(2, commits.Attempts);
        Assert.Equal(
            new RowCounts(counts.Organisations + 1, counts.Users + 1, counts.AuditEvents + 1), await CountRowsAsync());
        var manager = await FindUserAsync(email);
        var setupLink = ReadOnlyLine(run.Output);
        Assert.StartsWith($"{Grow2NotesFactory.Origin}/setup#u={manager.Id:D}&t=", setupLink, StringComparison.Ordinal);
        Assert.True(await VerifySetupTokenAsync(app.Services, manager.Id, TokenIn(setupLink)));
    }

    [Fact]
    public async Task Names_and_an_address_at_their_longest_are_stored_whole()
    {
        var organisationName = OrganisationName.PadRight(Limits.OrganisationName, 's');
        var managerName = ManagerName.PadRight(Limits.DisplayName, 'r');
        var email = $"sam.taylor.{Guid.NewGuid():N}".PadRight(Limits.Email - "@example.org".Length, 'r') +
            "@example.org";

        var run = await RunAsync(factory.Services, Bootstrap(organisationName, managerName, email));

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

    // The link holds the setup token, and the names and the address are personal (design.md §9.5).
    [Fact]
    public async Task Neither_a_run_nor_a_refused_one_logs_the_names_the_address_or_the_link()
    {
        var logs = new RecordingLoggerProvider();
        await using var app = factory.WithWebHostBuilder(
            builder => builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        var email = NewAddress();

        var done = await RunAsync(app.Services, Bootstrap(managerEmail: email));
        var refused = await RunAsync(app.Services, Bootstrap(managerEmail: email));

        Assert.Equal((0, 1), (done.ExitCode, refused.ExitCode));
        string[] personal = [OrganisationName, ManagerName, email, TokenIn(ReadOnlyLine(done.Output))];
        Assert.DoesNotContain(logs.Entries, entry => personal.Any(value => Holds(entry, value)));
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

    private static async Task<Run> RunAsync(IServiceProvider services, string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await RunAsync(services, arguments, output, error);
        return new(exitCode, output.ToString(), error.ToString());
    }

    // As Program.cs runs the entry, in a scope of its own.
    private static async Task<int> RunAsync(
        IServiceProvider services, string[] arguments, TextWriter output, TextWriter error)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AdminCommands>()
            .RunAsync(arguments, output, error, TestContext.Current.CancellationToken);
    }

    // The one line that was written, which must end with a line break.
    private static string ReadOnlyLine(string written)
    {
        Assert.EndsWith(Environment.NewLine, written, StringComparison.Ordinal);
        return Assert.Single(written.Split(Environment.NewLine)[..^1]);
    }

    // As the SPA reads the fragment, with URLSearchParams, which ParseQuery matches: + is a space, then %XX is decoded.
    private static string TokenIn(string setupLink) =>
        QueryHelpers.ParseQuery(setupLink[(setupLink.IndexOf('#', StringComparison.Ordinal) + 1)..])["t"].ToString();

    private static bool Holds(RecordedLog entry, string value) =>
        entry.Message.Contains(value, StringComparison.OrdinalIgnoreCase)
        || entry.Exception?.ToString().Contains(value, StringComparison.OrdinalIgnoreCase) == true
        || entry.Values.Any(v => v.Value?.ToString()?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);

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

    // Every organisation, user and audit event in the shared database. The tests of the collection run one at a time,
    // so only the run between two counts can change them.
    private async Task<RowCounts> CountRowsAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return new(
            await db.Organisations.CountAsync(cancellationToken),
            await db.Users.CountAsync(cancellationToken),
            // In SQL, since the tenant filter would count one organisation's events only.
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

    private sealed record Run(int ExitCode, string Output, string Error);

    private sealed record RowCounts(int Organisations, int Users, int AuditEvents);

    private sealed record StoredOrganisation(string Name, DateTime CreatedAtUtc);

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
