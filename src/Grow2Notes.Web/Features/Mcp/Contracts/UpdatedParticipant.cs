namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>update_participant</c> returns (mcp-server.md §5.2): the participant as it is now, with no date of birth.
/// </summary>
/// <param name="GivenName">As stored.</param>
/// <param name="FamilyName">As stored.</param>
/// <param name="Status"><c>active</c>.</param>
/// <param name="Changed">Whether the call changed anything; a call that changes nothing saves nothing.</param>
/// <param name="ExpectedVersion">The participant's version now, which a later change needs.</param>
internal sealed record UpdatedParticipant(
    string GivenName, string FamilyName, string Status, bool Changed, string ExpectedVersion);
