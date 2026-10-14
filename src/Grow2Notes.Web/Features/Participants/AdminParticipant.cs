using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// A participant as <c>GET /api/admin/participants</c> lists them (design.md §6.6), the whole record, for the
/// Participants screens. <see cref="Status"/> is its <see cref="ParticipantStatus"/> name, as the user list gives a
/// user's status; <see cref="DateOfBirth"/> is a calendar date, written <c>2026-10-01</c>; and <see cref="RowVersion"/>
/// is the row's <c>RowVersion</c> as base64, which the SPA sends, in quotes, as <c>If-Match</c> when it changes the
/// participant (D78).
/// </summary>
internal sealed record AdminParticipant(
    Guid Id,
    string GivenName,
    string FamilyName,
    DateOnly DateOfBirth,
    string Status,
    string RowVersion)
{
    public static AdminParticipant Of(Participant participant) =>
        new(
            participant.Id,
            participant.GivenName,
            participant.FamilyName,
            participant.DateOfBirth,
            participant.Status.ToString(),
            RowVersionToken.Of(participant));
}
