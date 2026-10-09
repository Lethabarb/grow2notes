using System.Diagnostics;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform.OperatorCommands;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform.OperatorCommands;

/// <summary>
/// The operator commands run from the app's own binary, as the SSH console runs them (design.md §7.4): the built
/// Grow2Notes.Web.dll, started with <c>dotnet</c> in a process of its own, from the folder that holds it, as the
/// console runs it from <c>/home/site/wwwroot</c>, with the shared database's connection string and an
/// <c>App__Origin</c> in its environment, as App Service gives the app its settings. Each run has made-up names and an
/// address of its own.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AdminCommandsFromTheAppBinaryTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // A web server never exits by itself, so a run that ends within this has served nothing. It leaves room for a slow
    // start on a busy CI runner.
    private static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task Admin_bootstrap_exits_with_0_and_prints_only_the_link_whose_token_the_web_app_verifies()
    {
        // Data Protection loads the key ring as the app starts, and makes a key if there is none. Starting the web app
        // first leaves a key in the shared database for the command to protect the token with, as in test, where the
        // web app has been running before the operator runs a command.
        _ = factory.Services;
        var email = $"sam.taylor.{Guid.NewGuid():N}@example.org";

        var run = await RunAsync(
            "admin", "bootstrap", "--organisation", "Riverside Supports", "--manager-name", "Sam Taylor",
            "--manager-email", email);

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.EndsWith(Environment.NewLine, run.Output, StringComparison.Ordinal);
        var setupLink = Assert.Single(run.Output.Split(Environment.NewLine)[..^1]);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var manager = await users.FindByEmailAsync(email);
        Assert.NotNull(manager);
        Assert.StartsWith($"{Grow2NotesFactory.Origin}/setup#u={manager.Id:D}&t=", setupLink, StringComparison.Ordinal);

        // The web app's content root is the web project's folder and the command's is the folder it ran from, so the
        // two share one key ring only because Program.cs names the application for both, with SetApplicationName.
        var token = QueryHelpers.ParseQuery(setupLink[(setupLink.IndexOf('#', StringComparison.Ordinal) + 1)..])["t"];
        Assert.True(await users.VerifyUserTokenAsync(
            manager, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, token.ToString()));
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

    // Starts dotnet Grow2Notes.Web.dll with the arguments, and waits for it to exit, up to the time limit.
    private async Task<Run> RunAsync(params string[] arguments)
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
                    ["ConnectionStrings__Grow2Notes"] = sqlServer.ConnectionString,
                    ["App__Origin"] = Grow2NotesFactory.Origin,
                },
            },
        };
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
