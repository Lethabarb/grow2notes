using System.ComponentModel.DataAnnotations;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The body of <c>PUT {base}/draft</c> (design.md §6.3), an autosave of the caller's working copy: the page that sends
/// it, by <see cref="ClientId"/>, a random ID that each page load makes, and <see cref="Seq"/>, that page's count of
/// its saves of the note, which the working copy keeps as the page that saved last (§5.6); the Guided notes text; and
/// the IDs of the goals ticked as reached. The rest of §6.3's body comes with what reads it: the lists' version with
/// the creating save's checks, T09's flag, T13's picks and common-item ticks, and T14's base version.
/// </summary>
/// <remarks>
/// <see cref="Narrative"/> is required but may be empty, as a draft's may (§5.3), and is at most
/// <see cref="Limits.GuidedNotes"/> characters, a limit that only validation enforces, as its column has no size
/// (§5.1). <see cref="TickedGoalIds"/> is required, and may be empty. So a body without either, a <c>null</c> one or a
/// longer narrative gets <c>422 validation.failed</c> under its name before the endpoint runs, and writes nothing.
/// Public, as the validation source generator describes public types only.
/// </remarks>
public sealed record DraftSaveRequest(
    Guid ClientId,
    int Seq,
    [property: Required(AllowEmptyStrings = true), MaxLength(Limits.GuidedNotes)] string Narrative,
    [property: Required] IReadOnlyList<Guid> TickedGoalIds);
