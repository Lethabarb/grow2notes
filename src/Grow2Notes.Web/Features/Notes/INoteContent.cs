namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The IDs that a note's content ticks and picks, and the text it adds to them (design.md §5.7, §5.8), as
/// <see cref="DraftSaveRequest"/> and <see cref="NoteVersionRequest"/> carry them, so that <see cref="NoteSnapshot"/>
/// checks and applies either.
/// </summary>
internal interface INoteContent
{
    /// <summary>The IDs of the goals ticked as reached.</summary>
    IReadOnlyList<Guid> TickedGoalIds { get; }

    /// <summary>The IDs of the common item groups picked as having happened.</summary>
    IReadOnlyList<Guid> PickedGroupIds { get; }

    /// <summary>The IDs of the common items ticked as done.</summary>
    IReadOnlyList<Guid> TickedCommonItemIds { get; }

    /// <summary>
    /// The details written for common items that ask for them, by item ID; null for none. Keyed by ID, so an item has
    /// at most one (A151).
    /// </summary>
    IReadOnlyDictionary<Guid, string>? CommonItemDetails { get; }

    /// <summary>The Medication text, or null for none (A152).</summary>
    string? Medication { get; }
}
