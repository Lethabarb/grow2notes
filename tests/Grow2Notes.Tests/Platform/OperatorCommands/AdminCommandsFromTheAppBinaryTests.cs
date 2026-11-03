using System.Diagnostics;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.OperatorCommands;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.OperatorCommands;

/// <summary>
/// The operator commands run from the app's own binary, as the SSH console runs them (design.md §7.4): the built
/// Grow2Notes.Web.dll, started with <c>dotnet</c> in a process of its own, from the folder that holds it, as the
/// console runs it from <c>/home/site/wwwroot</c>, with the shared database's connection string and an
/// <c>App__Origin</c> in its environment, as App Service gives the app its settings. Each run has made-up names and an
/// address of its own; <c>admin signout-all</c>, which gives every user a new security stamp, the seeded users' too,
/// runs on a migrated database of its own. The binary has no email settings here, so a command that emails a setup
/// link runs with <c>Email__UseConsole</c> true, which writes the email to standard output, as development and CI's
/// end-to-end run do.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AdminCommandsFromTheAppBinaryTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // A web server never exits by itself, so a run that ends within this has served nothing. It leaves room for a slow
    // start on a busy CI runner.
    private static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(1);

    private static readonly Dictionary<string, string> ConsoleEmail = new() { ["Email__UseConsole"] = "true" };

    // A command registers no telemetry (design.md §9.5): with a connection string set and no
    // ManagedIdentity__ClientId, registering it would throw, so a command that exits 0 has not.
    private static readonly Dictionary<string, string> ConsoleEmailAndTelemetry = new()
    {
        ["Email__UseConsole"] = "true",
        ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=not-a-real-key",
    };

    [Fact]
    public async Task Admin_bootstrap_exits_with_0_registers_no_telemetry_and_emails_a_link_the_web_app_verifies()
    {
        // Data Protection loads the key ring as the app starts, and makes a key if there is none. Starting the web app
        // first leaves a key in the shared database for the command to protect the token with, as in test, where the
        // web app has been running before the operator runs a command.
        _ = factory.Services;
        var email = $"sam.taylor.{Guid.NewGuid():N}@example.org";

        var run = await RunWithAsync(
            ConsoleEmailAndTelemetry,
            "admin", "bootstrap", "--organisation", "Riverside Supports", "--manager-name", "Sam Taylor",
            "--manager-email", email);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var manager = await users.FindByEmailAsync(email);
        Assert.NotNull(manager);
        await AssertEmailsALinkTheWebAppVerifiesAsync(run.Output, users, manager);
    }

    // The binary has no email settings, so its sender is the not-configured one, whose every send fails.
    [Fact]
    public async Task Admin_bootstrap_without_the_email_settings_exits_with_1_and_writes_nothing()
    {
        _ = factory.Services;
        var email = $"sam.taylor.{Guid.NewGuid():N}@example.org";

        // Pinned off, so that a Development environment in the test runner cannot choose the console sender.
        var run = await RunWithAsync(
            new() { ["Email__UseConsole"] = "false" },
            "admin", "bootstrap", "--organisation", "Riverside Supports", "--manager-name", "Sam Taylor",
            "--manager-email", email);

        Assert.Equal(
            (1, "", $"Refused: The setup-link email could not be sent. Nothing was written.{Environment.NewLine}"),
            (run.ExitCode, run.Output, run.Error));
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Admin_bootstrap_with_no_options_exits_with_1_and_says_why_on_standard_error_only()
    {
        var run = await RunAsync("admin", "bootstrap");

        Assert.Equal(
            (1, "", $"Refused: --organisation is missing. Nothing was written.{Environment.NewLine}" +
                $"{BootstrapCommand.Usage}{Environment.NewLine}"),
            (run.ExitCode, run.Output, run.Error));
    }

    [Fact]
    public async Task Admin_reset_signin_exits_with_0_and_emails_a_link_the_web_app_verifies()
    {
        // Adding the worker starts the web app, which leaves a key in the shared database, as for admin bootstrap.
        var email = $"sam.taylor.{Guid.NewGuid():N}@example.org";
        await AddActiveWorkerAsync(factory, sqlServer.Seeded.A.OrganisationId, email);

        var run = await RunWithAsync(ConsoleEmail, "admin", "reset-signin", email);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var worker = await users.FindByEmailAsync(email);
        Assert.NotNull(worker);
        Assert.Equal(UserStatus.Invited, worker.Status);
        await AssertEmailsALinkTheWebAppVerifiesAsync(run.Output, users, worker);
    }

    [Fact]
    public async Task Admin_reset_signin_with_no_address_exits_with_1_and_says_why_on_standard_error_only()
    {
        var run = await RunAsync("admin", "reset-signin");

        Assert.Equal(
            (1, "", $"Refused: No address was given. Nothing was written.{Environment.NewLine}" +
                $"{ResetSignInCommand.Usage}{Environment.NewLine}"),
            (run.ExitCode, run.Output, run.Error));
    }

    [Fact]
    public async Task Admin_signout_all_exits_with_0_prints_nothing_and_gives_every_user_a_new_stamp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await sqlServer.CreateDatabaseAsync("SignOutAllFromTheAppBinary", cancellationToken);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection([new("ConnectionStrings:Grow2Notes", connectionString)])));
        var organisationId = await AddOrganisationAsync(app);
        var worker = await AddActiveWorkerAsync(app, organisationId, $"sam.taylor.{Guid.NewGuid():N}@example.org");

        var run = await RunOnAsync(connectionString, [], "admin", "signout-all");

        Assert.Equal((0, "", ""), (run.ExitCode, run.Output, run.Error));
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        Assert.NotEqual(
            worker.SecurityStamp,
            await db.Users.Where(user => user.Id == worker.Id).Select(user => user.SecurityStamp)
                .SingleAsync(cancellationToken));
        Assert.Equal(
            1,
            Assert.Single(await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value] FROM [AuditEvent]
                WHERE [EventType] = {AuditEventTypes.Admin.SignOutAll} AND [OrganisationId] = {organisationId}
                """).ToListAsync(cancellationToken)));
    }

    // Refused before it reads the database, so it runs on the shared one, whose stamps it would otherwise change.
    [Fact]
    public async Task Admin_signout_all_with_an_option_exits_with_1_and_says_why_on_standard_error_only()
    {
        var run = await RunAsync("admin", "signout-all", "--all");

        Assert.Equal(
            (1, "", $"Refused: \"--all\" is not an option of admin signout-all. Nothing was written." +
                $"{Environment.NewLine}{SignOutAllCommand.Usage}{Environment.NewLine}"),
            (run.ExitCode, run.Output, run.Error));
    }

    // One console email to the person, with the setup-link subject and one link, and nothing else. The web app's
    // content root is the web project's folder and the command's is the folder it ran from, so the two share one key
    // ring only because Program.cs names the application for both, with SetApplicationName.
    private static async Task AssertEmailsALinkTheWebAppVerifiesAsync(
        string output, UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            $"To: {user.Email}", Assert.Single(lines, line => line.StartsWith("To: ", StringComparison.Ordinal)));
        Assert.Equal(
            $"Subject: {SetupLinkEmail.Subject}",
            Assert.Single(lines, line => line.StartsWith("Subject: ", StringComparison.Ordinal)));
        var setupLink = Assert.Single(lines, line => line.StartsWith("https://", StringComparison.Ordinal));
        Assert.StartsWith($"{Grow2NotesFactory.Origin}/setup#u={user.Id:D}&t=", setupLink, StringComparison.Ordinal);
        var token = QueryHelpers.ParseQuery(setupLink[(setupLink.IndexOf('#', StringComparison.Ordinal) + 1)..])["t"];
        Assert.True(await users.VerifyUserTokenAsync(
            user, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, token.ToString()));
    }

    // An organisation is the tenant, not tenant-owned, so this needs no tenant.
    private static async Task<Guid> AddOrganisationAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Riverside Supports", CreatedAtUtc = TestClock.Start.UtcDateTime };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return organisation.Id;
    }

    // An Active worker, as setup leaves one (design.md §8.1) apart from a way to sign in, which these tests do not use.
    private static async Task<ApplicationUser> AddActiveWorkerAsync(
        WebApplicationFactory<Program> app, Guid organisationId, string email)
    {
        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = "Sam Taylor",
            Role = UserRole.Worker,
            Status = UserStatus.Active,
            InvitedAtUtc = TestClock.Start.UtcDateTime,
            ActivatedAtUtc = TestClock.Start.UtcDateTime,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = app.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user;
    }

    private Task<Run> RunAsync(params string[] arguments) => RunWithAsync([], arguments);

    private Task<Run> RunWithAsync(Dictionary<string, string> environment, params string[] arguments) =>
        RunOnAsync(sqlServer.ConnectionString, environment, arguments);

    // Starts dotnet Grow2Notes.Web.dll on the database at connectionString with the arguments, and the settings in
    // environment besides, and waits for it to exit, up to the time limit.
    private static async Task<Run> RunOnAsync(
        string connectionString, Dictionary<string, string> environment, params string[] arguments)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var web = typeof(Program).Assembly.Location;
        using var process = new Process
        {
            StartInfo = new("dotnet", [web, .. arguments])
            {
                // The content root, which holds the app's appsettings.json, as /home/site/wwwroot does in Azure.
                WorkingDirectory = Path.GetDirectoryName(web),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    ["ConnectionStrings__Grow2Notes"] = connectionString,
                    ["App__Origin"] = Grow2NotesFactory.Origin,
                },
            },
        };
        foreach (var (name, value) in environment)
        {
            process.StartInfo.Environment[name] = value;
        }

        process.Start();
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeLimit, cancellationToken);
            return new(process.ExitCode, await output, await error);
        }
        finally
        {
            // Still running only when the run failed the test, by passing the time limit or being cancelled.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private sealed record Run(int ExitCode, string Output, string Error);
}
