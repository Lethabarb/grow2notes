namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// What <see cref="NoteReviews.ReviewAsync"/> did with a Mark reviewed: reviewed the note (<see cref="ReviewedAtUtc"/>),
/// found it not in To review at that version (<see cref="IsNotCurrent"/>), or found no note. The last two write
/// nothing.
/// </summary>
internal sealed record NoteReviewResult
{
    /// <summary>
    /// No participant of that ID is in the tenant's organisation, another organisation's included (design.md §9.2), or
    /// the participant has no live note for the day.
    /// </summary>
    public static readonly NoteReviewResult NotFound = new();

    /// <summary>
    /// The note is not in To review, or its current version is not the one reviewed: already reviewed, by anyone, a
    /// retried press included; edited since it was opened; a draft; or never flagged.
    /// </summary>
    public static readonly NoteReviewResult NotCurrent = new() { IsNotCurrent = true };

    private NoteReviewResult()
    {
    }

    /// <summary>The time of the review, or <see langword="null"/> when nothing was reviewed.</summary>
    public DateTime? ReviewedAtUtc { get; private init; }

    /// <summary>Whether the note was found but not reviewable.</summary>
    public bool IsNotCurrent { get; private init; }

    /// <summary>The request reviewed the note.</summary>
    public static NoteReviewResult Reviewed(DateTime reviewedAtUtc) => new() { ReviewedAtUtc = reviewedAtUtc };
}
