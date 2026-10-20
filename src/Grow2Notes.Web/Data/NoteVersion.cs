namespace Grow2Notes.Web.Data;

/// <summary>
/// One saved version of a submitted note (design.md §5.3, D15): version 1 is the submission, and each later one an
/// edit, holding the complete text, not a difference. Its groups are in <see cref="NoteVersionGroup"/>, and its goal
/// and common item rows in <see cref="NoteVersionItem"/>. A version is only ever added (§5.8), so the history of a note
/// never changes.
/// </summary>
internal sealed class NoteVersion : ITenantOwned, IAppendOnly
{
    public Guid Id { get; set; }

    /// <summary>
    /// Given by the code that saves the version, the tenant's, and not left to the save to stamp: it is in the
    /// alternate key and both foreign keys.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    public required Guid NoteId { get; set; }

    /// <summary><c>1</c> for the submission; <c>2</c> and on for edits.</summary>
    public required int VersionNumber { get; set; }

    /// <summary>The Guided notes text, which a version always has.</summary>
    public required string Narrative { get; set; }

    public bool IsFlagged { get; set; }

    /// <summary>Null unless the version is flagged.</summary>
    public string? FlagReason { get; set; }

    /// <summary>The author for version 1; the author or a manager after that (D19).</summary>
    public required Guid CreatedByUserId { get; set; }

    /// <summary>The server's time of the save.</summary>
    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// The save's <c>Idempotency-Key</c> header, unique in the organisation, so a retried save finds its first try
    /// rather than adding a second version.
    /// </summary>
    public required Guid IdempotencyKey { get; set; }
}
