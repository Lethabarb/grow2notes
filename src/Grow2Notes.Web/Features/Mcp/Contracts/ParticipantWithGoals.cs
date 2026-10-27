namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>get_participant</c> returns (mcp-server.md §5.2): a participant and their goals, with no date of birth and
/// nothing derived from notes.
/// </summary>
/// <param name="ParticipantId">The participant's ID.</param>
/// <param name="GivenName">As typed (A5).</param>
/// <param name="FamilyName">As typed (A5).</param>
/// <param name="Status"><c>active</c> or <c>archived</c>.</param>
/// <param name="ExpectedVersion">The participant's version, the app's ETag token, which a later change needs.</param>
/// <param name="Goals">The active goals in the list's order, then the archived goals in the same order.</param>
internal sealed record ParticipantWithGoals(
    Guid ParticipantId,
    string GivenName,
    string FamilyName,
    string Status,
    string ExpectedVersion,
    IReadOnlyList<ParticipantGoal> Goals);
