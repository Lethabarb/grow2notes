using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The body of <c>PUT /api/admin/common-item-groups/{groupId}/items/order</c> (design.md §6.6),
/// <c>{commonItemIds}</c>: the IDs of the group's active items in their new order, as the screen builds them from the
/// list it last read.
/// </summary>
/// <remarks>
/// Required, so a body with no <c>commonItemIds</c>, or a <c>null</c> one, gets <c>422 validation.failed</c> on
/// <c>commonItemIds</c> before the endpoint runs, as the goals' reorder does (T06's Notes); IDs that are not the
/// group's active items, each once, are the handler's <c>422 config.order_mismatch</c>. Public, as the validation
/// source generator describes public types only.
/// </remarks>
public sealed record CommonItemOrderRequest([property: Required] IReadOnlyList<Guid> CommonItemIds);
