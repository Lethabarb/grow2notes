using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The body of <c>PUT /api/admin/participants/{participantId}/goals/order</c> (design.md §6.6), <c>{goalIds}</c>: the
/// IDs of the participant's active goals in their new order, as the screen builds them from the list it last read.
/// </summary>
/// <remarks>
/// Required, so a body with no <c>goalIds</c>, or a <c>null</c> one, gets <c>422 validation.failed</c> on
/// <c>goalIds</c> before the endpoint runs; IDs that are not the active goals, each once, are the handler's
/// <c>422 config.order_mismatch</c>. Public, as the validation source generator describes public types only.
/// </remarks>
public sealed record GoalOrderRequest([property: Required] IReadOnlyList<Guid> GoalIds);
