namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// A participant's header, as <c>GET /api/participants/{participantId}</c> gives it to any signed-in user (design.md
/// §6.3), for the name over their note: their ID, their names as stored, and <see cref="Status"/>, the
/// <c>ParticipantStatus</c> name, never their date of birth, which only managers get, from the admin endpoints. T21
/// adds <c>firstNoteDate</c> (D80).
/// </summary>
internal sealed record ParticipantHeader(Guid Id, string GivenName, string FamilyName, string Status);
