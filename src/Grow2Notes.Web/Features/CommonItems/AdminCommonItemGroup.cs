using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// A common item group as <c>GET /api/admin/common-item-groups</c> lists it (design.md §6.6), with its items in the
/// list's order, for the Common items screen (§4.9): <see cref="ArchivedAtUtc"/> is <see langword="null"/> while the
/// group is active, and <see cref="RowVersion"/> is the row's <c>RowVersion</c> as base64, which the SPA sends, in
/// quotes, as <c>If-Match</c> when it changes the group (D78).
/// </summary>
internal sealed record AdminCommonItemGroup(
    Guid Id,
    string Name,
    bool IsEveryNote,
    int SortOrder,
    DateTime? ArchivedAtUtc,
    string RowVersion,
    IReadOnlyList<AdminCommonItem> Items)
{
    public static AdminCommonItemGroup Of(CommonItemGroup group, IEnumerable<CommonItem> items) =>
        new(
            group.Id,
            group.Name,
            group.IsEveryNote,
            group.SortOrder,
            group.ArchivedAtUtc,
            RowVersionToken.Of(group),
            [.. items.Select(AdminCommonItem.Of)]);
}
