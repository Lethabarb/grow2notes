namespace Grow2Notes.Web.Data;

/// <summary>
/// One editor's working copy of a note (design.md §5.3, §5.6): a draft, the author's unsubmitted note, or a pending
/// edit on top of a submitted version. Every autosave overwrites it. It holds the lists as they were when it was
/// created, so a later change to a participant's goals or the common items never alters it (§5.7). The app deletes a
/// working copy once its version is saved or its pending edit is cancelled (§5.1 <i>Deletes</i>).
/// </summary>
internal sealed class NoteDraft : ITenantOwned
{
    public required Guid NoteId { get; set; }

    public required Guid EditorUserId { get; set; }

    /// <summary>
    /// Given by the code that creates the working copy, the tenant's, and not left to the save to stamp: it is in both
    /// foreign keys.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    /// <summary><c>0</c> for a draft; <c>n</c> for a pending edit on version <c>n</c>.</summary>
    public int BaseVersionNumber { get; set; }

    /// <summary>
    /// The common item groups as they were when the working copy was created, the Every note group first, then every
    /// active group in order, with the current picks.
    /// </summary>
    public List<DraftGroup> Groups { get; set; } = [];

    /// <summary>
    /// The goals, then the common items, as they were when the working copy was created, with the current ticks.
    /// </summary>
    public List<DraftItem> Items { get; set; } = [];

    /// <summary>The Guided notes text, which may be empty in a draft.</summary>
    public required string Narrative { get; set; }

    public bool IsFlagged { get; set; }

    /// <summary>Null unless the note is flagged.</summary>
    public string? FlagReason { get; set; }

    /// <summary>The browser page that last saved (§5.6).</summary>
    public required Guid LastClientId { get; set; }

    /// <summary>That page's last applied save number.</summary>
    public required int LastSeq { get; set; }

    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>The time of the last save, shown as "Saved 9:42 am".</summary>
    public required DateTime UpdatedAtUtc { get; set; }
}
