using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// A common item as <c>GET /api/admin/common-item-groups</c> lists it in its group (design.md §6.6), for the Common
/// items screen (§4.9): <see cref="ArchivedAtUtc"/> is <see langword="null"/> while the item is active, and
/// <see cref="RowVersion"/> is the row's <c>RowVersion</c> as base64, which the SPA sends, in quotes, as
/// <c>If-Match</c> when it changes the item (D78).
/// </summary>
internal sealed record AdminCommonItem(Guid Id, string Text, int SortOrder, DateTime? ArchivedAtUtc, string RowVersion)
{
    public static AdminCommonItem Of(CommonItem item) =>
        new(item.Id, item.Text, item.SortOrder, item.ArchivedAtUtc, RowVersionToken.Of(item));
}
