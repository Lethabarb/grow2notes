using System.ComponentModel.DataAnnotations;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// The body of <c>POST {base}/reviews</c> (design.md §6.4), Mark reviewed: the version of the note that the manager
/// reviewed, which the screen shows, and their optional comment.
/// </summary>
/// <remarks>
/// <see cref="VersionNumber"/> is at least 1, as a note is only flagged from its first version. <see cref="Comment"/>
/// is at most <see cref="Limits.ReviewComment"/> characters as sent, before it is trimmed. A body that breaks either
/// gets <c>422 validation.failed</c> under its name before the endpoint runs, and writes nothing. Public, as the
/// validation source generator describes public types only.
/// </remarks>
public sealed record NoteReviewRequest(
    [property: Range(1, int.MaxValue)] int VersionNumber,
    [property: MaxLength(Limits.ReviewComment)] string? Comment);
