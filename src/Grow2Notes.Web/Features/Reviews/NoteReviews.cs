using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// The handler of a Mark reviewed on a participant's note for a day (design.md §3.6, §5.8 <i>Review</i>, §6.4), found
/// by its natural key as <see cref="NoteDiscard"/> finds it, as a handler class that takes the <see cref="Actor"/>
/// (mcp-server.md §3.5, convention 1): <c>POST {base}/reviews</c> calls it with <see cref="Actor.InApp"/>.
/// </summary>
/// <remarks>
/// The review runs in one transaction (§5.9), so the header's change, the <see cref="NoteReview"/> row and
/// <c>note.reviewed</c> commit together or not at all. The route's <see cref="Policies.Manager"/> policy is the role
/// check, so the handler reads no stored role. There is no date or archive check, as Save changes has none: a note of
/// an archived participant can be reviewed (§3.7). The tenant query filter keeps every query, and the header's
/// conditional update, to the tenant's organisation (§5.9 item 2).
/// </remarks>
internal sealed class NoteReviews(
    Grow2NotesDbContext db, ITenantContext tenant, IAuditWriter audit, TimeProvider timeProvider)
{
    /// <summary>
    /// Reviews <paramref name="participantId"/>'s note for <paramref name="noteDate"/> as <paramref name="actor"/>:
    /// moves the note from To review to Reviewed by one conditional update, which holds only while the note's current
    /// version is the one in <paramref name="request"/>; adds the <see cref="NoteReview"/>, with its comment trimmed,
    /// null when blank, and its inner line breaks kept; and writes <c>note.reviewed</c>, with the version number alone
    /// (A28).
    /// </summary>
    /// <returns>
    /// <see cref="NoteReviewResult.NotFound"/> for no participant of that ID in the organisation, or for a day with no
    /// live note; <see cref="NoteReviewResult.NotCurrent"/> when the conditional update changes no row, which covers a
    /// note already reviewed, an edited one, a draft and one never flagged alike; then
    /// <see cref="NoteReviewResult.Reviewed"/>.
    /// </returns>
    public async Task<NoteReviewResult> ReviewAsync(
        Actor actor, Guid participantId, DateOnly noteDate, NoteReviewRequest request,
        CancellationToken cancellationToken)
    {
        // Set by each attempt, as a transient failure runs the whole work again (§5.9). A commit whose answer was lost
        // runs it again too, and finds the note reviewed, so it adds no second review.
        var result = NoteReviewResult.NotFound;
        await db.InTransactionAsync(
            async ct => result = await ReviewInTransactionAsync(actor, participantId, noteDate, request, ct),
            cancellationToken);
        return result;
    }

    private async Task<NoteReviewResult> ReviewInTransactionAsync(
        Actor actor, Guid participantId, DateOnly noteDate, NoteReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return NoteReviewResult.NotFound;
        }

        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is null)
        {
            return NoteReviewResult.NotFound;
        }

        // ExecuteUpdate bypasses the save interceptors, which is why only the header's conditional updates use it
        // (§5.8); it still applies the tenant query filter. FlaggedVersionNumber stays, as the review is of that flag.
        var reviewed = await db.Notes
            .Where(n => n.Id == note.Id
                && n.FlagStatus == FlagStatus.ToReview
                && n.CurrentVersionNumber == request.VersionNumber)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.FlagStatus, FlagStatus.Reviewed), cancellationToken);
        if (reviewed == 0)
        {
            return NoteReviewResult.NotCurrent;
        }

        var comment = request.Comment?.Trim();
        var reviewedAtUtc = StoredTime.UtcNow(timeProvider);
        db.NoteReviews.Add(new()
        {
            OrganisationId = tenant.OrganisationId,
            NoteId = note.Id,
            VersionNumber = request.VersionNumber,
            ReviewedByUserId = actor.UserId,
            ReviewedAtUtc = reviewedAtUtc,
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
        });
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(
            tenant.OrganisationId, actor.UserId, AuditEventTypes.Note.Reviewed, AuditEntityTypes.Note, note.Id,
            participantId, new JsonObject { ["versionNumber"] = request.VersionNumber }, cancellationToken);
        return NoteReviewResult.Reviewed(reviewedAtUtc);
    }
}
