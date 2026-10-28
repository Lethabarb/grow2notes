namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>reorder_common_items</c> returns (mcp-server.md §5.2): the group's active items as they are now.
/// </summary>
/// <param name="Changed">Whether the call changed anything; an order that is already stored saves nothing.</param>
/// <param name="GroupId">The group whose items were reordered.</param>
/// <param name="Items">The group's active items in their new order, each with its current version.</param>
/// <param name="HelpText">What a change to an item does to the notes (design.md §4.8).</param>
internal sealed record ReorderedItems(
    bool Changed, Guid GroupId, IReadOnlyList<CommonItemEntry> Items, string HelpText);
