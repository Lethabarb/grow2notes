using System.Text.Json.Serialization;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// A participant's live note for a day, as <c>GET {base}</c> answers it to any signed-in user of the organisation
/// (design.md §6.3): <see cref="Status"/>, the <c>NoteStatus</c> name, alone for a draft; and, for a submitted note,
/// its author, its first submitted time and its current version. Each member that a draft does not have is left out.
/// The rest of §6.3's shape comes with what uses it: T12 adds a draft's header and a manager's draft content, T13 the
/// common items, T14 the author's ID, <c>isEdited</c>, <c>lastEdit</c> and the version's number, T16 a manager's
/// <c>flagStatus</c> and <c>reviews</c>, and T18 <c>isPastDayNote</c> and <c>versionCount</c>.
/// </summary>
internal sealed record NoteRead(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NoteReadAuthor? Author,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? FirstSubmittedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NoteReadVersion? Current);

/// <summary>Who wrote a note: their display name as stored.</summary>
internal sealed record NoteReadAuthor(string DisplayName);

/// <summary>
/// A version of a note as the read view shows it (design.md §4.4): its Guided notes text, as written; whether it is
/// flagged, and <see cref="FlagReason"/>, which is <c>null</c> unless it is, and is written so; and every goal of its
/// snapshot, in the snapshot's order.
/// </summary>
internal sealed record NoteReadVersion(
    string Narrative, bool IsFlagged, string? FlagReason, IReadOnlyList<NoteReadItem> Goals);

/// <summary>
/// A goal of a version: its wording as the version holds it (A3), and whether it is ticked as reached.
/// </summary>
internal sealed record NoteReadItem(string Text, bool IsTicked);
