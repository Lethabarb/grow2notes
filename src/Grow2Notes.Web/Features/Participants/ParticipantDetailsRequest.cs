namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The body of <c>POST /api/admin/participants</c> (design.md §6.6): a participant's given and family names, as typed,
/// and their date of birth, a calendar date written <c>1987-03-07</c>. Each may be missing, so that
/// <see cref="ParticipantDetails.TryRead"/> checks all three and words each refusal as the Participants screens do.
/// </summary>
/// <remarks>
/// Nothing else is read: any other field in the body, such as a government identifier, is passed over, so none is
/// stored (A5).
/// </remarks>
internal sealed record ParticipantDetailsRequest(string? GivenName, string? FamilyName, DateOnly? DateOfBirth);
