namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// A participant's header, as <c>GET /api/participants/{participantId}</c> gives it to any signed-in user (design.md
/// §6.3), for the name over their note: their ID, their names as stored, and <see cref="Status"/>, the
/// <c>ParticipantStatus</c> name, never their date of birth, which only managers get, from the admin endpoints.
/// <see cref="FirstNoteDate"/> is the date of their earliest submitted note, or <c>null</c> when they have none, for
/// the export screen's From date (D80); a draft or a discarded draft is not a note to count.
/// </summary>
internal sealed record ParticipantHeader(
    Guid Id, string GivenName, string FamilyName, string Status, DateOnly? FirstNoteDate);
