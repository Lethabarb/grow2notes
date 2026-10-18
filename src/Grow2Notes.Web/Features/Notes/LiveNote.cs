using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The live note of a participant and day (design.md §5.5 item 1), a draft or submitted, never a discarded draft, with
/// its author's display name, which the note's handlers find by its natural key, so that everyone asking for the same
/// participant and day reaches the same note.
/// </summary>
internal sealed record LiveNote(
    Guid Id, NoteStatus Status, Guid AuthorUserId, DateTime CreatedAtUtc, string AuthorDisplayName)
{
    /// <summary>
    /// <paramref name="participantId"/>'s live note for <paramref name="noteDate"/>, or <see langword="null"/>. The
    /// tenant query filter keeps it to the tenant's organisation (§5.9 item 2).
    /// </summary>
    /// <remarks>
    /// The status is written as the one-live-note index's filter, <c>[Status] IN (0, 1)</c>, so that SQL Server reads
    /// the note there.
    /// </remarks>
    public static Task<LiveNote?> FindAsync(
        Grow2NotesDbContext db, Guid participantId, DateOnly noteDate, CancellationToken cancellationToken) =>
        (
            from n in db.Notes.AsNoTracking()
            where n.ParticipantId == participantId
                && n.NoteDate == noteDate
                && (n.Status == NoteStatus.Draft || n.Status == NoteStatus.Submitted)
            join author in db.Users on n.AuthorUserId equals author.Id
            select new LiveNote(n.Id, n.Status, n.AuthorUserId, n.CreatedAtUtc, author.DisplayName))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Who started the note and when, when it is a draft that another user than <paramref name="actor"/> started,
    /// which only its author may open, save or submit (§6.3); otherwise <see langword="null"/>.
    /// </summary>
    public DraftInProgress? StartedByOtherThan(Actor actor) =>
        Status == NoteStatus.Draft && AuthorUserId != actor.UserId ? new(AuthorDisplayName, CreatedAtUtc) : null;
}
