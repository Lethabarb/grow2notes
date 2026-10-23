namespace Grow2Notes.Web.Data;

/// <summary>
/// One manager's review of a flagged note's current version (design.md §5.3, §3.6): Mark reviewed adds a row and moves
/// the note's flag status from To review to Reviewed in one transaction. A review is only ever added, so a note's
/// reviews, with their comments, never change, and a later edit that flags the note again adds to them.
/// </summary>
internal sealed class NoteReview : ITenantOwned, IAppendOnly
{
    public Guid Id { get; set; }

    /// <summary>
    /// Given by the code that saves the review, the tenant's, and not left to the save to stamp: it is in both foreign
    /// keys.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    public required Guid NoteId { get; set; }

    /// <summary>The number of the version that was reviewed, the note's current one when it was marked.</summary>
    public required int VersionNumber { get; set; }

    public required Guid ReviewedByUserId { get; set; }

    /// <summary>The server's time of the review.</summary>
    public required DateTime ReviewedAtUtc { get; set; }

    /// <summary>The manager's optional comment, trimmed; <see langword="null"/> when there is none.</summary>
    public string? Comment { get; set; }
}
