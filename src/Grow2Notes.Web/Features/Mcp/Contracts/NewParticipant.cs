namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>add_participant</c> returns (mcp-server.md §5.2): the participant as it was added, with no date of birth.
/// </summary>
/// <param name="ParticipantId">The new participant's ID.</param>
/// <param name="GivenName">As stored, with its ends trimmed.</param>
/// <param name="FamilyName">As stored, with its ends trimmed.</param>
/// <param name="Status"><c>active</c>.</param>
/// <param name="ExpectedVersion">The participant's version, which a later change needs.</param>
internal sealed record NewParticipant(
    Guid ParticipantId, string GivenName, string FamilyName, string Status, string ExpectedVersion);
