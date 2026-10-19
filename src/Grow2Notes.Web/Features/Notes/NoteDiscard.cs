using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The handler of a discard of a participant's draft for a day (design.md §3.4 <i>Discard</i>, §5.5 item 5, §5.8
/// <i>Discard</i>), found by its natural key as <see cref="NoteDrafts"/> finds it, as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST {base}/discard</c> calls it with
/// <see cref="Actor.InApp"/>, for the draft's author and for a manager alike.
/// </summary>
/// <remarks>
/// The discard runs in one transaction (§5.9), so the header's change and <c>note.discarded</c> commit together or not
/// at all. It deletes nothing (A29): the draft's working copy, which holds its content, stays beside the note,
/// read-only (§5.3). The tenant query filter keeps every query, and the header's conditional update, to the tenant's
/// organisation (§5.9 item 2).
/// </remarks>
internal sealed class NoteDiscard(
    Grow2NotesDbContext db, ITenantContext tenant, IAuditWriter audit, TimeProvider timeProvider)
{
    /// <summary>
    /// Discards <paramref name="participantId"/>'s draft for <paramref name="noteDate"/> as <paramref name="actor"/>:
    /// moves the note to Discarded, with now as its discarded time and the actor as who discarded it, by §5.8's one
    /// conditional update, which frees the day (§5.5 item 5); and writes <c>note.discarded</c>, with IDs alone, as the
    /// note row holds who discarded it and when.
    /// </summary>
    /// <returns>
    /// In this order: <see cref="NoteDiscardResult.NotFound"/> for no participant of that ID in the organisation, or
    /// for a day with no note; <see cref="NoteDiscardResult.NotADraft"/> for a submitted note, or a day whose only note
    /// is a discarded draft; <see cref="NoteDiscardResult.NotTheAuthor"/> for a caller who neither started the draft
    /// nor is a manager, by the role as stored; <see cref="NoteDiscardResult.NotADraft"/> when the conditional update
    /// changes no row, as another discard or a submit has moved the note on since it was read; then
    /// <see cref="NoteDiscardResult.Discarded"/>. There is no date or archive check, as any draft may be discarded,
    /// a deactivated author's included (§3.7, §3.8).
    /// </returns>
    public async Task<NoteDiscardResult> DiscardAsync(
        Actor actor, Guid participantId, DateOnly noteDate, CancellationToken cancellationToken)
    {
        // Set by each attempt, as a transient failure runs the whole work again (§5.9). A commit whose answer was lost
        // runs it again too, and finds the draft discarded, so it writes no second event.
        var result = NoteDiscardResult.NotFound;
        await db.InTransactionAsync(
            async ct => result = await DiscardInTransactionAsync(actor, participantId, noteDate, ct),
            cancellationToken);
        return result;
    }

    private async Task<NoteDiscardResult> DiscardInTransactionAsync(
        Actor actor, Guid participantId, DateOnly noteDate, CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return NoteDiscardResult.NotFound;
        }

        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is null)
        {
            return await HasADiscardedDraftAsync(participantId, noteDate, cancellationToken)
                ? NoteDiscardResult.NotADraft
                : NoteDiscardResult.NotFound;
        }

        if (note.Status != NoteStatus.Draft)
        {
            return NoteDiscardResult.NotADraft;
        }

        if (note.AuthorUserId != actor.UserId && !await StoredRole.IsManagerAsync(db, actor, cancellationToken))
        {
            return NoteDiscardResult.NotTheAuthor;
        }

        // ExecuteUpdate bypasses the save interceptors, which is why only the header's conditional updates use it
        // (§5.8); it still applies the tenant query filter.
        var now = StoredTime.UtcNow(timeProvider);
        var discarded = await db.Notes
            .Where(n => n.Id == note.Id && n.Status == NoteStatus.Draft)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(n => n.Status, NoteStatus.Discarded)
                    .SetProperty(n => n.DiscardedAtUtc, now)
                    .SetProperty(n => n.DiscardedByUserId, actor.UserId),
                cancellationToken);
        if (discarded == 0)
        {
            return NoteDiscardResult.NotADraft;
        }

        await audit.WriteAsync(
            tenant.OrganisationId, actor.UserId, AuditEventTypes.Note.Discarded, AuditEntityTypes.Note, note.Id,
            participantId, details: null, cancellationToken);
        return NoteDiscardResult.Discarded;
    }

    private Task<bool> HasADiscardedDraftAsync(
        Guid participantId, DateOnly noteDate, CancellationToken cancellationToken) =>
        db.Notes.AnyAsync(
            n => n.ParticipantId == participantId && n.NoteDate == noteDate && n.Status == NoteStatus.Discarded,
            cancellationToken);
}
