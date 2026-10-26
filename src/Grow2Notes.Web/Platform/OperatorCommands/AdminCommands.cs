using Grow2Notes.Web.Features.Mcp.OAuth;

namespace Grow2Notes.Web.Platform.OperatorCommands;

/// <summary>
/// The operator commands' one entry (design.md §7.4): it runs the command that its first argument names with the rest,
/// <c>admin bootstrap</c>, <c>admin reset-signin</c>, <c>admin signout-all</c> or, for Release 2,
/// <c>admin mcp-client</c> (mcp-server.md §11.2).
/// </summary>
internal sealed class AdminCommands(
    BootstrapCommand bootstrap,
    ResetSignInCommand resetSignIn,
    SignOutAllCommand signOutAll,
    McpClientCommand mcpClient)
{
    /// <summary>The exit code of a command that has done its work.</summary>
    public const int Done = 0;

    /// <summary>The exit code of a command that was refused, which has written nothing.</summary>
    public const int Refused = 1;

    /// <summary>How each command is used, one to a line, for a run that names no command or an unknown one.</summary>
    public static readonly string Usage = string.Join(
        Environment.NewLine,
        BootstrapCommand.Usage,
        ResetSignInCommand.Usage,
        SignOutAllCommand.Usage,
        McpClientCommand.Usage);

    /// <summary>
    /// Runs the command that <paramref name="arguments"/> names, or refuses an unknown one, or none, saying why on
    /// <paramref name="error"/>.
    /// </summary>
    /// <param name="arguments">The command's name and then its options, as typed after <c>admin</c>.</param>
    /// <param name="output">Where the command writes its result, such as a setup link, and nothing else.</param>
    /// <param name="error">Where the command says why it was refused.</param>
    /// <param name="cancellationToken">Cancels the command's database work.</param>
    /// <returns><see cref="Done"/>, or <see cref="Refused"/>.</returns>
    public async Task<int> RunAsync(
        IReadOnlyList<string> arguments, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default) =>
        arguments switch
        {
            [BootstrapCommand.Name, ..] =>
                await bootstrap.RunAsync([.. arguments.Skip(1)], output, error, cancellationToken),
            [ResetSignInCommand.Name, ..] =>
                await resetSignIn.RunAsync([.. arguments.Skip(1)], output, error, cancellationToken),
            [SignOutAllCommand.Name, ..] =>
                await signOutAll.RunAsync([.. arguments.Skip(1)], error, cancellationToken),
            [McpClientCommand.Name, ..] =>
                await mcpClient.RunAsync([.. arguments.Skip(1)], output, error, cancellationToken),
            [var command, ..] => await RefuseAsync(error, $"\"{command}\" is not a command.", Usage),
            _ => await RefuseAsync(error, "No command was given.", Usage),
        };

    /// <summary>
    /// Says on <paramref name="error"/> why a command was refused, and then how it is used, if given.
    /// </summary>
    /// <returns><see cref="Refused"/>.</returns>
    public static async Task<int> RefuseAsync(TextWriter error, string reason, string? usage)
    {
        await error.WriteLineAsync($"Refused: {reason} Nothing was written.");
        if (usage is not null)
        {
            await error.WriteLineAsync(usage);
        }

        return Refused;
    }

    /// <summary>
    /// Why <paramref name="configuration"/>'s <c>App:Origin</c> cannot make what a command needs of it, as a refusal's
    /// reason, or null when it can. A command that makes a link or names a resource from it checks it before its
    /// transaction, so that such a setting is refused, rather than thrown from inside the transaction.
    /// </summary>
    /// <param name="configuration">The app's settings.</param>
    /// <param name="consequence">What cannot be done without the setting, ending the sentence.</param>
    public static string? FindOriginProblem(IConfiguration configuration, string consequence)
    {
        try
        {
            return AppOrigin.Find(configuration) is null
                ? $"{AppOrigin.Key} is not set, so {consequence}"
                : null;
        }
        catch (UriFormatException)
        {
            return $"{AppOrigin.Key} is not an absolute address, so {consequence}";
        }
    }
}
