using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The body of <c>POST /api/admin/participants</c> and <c>PUT /api/admin/participants/{id}</c> (design.md §6.6): a
/// participant's given and family names, as typed, and their date of birth, a calendar date written
/// <c>1987-03-07</c>. Each may be missing, so that <see cref="ParticipantDetails.TryRead"/> checks all three and words
/// each refusal as the Participants screens do.
/// </summary>
/// <remarks>
/// Nothing else is read: any other field in the body, such as a government identifier, is passed over, so none is
/// stored (A5).
/// </remarks>
internal sealed record ParticipantDetailsRequest(string? GivenName, string? FamilyName, DateOnly? DateOfBirth)
{
    /// <summary>
    /// This request with each field it leaves out (<see langword="null"/>) taken from <paramref name="stored"/>, for a
    /// tool that sends only the fields to change. A field sent empty is kept as sent, so its rule refuses it.
    /// </summary>
    public ParticipantDetailsRequest OrStored(Participant stored) =>
        new(GivenName ?? stored.GivenName, FamilyName ?? stored.FamilyName, DateOfBirth ?? stored.DateOfBirth);
}
