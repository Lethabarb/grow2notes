namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What a tool that adds, renames, archives or restores a common item group returns (mcp-server.md §5.2): the group as
/// it is now.
/// </summary>
/// <param name="GroupId">The group's ID.</param>
/// <param name="Name">As stored.</param>
/// <param name="Archived">Whether the group is archived.</param>
/// <param name="Changed">Whether the call changed anything; a call that changes nothing saves nothing.</param>
/// <param name="ExpectedVersion">The group's version now, which a later change needs.</param>
/// <param name="HelpText">What a change to a group does to the notes (design.md §4.8).</param>
internal sealed record GroupChange(
    Guid GroupId, string Name, bool Archived, bool Changed, string ExpectedVersion, string HelpText);
