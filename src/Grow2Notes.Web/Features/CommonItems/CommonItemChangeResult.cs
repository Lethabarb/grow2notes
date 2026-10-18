using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// What <see cref="CommonItemChanges"/> did with a change to a group's common items: found no such group in the
/// tenant's organisation, another organisation's included (design.md §9.2); refused the change with a design.md §6.9
/// code (<see cref="Error"/>); refused the text, with its message (<see cref="Errors"/>); or took the change, and saved
/// it with its audit event.
/// </summary>
internal sealed record CommonItemChangeResult
{
    /// <summary>No group of that ID is in the tenant's organisation, so nothing was changed.</summary>
    public static readonly CommonItemChangeResult NotFound = new() { Found = false };

    /// <summary>The change is saved, with its audit event.</summary>
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
