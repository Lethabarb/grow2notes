namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>add_goal</c> returns (mcp-server.md §5.2): the goal as it was added, at the end of the active goals.
/// </summary>
/// <param name="GoalId">The new goal's ID.</param>
/// <param name="Text">As stored, with its ends trimmed.</param>
/// <param name="ExpectedVersion">The goal's version, which a later change needs.</param>
/// <param name="HelpText">What a change to a goal does to the notes (design.md §4.8).</param>
internal sealed record NewGoal(Guid GoalId, string Text, string ExpectedVersion, string HelpText);
