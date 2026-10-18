namespace Grow2Notes.Web.Data;

/// <summary>
/// The header of one participant's note for one day (design.md §5.3). Its content is in the working copies
/// (<see cref="NoteDraft"/>) and, once submitted, in its versions (<see cref="NoteVersion"/>). At most one note per
/// participant and day is live, a draft or submitted; discarded drafts stay beside it (§5.5, D6). Nothing deletes a
/// note (A29).
/// </summary>
internal sealed class Note : ITenantOwned
{
    public Guid Id { get; set; }

    /// <summary>
    /// Given by the code that creates the note, the tenant's, and not left to the save to stamp: it is in the alternate
    /// key and every foreign key.
    /// </summary>
    public required Guid OrganisationId { get; set; }

    /// <summary>Fixed when the note is created (<see cref="AppendOnlySaveChangesInterceptor"/>).</summary>
    public required Guid ParticipantId { get; set; }

    /// <summary>
    /// The Melbourne calendar date the note is for (§3.3). Fixed when the note is created
    /// (<see cref="AppendOnlySaveChangesInterceptor"/>).
    /// </summary>
    public required DateOnly NoteDate { get; set; }

    /// <summary>
    /// The user who made the first change: the worker for today's note, or the manager for a past-day note (D35).
    /// Fixed when the note is created (<see cref="AppendOnlySaveChangesInterceptor"/>).
    /// </summary>
    public required Guid AuthorUserId { get; set; }

    public NoteStatus Status { get; set; }

    /// <summary>
    /// The time of the first change. The note is a past-day note when this, in Melbourne, is after
    /// <see cref="NoteDate"/>.
    /// </summary>
    public required DateTime CreatedAtUtc { get; set; }

    /// <summary>Set once, by version 1: the submitted time shown beside the note date.</summary>
    public DateTime? FirstSubmittedAtUtc { get; set; }

    /// <summary><c>0</c> until submitted, then the latest version's number.</summary>
    public int CurrentVersionNumber { get; set; }

    public FlagStatus FlagStatus { get; set; }

    /// <summary>
    /// The version that last put the note into To review, whose reason and time the Flagged list shows, even when a
    /// later version removed the tick.
    /// </summary>
    public int? FlaggedVersionNumber { get; set; }

    public DateTime? DiscardedAtUtc { get; set; }

    /// <summary>The author or a manager.</summary>
    public Guid? DiscardedByUserId { get; set; }
}
