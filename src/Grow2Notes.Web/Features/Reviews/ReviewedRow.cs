namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// A review on the Reviewed list, as <c>GET /api/reviews?status=reviewed</c> answers it (design.md §4.6, §6.4): the
/// review's ID, which orders reviews that share a millisecond and so completes the paging cursor; its note's
/// participant's ID and names as stored; the note's date; its author's display name as stored, which a deactivated
/// author keeps (§3.7); the reason of the flag the review cleared, whole, as the screen cuts it at its first line
/// break; the reviewer's display name as stored, which a deactivated reviewer keeps; the time of the review; and the
/// reviewer's comment, trimmed with its line breaks, or <see langword="null"/>. It holds nothing else of what the note
/// says (A9).
/// </summary>
internal sealed record ReviewedRow(
    Guid ReviewId,
    Guid ParticipantId,
    string GivenName,
    string FamilyName,
    DateOnly NoteDate,
    string AuthorDisplayName,
    string FlagReason,
    string ReviewedBy,
    DateTime ReviewedAtUtc,
    string? Comment);
