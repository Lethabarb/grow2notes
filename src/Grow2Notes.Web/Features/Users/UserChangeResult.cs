using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// What a handler that changes a user did: made the change, or refused it with a design.md §6.9 code, which the app's
/// endpoint answers and Release 2's tools give as theirs (mcp-server.md §5.5). A refusal for
/// <see cref="ErrorCode.ValidationFailed"/> names the fields at fault and why, in the app's own words.
/// </summary>
internal sealed class UserChangeResult
{
    /// <summary>The change is made.</summary>
    public static readonly UserChangeResult Done = new(error: null, new Dictionary<string, string[]>());

    private UserChangeResult(ErrorCode? error, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        Error = error;
        FieldErrors = fieldErrors;
    }

    /// <summary>The code of a refusal, or <see langword="null"/> when the change is made.</summary>
    public ErrorCode? Error { get; }

    /// <summary>
    /// For <see cref="ErrorCode.ValidationFailed"/>, each field at fault, by the request member's C# name, with its
    /// errors; otherwise empty.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; }

    /// <summary>A refusal with <paramref name="error"/>, a code that names no field.</summary>
    public static UserChangeResult Refused(ErrorCode error) => new(error, new Dictionary<string, string[]>());

    /// <summary>
    /// A refusal with <see cref="ErrorCode.ValidationFailed"/> for the fields in <paramref name="errors"/>.
    /// </summary>
    public static UserChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorCode.ValidationFailed, errors);
}
