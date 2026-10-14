namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The body of <c>POST /api/admin/participants</c>'s <c>201</c>, <c>{id}</c>: the new participant's ID (D79).
/// </summary>
internal sealed record AddedParticipant(Guid Id);
