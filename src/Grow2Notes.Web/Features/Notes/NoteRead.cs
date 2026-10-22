using System.Text.Json.Serialization;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// A participant's live note for a day, as <c>GET {base}</c> answers it to any signed-in user of the organisation
/// (design.md §6.3): <see cref="Status"/>, the <c>NoteStatus</c> name; for a draft, its header, who started it,
/// <see cref="AuthorDisplayName"/>, and when, <see cref="StartedAtUtc"/>, the note's <c>CreatedAtUtc</c>, by the names
/// that <c>409 note.in_progress_by_other</c> gives them, and to a manager alone its author's working copy as
/// <see cref="Current"/>; and, for a submitted note, its author, its first submitted time and its current version.
/// Each member that the note does not have, or the caller may not see, is left out. The rest of §6.3's shape comes with
/// what uses it: T14 adds the author's ID, <c>isEdited</c>, <c>lastEdit</c> and the version's number, T16 a
/// manager's <c>flagStatus</c> and <c>reviews</c>, and T18 <c>isPastDayNote</c> and <c>versionCount</c>.
/// </summary>
internal sealed record NoteRead(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AuthorDisplayName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NoteReadAuthor? Author,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? StartedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? FirstSubmittedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NoteReadVersion? Current);

/// <summary>Who wrote a note: their display name as stored.</summary>
internal sealed record NoteReadAuthor(string DisplayName);

/// <summary>
/// A version of a note as the read view shows it (design.md §4.4), or a draft's working copy in the same shape
/// (§6.3): its Guided notes text, as written; whether it is flagged, and <see cref="FlagReason"/>, which is <c>null</c>
/// unless it is, and is written so, and which a flagged draft may not have yet, as only Submit needs one (§3.4); and
/// every goal of its snapshot, in the snapshot's order; and its <see cref="CommonItemGroups"/>.
/// </summary>
internal sealed record NoteReadVersion(
    string Narrative,
    bool IsFlagged,
    string? FlagReason,
    IReadOnlyList<NoteReadItem> Goals,
    IReadOnlyList<NoteReadGroup> CommonItemGroups);

/// <summary>
/// A common item group that a version or a working copy shows (D47): Every note when it has items, then the picked
/// groups, with their names as the snapshot holds them (A3) and every item, ticked or not. A group that is not picked
/// is never in it. It carries no ID.
/// </summary>
internal sealed record NoteReadGroup(string Name, bool IsEveryNote, IReadOnlyList<NoteReadItem> Items);

/// <summary>
/// A goal or a common item of a version or of a working copy: its wording as the snapshot holds it (A3), and whether
/// it is ticked, as reached for a goal and as done for a common item.
/// </summary>
internal sealed record NoteReadItem(string Text, bool IsTicked);
