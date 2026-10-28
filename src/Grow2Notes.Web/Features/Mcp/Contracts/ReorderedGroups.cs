namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>reorder_common_item_groups</c> returns (mcp-server.md §5.2): the groups as they are now, exactly as
/// <c>list_common_items</c> lists them without <c>includeArchived</c>.
/// </summary>
/// <param name="Changed">Whether the call changed anything; an order that is already stored saves nothing.</param>
/// <param name="Groups">
/// Every active group with its active items, Every note first, each with its current version, as a reorder changes the
/// version of every group it moves.
/// </param>
/// <param name="HelpText">What a change to a group does to the notes (design.md §4.8).</param>
internal sealed record ReorderedGroups(bool Changed, IReadOnlyList<CommonItemGroupEntry> Groups, string HelpText);
