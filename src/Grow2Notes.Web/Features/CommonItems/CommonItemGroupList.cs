using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The handler that lists an organisation's common item groups with their items (design.md §6.6), as a handler class
/// that takes the <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>GET /api/admin/common-item-groups</c>
/// calls it with <see cref="Actor.InApp"/>, and the <c>list_common_items</c> tool calls it with
/// <see cref="Channel.Mcp"/>.
/// </summary>
internal sealed class CommonItemGroupList(Grow2NotesDbContext db)
{
    /// <summary>
    /// The tenant's active groups, each with its active items, or with <paramref name="includeArchived"/> the archived
    /// groups and items too, as <see cref="AdminCommonItemGroup"/>s: the groups in the groups' list order, Every note
    /// first (D45), and each group's items in the items' list order. The list is whole, as design.md §6.1 returns
    /// lists.
    /// </summary>
    /// <remarks>
    /// The client never sorts the active groups or items again, as two adds at once can give two rows the same
    /// <c>SortOrder</c>, and each reorder reads its rows in the order that the list shows them. The tenant query filter
    /// keeps both queries to the tenant's organisation (design.md §5.9 item 2). Without
    /// <paramref name="includeArchived"/>, the active items of an archived group are read too, and left out with their
    /// group. Nothing here uses the actor: reading writes no audit event (§5.4, A28). It is taken so that every admin
    /// handler has the same shape.
    /// </remarks>
    public async Task<IReadOnlyList<AdminCommonItemGroup>> ListAsync(
        Actor actor, bool includeArchived, CancellationToken cancellationToken)
    {
        var groups = db.CommonItemGroups.AsNoTracking();
        var items = db.CommonItems.AsNoTracking();
        if (!includeArchived)
        {
            groups = groups.Where(group => group.ArchivedAtUtc == null);
            items = items.Where(item => item.ArchivedAtUtc == null);
        }

        var orderedGroups = await InListOrder(groups).ToListAsync(cancellationToken);

        // A lookup keeps each group's items in the order the query gives them.
        var itemsByGroup = (await InListOrder(items).ToListAsync(cancellationToken)).ToLookup(item => item.GroupId);
        return [.. orderedGroups.Select(group => AdminCommonItemGroup.Of(group, itemsByGroup[group.Id]))];
    }

    /// <summary>
    /// <paramref name="groups"/> in the groups' list order, <c>ORDER BY IsEveryNote DESC, SortOrder, CreatedAtUtc,
    /// Id</c> (design.md §5.3), in which the list shows them and the groups' reorder reads them, so that the order the
    /// screen sends is checked against the one it showed.
    /// </summary>
    public static IQueryable<CommonItemGroup> InListOrder(IQueryable<CommonItemGroup> groups) =>
        groups
            .OrderByDescending(group => group.IsEveryNote)
            .ThenBy(group => group.SortOrder)
            .ThenBy(group => group.CreatedAtUtc)
            .ThenBy(group => group.Id);

    /// <summary>
    /// <paramref name="items"/> in the items' list order, <c>ORDER BY SortOrder, CreatedAtUtc, Id</c>, in which the
    /// list shows each group's items and a group's reorder reads them, so that the order the screen sends is checked
    /// against the one it showed.
    /// </summary>
    public static IQueryable<CommonItem> InListOrder(IQueryable<CommonItem> items) =>
        items.OrderBy(item => item.SortOrder).ThenBy(item => item.CreatedAtUtc).ThenBy(item => item.Id);
}
