using System.Text.Json.Serialization;

namespace Grow2Notes.Web.Features.Today;

/// <summary>
/// The Today list, as <c>GET /api/today</c> answers it (design.md §6.3): <see cref="Date"/>, the date in Melbourne
/// from <c>MelbourneClock</c>, written <c>2026-10-01</c>, which the rows' links name; and the organisation's active
/// participants, by family name, then given name (A5).
/// </summary>
internal sealed record TodayList(DateOnly Date, IReadOnlyList<TodayParticipant> Participants);

/// <summary>
/// A participant on the Today list: their ID and their names as stored, never their date of birth, which only managers
/// get (§6.3); and their live note for the list's date, or <see langword="null"/>, written so, when they have none.
/// </summary>
internal sealed record TodayParticipant(Guid Id, string GivenName, string FamilyName, TodayNote? Note);

/// <summary>
/// A participant's live note for the day, as the caller's row shows it (design.md §4.2 item 4, §6.3):
/// <see cref="Status"/>, the <c>NoteStatus</c> name, <c>Draft</c> or <c>Submitted</c>; its author's display name, and
/// whether the caller is its author; <see cref="StartedAtUtc"/>, the note's <c>CreatedAtUtc</c>; on the caller's own
/// draft alone, <see cref="SavedAtUtc"/>, the time of their working copy's last save; on a submitted note alone,
/// <see cref="FirstSubmittedAtUtc"/>; and whether its current version is flagged, which a draft, with no version, never
/// is. It holds nothing of what a draft says (A9).
/// </summary>
internal sealed record TodayNote(
    string Status,
    string AuthorDisplayName,
    bool IsMine,
    DateTime StartedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? SavedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? FirstSubmittedAtUtc,
    bool IsFlagged);
