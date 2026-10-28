namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>What <c>update_goal</c> returns (mcp-server.md §5.2): the goal as it is now.</summary>
/// <param name="Text">As stored.</param>
/// <param name="Changed">Whether the call changed anything; a call that changes nothing saves nothing.</param>
/// <param name="ExpectedVersion">The goal's version now, which a later change needs.</param>
/// <param name="HelpText">What a change to a goal does to the notes (design.md §4.8).</param>
internal sealed record UpdatedGoal(string Text, bool Changed, string ExpectedVersion, string HelpText);
