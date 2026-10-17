using Grow2Notes.Web.Platform.OperatorCommands;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Runs of the operator commands' one entry, <see cref="AdminCommands"/> (design.md §7.4), as Program.cs runs it, in a
/// scope of its own, with writers in place of the console, for the tests of each command.
/// </summary>
internal static class AdminCommandRuns
{
    /// <summary>
    /// Runs the entry in <paramref name="app"/> with <paramref name="arguments"/>, and returns its exit code and what
    /// it wrote on each writer.
    /// </summary>
    public static async Task<AdminCommandRun> RunAdminCommandAsync(
        this WebApplicationFactory<Program> app, string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await app.RunAdminCommandAsync(arguments, output, error);
        return new(exitCode, output.ToString(), error.ToString());
    }

    /// <summary>
    /// Runs the entry in <paramref name="app"/> with <paramref name="arguments"/> on the writers given, which a test
    /// reads even when the run throws, and returns its exit code.
    /// </summary>
    public static async Task<int> RunAdminCommandAsync(
        this WebApplicationFactory<Program> app, string[] arguments, TextWriter output, TextWriter error)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AdminCommands>()
            .RunAsync(arguments, output, error, TestContext.Current.CancellationToken);
    }
}

/// <summary>An operator command's exit code, and what it wrote on the output and error writers.</summary>
internal sealed record AdminCommandRun(int ExitCode, string Output, string Error)
{
    /// <summary>The one line written on the output writer, which must end with a line break.</summary>
    public string SingleOutputLine()
    {
        Assert.EndsWith(Environment.NewLine, Output, StringComparison.Ordinal);
        return Assert.Single(Output.Split(Environment.NewLine)[..^1]);
    }
}
