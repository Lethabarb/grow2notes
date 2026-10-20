namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// One of the caller's working copies, as <c>GET /api/me/drafts</c> lists it (design.md §6.3) for Your unfinished
/// drafts (§4.2 item 2): its participant's ID and names as stored; its note's date; <see cref="Kind"/>,
/// <see cref="DraftKind"/> for a draft, on base version 0, or <see cref="PendingEditKind"/> for a pending edit, above
/// it (§5.3), which tells the row whether to open the note's address or its edit form (note-form.md <i>Routes</i>);
/// and when it was last saved, the working copy's <c>UpdatedAtUtc</c>. It holds nothing of what the note says (A9).
/// </summary>
internal sealed record MyDraft(
    Guid ParticipantId, string GivenName, string FamilyName, DateOnly NoteDate, string Kind, DateTime SavedAtUtc)
{
    public const string DraftKind = "draft";

    public const string PendingEditKind = "pendingEdit";
}
