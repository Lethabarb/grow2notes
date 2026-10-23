namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// A page of the Reviewed list, as <c>GET /api/reviews?status=reviewed</c> answers it (design.md §6.4): at most 30
/// <see cref="ReviewedRow"/>s, newest review first, and whether another review follows them.
/// </summary>
internal sealed record ReviewedPage(IReadOnlyList<ReviewedRow> Items, bool HasMore);
