namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// The answer to a Mark reviewed, <c>{reviewedAtUtc}</c> (design.md §6.4): the server's time of the review, as the
/// <see cref="Data.NoteReview"/> row holds it.
/// </summary>
internal sealed record NoteReviewed(DateTime ReviewedAtUtc);
