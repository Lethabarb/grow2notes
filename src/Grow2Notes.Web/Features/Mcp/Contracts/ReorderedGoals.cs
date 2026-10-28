namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>What <c>reorder_goals</c> returns (mcp-server.md §5.2): the participant's goals as they are now.</summary>
/// <param name="Changed">Whether the call changed anything; an order that is already stored saves nothing.</param>
/// <param name="Goals">
/// The goals exactly as <c>get_participant</c> lists them: the active ones in their new order, then the archived ones,
/// each with its current version, as a reorder changes the version of every goal it moves.
/// </param>
/// <param name="HelpText">What a change to a goal does to the notes (design.md §4.8).</param>
internal sealed record ReorderedGoals(bool Changed, IReadOnlyList<ParticipantGoal> Goals, string HelpText);
