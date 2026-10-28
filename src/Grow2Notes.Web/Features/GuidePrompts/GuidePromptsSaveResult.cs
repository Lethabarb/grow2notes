using System.Diagnostics.CodeAnalysis;
using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.GuidePrompts;

/// <summary>
/// What <see cref="GuidePromptsHandler.SaveAsync"/> did with the text: stored it, or found it already stored, and gave
/// the row's version token and whether it changed (<see cref="Changed"/>); or refused it with a design.md §6.9 code
/// and changed nothing. The constructor is private, so it is always one or the other.
/// </summary>
internal sealed class GuidePromptsSaveResult
{
    private GuidePromptsSaveResult(ErrorCode? error, string? token, bool changed)
    {
        Error = error;
        Token = token;
        Changed = changed;
        Succeeded = error is null;
    }

    /// <summary>
    /// Whether the text is stored, with <see cref="Token"/>; otherwise <see cref="Error"/> says why it is not.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Token))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded { get; }

    /// <summary>The code that the handler's caller answers with, when the text was refused.</summary>
    public ErrorCode? Error { get; }

    /// <summary>
    /// The row's version token once the text is stored: a new one after a save, or the one loaded when the text was
    /// already stored, which nothing changed (D84).
    /// </summary>
    public string? Token { get; }

    /// <summary>
    /// Whether the text was saved, with its event: <see langword="false"/> when it was already stored, which
    /// <c>set_guide_prompts</c> gives as <c>changed</c> (mcp-server.md §5.1).
    /// </summary>
    public bool Changed { get; }

    public static GuidePromptsSaveResult Stored(string token, bool changed) => new(error: null, token, changed);

    public static GuidePromptsSaveResult Refused(ErrorCode error) => new(error, token: null, changed: false);
}
