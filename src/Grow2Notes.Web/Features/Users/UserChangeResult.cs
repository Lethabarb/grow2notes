using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// What a handler that changes a user did: made the change, found it already made, found no such user, or refused it
/// with a design.md §6.9 code, which the app's endpoint answers and Release 2's tools give as theirs (mcp-server.md
/// §5.5). A refusal for <see cref="ErrorCode.ValidationFailed"/> names the fields at fault and why, in the app's own
/// words.
/// </summary>
internal sealed class UserChangeResult
{
    /// <summary>The change is made.</summary>
    public static readonly UserChangeResult Done = new(changed: true, isNotFound: false, error: null, NoFieldErrors());

    /// <summary>
    /// The user is already as the change would leave them, so nothing is changed or written; it is no refusal, so the
    /// app's endpoint answers as it does for <see cref="Done"/>, and Release 2's tools give <c>changed: false</c>
    /// (mcp-server.md §5.1).
    /// </summary>
    public static readonly UserChangeResult Unchanged =
        new(changed: false, isNotFound: false, error: null, NoFieldErrors());

    /// <summary>
    /// No user in the tenant's organisation has the ID: none has it at all, or another organisation's user has it,
    /// which a caller is not told apart from the first (design.md §9.2). §6.9 gives it no code: the app's endpoint
    /// answers <c>404</c>, and the assistant's tools give <c>not_found</c> (mcp-server.md §5.5).
    /// </summary>
    public static readonly UserChangeResult NotFound =
        new(changed: false, isNotFound: true, error: null, NoFieldErrors());

    private UserChangeResult(
        bool changed, bool isNotFound, ErrorCode? error, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        Changed = changed;
        IsNotFound = isNotFound;
        Error = error;
        FieldErrors = fieldErrors;
    }

    /// <summary>Whether this is <see cref="Done"/>: the change is made, and not <see cref="Unchanged"/>.</summary>
    public bool Changed { get; }

    /// <summary>Whether this is <see cref="NotFound"/>.</summary>
    public bool IsNotFound { get; }

    /// <summary>
    /// The code of a refusal, or <see langword="null"/> when the change is made or the user is not found.
    /// </summary>
    public ErrorCode? Error { get; }

    /// <summary>
    /// For <see cref="ErrorCode.ValidationFailed"/>, each field at fault, by the request member's C# name, with its
    /// errors; otherwise empty.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    /// <summary>A refusal with <paramref name="error"/>, a code that names no field.</summary>
    public static UserChangeResult Refused(ErrorCode error) =>
        new(changed: false, isNotFound: false, error, NoFieldErrors());

    /// <summary>
    /// A refusal with <see cref="ErrorCode.ValidationFailed"/> for the fields in <paramref name="errors"/>.
    /// </summary>
    public static UserChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(changed: false, isNotFound: false, ErrorCode.ValidationFailed, errors);

    private static Dictionary<string, string[]> NoFieldErrors() => [];
}
