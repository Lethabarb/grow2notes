namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// A participant that <c>find_participants</c> found, or that <c>archive_participant</c> names: names and status,
/// nothing else.
/// </summary>
/// <param name="ParticipantId">The ID that <c>get_participant</c> takes.</param>
/// <param name="GivenName">As typed (A5).</param>
/// <param name="FamilyName">As typed (A5).</param>
/// <param name="Status"><c>active</c> or <c>archived</c>.</param>
internal sealed record ParticipantMatch(Guid ParticipantId, string GivenName, string FamilyName, string Status);
