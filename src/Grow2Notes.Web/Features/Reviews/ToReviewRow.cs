namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// A note on the To review list, as <c>GET /api/reviews?status=toReview</c> answers it (design.md §4.6 item 2, §6.4):
/// its participant's ID and names as stored; its date; its author's display name as stored, which a deactivated author
/// keeps (§3.7); the reason and time of its flagged version, the version that last put it in To review, whole, as the
/// screen cuts the reason at its first line break; the number of its current version; and whether that version is not
/// flagged, a later edit having removed the flag (A15). It holds nothing else of what the note says (A9).
/// </summary>
internal sealed record ToReviewRow(
    Guid ParticipantId,
    string GivenName,
    string FamilyName,
    DateOnly NoteDate,
    string AuthorDisplayName,
    string FlagReason,
    DateTime FlaggedAtUtc,
    int CurrentVersionNumber,
    bool FlagRemovedLater);
