using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// What <see cref="CommonItemChanges"/> did with a change to a group's common items, or
/// <see cref="CommonItemGroupChanges"/> with a change to a group: found no such group or item in the tenant's
/// organisation, another organisation's included (design.md §9.2); refused the change with a design.md §6.9 code,
/// <c>If-Match</c>'s, the group's, the reorder's or the save's (<see cref="Error"/>); refused the text or the name,
/// with its message (<see cref="Errors"/>); or took the change, and saved it with its audit event only when a value
/// changed (<see cref="Changed"/>), giving the new item's ID after an add (<see cref="ItemId"/>).
/// </summary>
internal sealed record CommonItemChangeResult
{
    /// <summary>No group or item of that ID is in the tenant's organisation, so nothing was changed.</summary>
    public static readonly CommonItemChangeResult NotFound = new() { Found = false };

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

    /// <summary>
    /// Whether the change was saved, with its event: <see langword="false"/> for one that changed no value, which
    /// Release 2's tools give as <c>changed</c> (mcp-server.md §5.1).
    /// </summary>
    public bool Changed { get; private init; }

    /// <summary>The ID of the item that an add made, or <see langword="null"/>.</summary>
    public Guid? ItemId { get; private init; }

    /// <summary>The change was refused with <paramref name="error"/>, and nothing was saved.</summary>
    public static CommonItemChangeResult Refused(ErrorCode error) => new() { Error = error };

    /// <summary>The change was refused for the fields <paramref name="errors"/> name, and nothing was saved.</summary>
    public static CommonItemChangeResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new() { Errors = errors };

    /// <summary>
    /// What <see cref="ConfigurationChanges.SaveAsync"/> did: saved the change, found nothing to change, or refused it,
    /// with the code it gave.
    /// </summary>
    public static CommonItemChangeResult Of(ConfigurationSaveResult saved) =>
        new() { Error = saved.Error, Changed = saved.Changed };

    /// <summary>An item was added, with the ID <paramref name="itemId"/>, and saved with its audit event.</summary>
    public static CommonItemChangeResult Added(Guid itemId) => new() { Changed = true, ItemId = itemId };
}
