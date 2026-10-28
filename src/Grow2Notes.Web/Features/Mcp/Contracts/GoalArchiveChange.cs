namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>archive_goal</c> and <c>restore_goal</c> return (mcp-server.md §5.2): no version, so the goal is looked up
/// again before its next change.
/// </summary>
/// <param name="Changed">Whether the call changed anything; a goal already in that state saves nothing.</param>
/// <param name="HelpText">What a change to a goal does to the notes (design.md §4.8).</param>
internal sealed record GoalArchiveChange(bool Changed, string HelpText);
