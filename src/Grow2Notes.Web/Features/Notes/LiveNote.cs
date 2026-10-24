using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The live note of a participant and day (design.md §5.5 item 1), a draft or submitted, never a discarded draft, with
/// its author's display name, which the note's handlers find by its natural key, so that everyone asking for the same
/// participant and day reaches the same note; and, from its submit on, its first submitted time and the number of its
/// current version, which is <c>0</c> on a draft; and its flag status.
/// </summary>
internal sealed record LiveNote(
    Guid Id,
    NoteStatus Status,
    Guid AuthorUserId,
    DateTime CreatedAtUtc,
    DateOnly NoteDate,
    string AuthorDisplayName,
    DateTime? FirstSubmittedAtUtc,
    int CurrentVersionNumber,
    FlagStatus FlagStatus)
{
    /// <summary>
    /// Every live note, read without tracking, which <see cref="FindAsync"/> narrows to one participant and day, and
    /// Today to one day. The tenant query filter keeps it to the tenant's organisation (§5.9 item 2).
    /// </summary>
    /// <remarks>
    /// The status is written as the one-live-note index's filter, <c>[Status] IN (0, 1)</c>, so that SQL Server reads
    /// a participant's note for a day there.
    /// </remarks>
    public static IQueryable<Note> All(Grow2NotesDbContext db) =>
        db.Notes.AsNoTracking().Where(n => n.Status == NoteStatus.Draft || n.Status == NoteStatus.Submitted);

    /// <summary>
    /// <paramref name="participantId"/>'s live note for <paramref name="noteDate"/>, or <see langword="null"/>.
    /// </summary>
    public static Task<LiveNote?> FindAsync(
        Grow2NotesDbContext db, Guid participantId, DateOnly noteDate, CancellationToken cancellationToken) =>
        (
            from n in All(db)
            where n.ParticipantId == participantId && n.NoteDate == noteDate
            join author in db.Users on n.AuthorUserId equals author.Id
            select new LiveNote(
                n.Id, n.Status, n.AuthorUserId, n.CreatedAtUtc, n.NoteDate, author.DisplayName,
                n.FirstSubmittedAtUtc, n.CurrentVersionNumber, n.FlagStatus))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Whether the note was started on a later Melbourne day than its date, which only a manager can do (D35): derived
    /// on every read from <see cref="CreatedAtUtc"/> and never stored. A worker's draft that crosses midnight is not
    /// one, so the first submitted time is never used.
    /// </summary>
    public bool IsPastDayNote => IsPastDay(CreatedAtUtc, NoteDate);

    /// <summary>Whether a note dated <paramref name="noteDate"/> and started at <paramref name="createdAtUtc"/> is a
    /// past-day note, the rule that <see cref="IsPastDayNote"/> applies and the daily report shares.</summary>
    public static bool IsPastDay(DateTime createdAtUtc, DateOnly noteDate) =>
        MelbourneClock.DateOf(createdAtUtc) > noteDate;

    /// <summary>
    /// Who started the note and when, when it is a draft that another user than <paramref name="actor"/> started,
    /// which only its author may open, save or submit (§6.3); otherwise <see langword="null"/>.
    /// </summary>
    public DraftInProgress? StartedByOtherThan(Actor actor) =>
        Status == NoteStatus.Draft && AuthorUserId != actor.UserId ? new(AuthorDisplayName, CreatedAtUtc) : null;

    /// <summary>
    /// Whether <paramref name="actor"/> may edit the note once it is submitted: its author, or a user whose stored role
    /// is Manager (D19, §6.3). Anyone else is refused, whatever base they send.
    /// </summary>
    public async Task<bool> MayBeEditedByAsync(
        Grow2NotesDbContext db, Actor actor, CancellationToken cancellationToken) =>
        AuthorUserId == actor.UserId || await StoredRole.IsManagerAsync(db, actor, cancellationToken);
}
