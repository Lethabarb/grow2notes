using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// What <see cref="CommonItemChanges"/> did with a change to a group's common items: found no such group or item in the
/// tenant's organisation, another organisation's included (design.md §9.2); refused the change with a design.md §6.9
/// code, <c>If-Match</c>'s, the group's, the reorder's or the save's (<see cref="Error"/>); refused the text, with its
/// message (<see cref="Errors"/>); or took the change, and saved it with its audit event when a value changed.
/// </summary>
internal sealed record CommonItemChangeResult
{
    /// <summary>No group or item of that ID is in the tenant's organisation, so nothing was changed.</summary>
    public static readonly CommonItemChangeResult NotFound = new() { Found = false };

    /// <summary>
    /// The change is taken: saved, with its audit event, or, when no value changed, with nothing to save (D84).
    /// </summary>
    public static readonly CommonItemChangeResult Saved = new();

    private CommonItemChangeResult()
    {
    }

    public bool Found { get; private init; } = true;

    /// <summary>The code that the change was refused with, or <see langword="null"/>.</summary>
    public ErrorCode? Error { get; private init; }

    /// <summary>
    /// Each refused field's messages, keyed by its member's name in the request, or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; private init; }

    /// <summary>The change was refused with <paramref name="error"/>, and nothing was saved.</summary>
    public static CommonItemChangeResult Refused(ErrorCode error) => new() { Error = error };

    /// <summary>The change was refused for the fields <paramref name="errors"/> name, and nothing was saved.</summary>
    public static CommonItemChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new() { Errors = errors };
}
