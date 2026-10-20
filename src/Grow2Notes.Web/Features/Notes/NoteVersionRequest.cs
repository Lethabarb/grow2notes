using System.ComponentModel.DataAnnotations;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The body of <c>POST {base}/versions</c> (design.md §6.3), Submit: the version that the save works on, 0 for a
/// draft; and the note's latest content, repeated so that a lagging autosave is never lost: the Guided notes text, the
/// IDs of the goals ticked as reached, the IDs of the common item groups picked as having happened and of the common
/// items ticked as done, and whether the note is flagged for a manager, with the reason.
/// </summary>
/// <remarks>
/// <see cref="BaseVersion"/> is 0 until T14 saves changes on a later version. <see cref="Narrative"/> is required and
/// not blank, spaces alone counting as blank, as a version always has text (§5.8 step 4), and is at most
/// <see cref="Limits.GuidedNotes"/> characters. <see cref="TickedGoalIds"/>, <see cref="PickedGroupIds"/> and
/// <see cref="TickedCommonItemIds"/> are required, and may be empty, as a note may be submitted with nothing picked.
/// <see cref="FlagReason"/> is at most <see cref="Limits.FlagReason"/> characters; that a flagged note has one is the
/// handler's check, as it reads the flag. So a body that breaks any of these gets <c>422 validation.failed</c> under
/// its name before the endpoint runs, and writes nothing. Public, as the validation source generator describes public
/// types only.
/// </remarks>
public sealed record NoteVersionRequest(
    [property: Range(0, 0)] int BaseVersion,
    [property: Required, MaxLength(Limits.GuidedNotes)] string Narrative,
    [property: Required] IReadOnlyList<Guid> TickedGoalIds,
    [property: Required] IReadOnlyList<Guid> PickedGroupIds,
    [property: Required] IReadOnlyList<Guid> TickedCommonItemIds,
    bool IsFlagged,
    [property: MaxLength(Limits.FlagReason)] string? FlagReason) : INoteTicks;
