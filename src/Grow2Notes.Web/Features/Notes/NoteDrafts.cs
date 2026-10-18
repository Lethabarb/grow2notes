using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The handler of a participant's note for a day as its form opens it (design.md §5.5 to §5.7, §6.3), found by its
/// natural key, the participant and the note date, so that everyone asking for the same participant and day reaches
/// the same note, as a handler class that takes the <see cref="Actor"/> (mcp-server.md §3.5, convention 1):
/// <c>GET {base}/draft</c> calls it with <see cref="Actor.InApp"/>.
/// </summary>
/// <remarks>
/// It reads the live note alone, a draft or a submitted note, never a discarded draft (§5.5 item 1). The tenant query
/// filter keeps every query to the tenant's organisation (§5.9 item 2), and reading writes nothing, not even an audit
/// event (§5.4, A28): the first change creates the note (§3.3).
/// </remarks>
internal sealed class NoteDrafts(Grow2NotesDbContext db, ITenantContext tenant, MelbourneClock clock)
{
    /// <summary>
    /// The note form for <paramref name="actor"/> on <paramref name="participantId"/>'s note for
    /// <paramref name="noteDate"/>: with no live note, the template of a new note, unless the date is after today in
    /// Melbourne, when no note can exist (§5.5 item 4); with the actor's own draft, their working copy; with anyone
    /// else's draft, who started it and when, as only its author may open it (§6.3).
    /// </summary>
    public async Task<DraftRead> ReadAsync(
        Actor actor, Guid participantId, DateOnly noteDate, CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return DraftRead.NotFound;
        }

        // Written as the one-live-note index's filter, [Status] IN (0, 1), so that SQL Server reads it there.
        var note = await (
                from n in db.Notes.AsNoTracking()
                where n.ParticipantId == participantId
                    && n.NoteDate == noteDate
                    && (n.Status == NoteStatus.Draft || n.Status == NoteStatus.Submitted)
                join author in db.Users on n.AuthorUserId equals author.Id
                select new { n.Id, n.Status, n.AuthorUserId, n.CreatedAtUtc, AuthorDisplayName = author.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        if (note is null)
        {
            return noteDate > clock.Today()
                ? DraftRead.NotFound
                : DraftRead.Of(await TemplateAsync(participantId, cancellationToken));
        }

        if (note.Status == NoteStatus.Draft && note.AuthorUserId != actor.UserId)
        {
            return DraftRead.StartedByOther(new(note.AuthorDisplayName, note.CreatedAtUtc));
        }

        var draft = await db.NoteDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.NoteId == note.Id && d.EditorUserId == actor.UserId, cancellationToken);
        return draft is null
            ? DraftRead.NotFound
            : DraftRead.Of(Saved(draft, await GuidePromptsAsync(cancellationToken)));
    }

    // The participant's active goals in the order of the manager's list (GoalList.InListOrder), unticked, as a new
    // draft's snapshot will hold them (§5.7), with the lists' version.
    private async Task<DraftForm> TemplateAsync(Guid participantId, CancellationToken cancellationToken)
    {
        List<DraftFormGoal> goals = await GoalList
            .InListOrder(db.Goals.AsNoTracking()
                .Where(goal => goal.ParticipantId == participantId && goal.ArchivedAtUtc == null))
            .Select(goal => new DraftFormGoal(goal.Id, goal.Text, false))
            .ToListAsync(cancellationToken);
        return new(
            BaseVersion: 0,
            IsSaved: false,
            ListsVersion.Of(goals),
            await GuidePromptsAsync(cancellationToken),
            goals,
            Narrative: "",
            SavedAtUtc: null);
    }

    // The goals of the working copy's snapshot, with the wording it was started with (A3), however the participant's
    // goals have changed since.
    private static DraftForm Saved(NoteDraft draft, string guidePrompts) =>
        new(
            draft.BaseVersionNumber,
            IsSaved: true,
            ListsVersion: null,
            guidePrompts,
            [
                .. draft.Items
                    .Where(item => item.Section == NoteSection.Goal)
                    .Select(item => new DraftFormGoal(item.SourceId, item.Text, item.IsTicked)),
            ],
            draft.Narrative,
            draft.UpdatedAtUtc);

    private Task<string> GuidePromptsAsync(CancellationToken cancellationToken) =>
        db.Organisations
            .AsNoTracking()
            .Where(organisation => organisation.Id == tenant.OrganisationId)
            .Select(organisation => organisation.GuidePrompts)
            .SingleAsync(cancellationToken);
}
