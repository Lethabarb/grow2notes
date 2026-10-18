using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.CommonItems;

/// <summary>
/// The body of <c>PUT /api/admin/common-item-groups/order</c> (design.md §6.6), <c>{groupIds}</c>: the IDs of the
/// active groups other than Every note in their new order, as the screen builds them from the list it last read.
/// </summary>
/// <remarks>
/// Required, so a body with no <c>groupIds</c>, or a <c>null</c> one, gets <c>422 validation.failed</c> on
/// <c>groupIds</c> before the endpoint runs, as the items' reorder does (T07's Notes); IDs that are not the active
/// groups other than Every note, each once, are the handler's <c>422 config.order_mismatch</c>. Public, as the
/// validation source generator describes public types only.
/// </remarks>
public sealed record CommonItemGroupOrderRequest([property: Required] IReadOnlyList<Guid> GroupIds);
