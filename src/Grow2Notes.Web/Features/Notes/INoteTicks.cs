namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The IDs that a note's content ticks and picks (design.md §5.7, §5.8), as <see cref="DraftSaveRequest"/> and
/// <see cref="NoteVersionRequest"/> carry them, so that <see cref="NoteSnapshot"/> checks and applies either.
/// </summary>
internal interface INoteTicks
{
    /// <summary>The IDs of the goals ticked as reached.</summary>
    IReadOnlyList<Guid> TickedGoalIds { get; }

    /// <summary>The IDs of the common item groups picked as having happened.</summary>
    IReadOnlyList<Guid> PickedGroupIds { get; }

    /// <summary>The IDs of the common items ticked as done.</summary>
    IReadOnlyList<Guid> TickedCommonItemIds { get; }
}
