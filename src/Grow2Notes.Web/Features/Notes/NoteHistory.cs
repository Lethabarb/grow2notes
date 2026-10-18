using System.Text.Json.Serialization;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// A page of a participant's history, as <c>GET /api/participants/{participantId}/notes</c> answers it (design.md
/// §6.3): up to 30 of their live notes, newest first, and whether older ones remain (§6.1 <i>Lists</i>), which the
/// next page asks for before the last row's date.
/// </summary>
internal sealed record NoteHistory(IReadOnlyList<NoteHistoryRow> Notes, bool HasMore);

/// <summary>
/// A live note on a participant's history (design.md §4.4 item 2): its date; <see cref="Status"/>, the
/// <c>NoteStatus</c> name, <c>Draft</c> or <c>Submitted</c>; its author's display name as stored; whether its current
/// version is flagged, which a draft, with no version, never is; and, to a manager alone, <see cref="FlagStatus"/>,
/// the <c>FlagStatus</c> name, left out for anyone else. It holds nothing of what a note says (A9). T14 adds
/// <c>currentVersionNumber</c> and <c>isEdited</c>.
/// </summary>
internal sealed record NoteHistoryRow(
    DateOnly NoteDate,
    string Status,
    string AuthorDisplayName,
    bool IsFlagged,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FlagStatus);
