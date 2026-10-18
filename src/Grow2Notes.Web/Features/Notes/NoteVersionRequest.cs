using System.ComponentModel.DataAnnotations;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The body of <c>POST {base}/versions</c> (design.md §6.3), Submit: the version that the save works on, 0 for a
/// draft; and the note's latest content, repeated so that a lagging autosave is never lost: the Guided notes text, the
/// IDs of the goals ticked as reached, and whether the note is flagged for a manager, with the reason. The rest of
/// §6.3's body comes with what reads it: T13's picks and common-item ticks.
/// </summary>
/// <remarks>
/// <see cref="BaseVersion"/> is 0 until T14 saves changes on a later version. <see cref="Narrative"/> is required and
/// not blank, spaces alone counting as blank, as a version always has text (§5.8 step 4), and is at most
/// <see cref="Limits.GuidedNotes"/> characters. <see cref="TickedGoalIds"/> is required, and may be empty.
/// <see cref="FlagReason"/> is at most <see cref="Limits.FlagReason"/> characters; that a flagged note has one is the
/// handler's check, as it reads the flag. So a body that breaks any of these gets <c>422 validation.failed</c> under
/// its name before the endpoint runs, and writes nothing. Public, as the validation source generator describes public
/// types only.
/// </remarks>
public sealed record NoteVersionRequest(
    [property: Range(0, 0)] int BaseVersion,
    [property: Required, MaxLength(Limits.GuidedNotes)] string Narrative,
    [property: Required] IReadOnlyList<Guid> TickedGoalIds,
    bool IsFlagged,
    [property: MaxLength(Limits.FlagReason)] string? FlagReason);
