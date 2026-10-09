namespace Grow2Notes.Web.Platform.OperatorCommands;

/// <summary>
/// The operator commands' one entry (design.md §7.4): it runs the command that its first argument names with the rest,
/// as <c>admin bootstrap</c>. <c>admin reset-signin</c> and <c>admin signout-all</c> (S00.03.06), and Release 2's
/// <c>admin mcp-client</c> (mcp-server.md §11.2), join it here.
/// </summary>
internal sealed class AdminCommands(BootstrapCommand bootstrap)
{
    /// <summary>The exit code of a command that has done its work.</summary>
    public const int Done = 0;

    /// <summary>The exit code of a command that was refused, which has written nothing.</summary>
    public const int Refused = 1;

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
            [var command, ..] => await RefuseAsync(error, $"\"{command}\" is not a command.", BootstrapCommand.Usage),
            _ => await RefuseAsync(error, "No command was given.", BootstrapCommand.Usage),
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
}
