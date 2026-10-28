namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>What <c>list_common_items</c> returns (mcp-server.md §5.2): the groups with their items.</summary>
/// <param name="Groups">Every note first, then the other groups in the app's order, the archived ones last.</param>
internal sealed record CommonItemGroups(IReadOnlyList<CommonItemGroupEntry> Groups);
