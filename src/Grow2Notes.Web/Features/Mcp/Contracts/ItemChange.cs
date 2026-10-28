namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What a tool that adds, rewords, moves, archives or restores a common item returns (mcp-server.md §5.2): the item as
/// it is now.
/// </summary>
/// <param name="ItemId">The item's ID.</param>
/// <param name="GroupId">The ID of the group the item is in now.</param>
/// <param name="Text">As stored.</param>
/// <param name="Archived">Whether the item is archived.</param>
/// <param name="Changed">Whether the call changed anything; a call that changes nothing saves nothing.</param>
/// <param name="ExpectedVersion">The item's version now, which a later change needs.</param>
/// <param name="HelpText">What a change to an item does to the notes (design.md §4.8).</param>
internal sealed record ItemChange(
    Guid ItemId, Guid GroupId, string Text, bool Archived, bool Changed, string ExpectedVersion, string HelpText);
