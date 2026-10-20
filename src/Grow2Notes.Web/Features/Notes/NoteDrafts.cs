using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.CommonItems;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The handler of a participant's note for a day as its form opens and autosaves it (design.md §5.5 to §5.7, §6.3),
/// found by its natural key, the participant and the note date, so that everyone asking for the same participant and
/// day reaches the same note, as a handler class that takes the <see cref="Actor"/> (mcp-server.md §3.5, convention 1):
/// <c>GET</c> and <c>PUT {base}/draft</c> call it with <see cref="Actor.InApp"/>.
/// </summary>
/// <remarks>
/// It reaches the live note alone, a draft or a submitted note, never a discarded draft (§5.5 item 1). The tenant query
/// filter keeps every query to the tenant's organisation (§5.9 item 2). Reading writes nothing, not even an audit
/// event (§5.4, A28): the first save creates the note (§3.3), with <c>note.draft_started</c>, and later saves are not
/// audited (§5.3).
/// </remarks>
internal sealed class NoteDrafts(
    Grow2NotesDbContext db,
    ITenantContext tenant,
    MelbourneClock clock,
    IAuditWriter audit,
    TimeProvider timeProvider)
{
    /// <summary>
    /// The note form for <paramref name="actor"/> on <paramref name="participantId"/>'s note for
    /// <paramref name="noteDate"/>: with no live note, the template of a new note, unless the date is after today in
    /// Melbourne, when no note can exist (§5.5 item 4), or, for an actor who is not a manager, before yesterday (A65);
    /// with the actor's own draft, their working copy; with anyone else's draft, who started it and when, as only its
    /// author may open it (§6.3).
    /// </summary>
    public async Task<DraftRead> ReadAsync(
        Actor actor, Guid participantId, DateOnly noteDate, CancellationToken cancellationToken)
    {
        if (await ParticipantStatusAsync(participantId, cancellationToken) is null)
        {
            return DraftRead.NotFound;
        }

        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is null)
        {
            return await MayOpenANewNoteAsync(actor, noteDate, cancellationToken)
                ? DraftRead.Of(await TemplateAsync(participantId, noteDate, cancellationToken))
                : DraftRead.NotFound;
        }

        if (note.StartedByOtherThan(actor) is { } inProgress)
        {
            return DraftRead.StartedByOther(inProgress);
        }

        var draft = await db.NoteDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.NoteId == note.Id && d.EditorUserId == actor.UserId, cancellationToken);
        return draft is null
            ? DraftRead.NotFound
            : DraftRead.Of(Saved(draft, await GuidePromptsAsync(cancellationToken)));
    }

    /// <summary>
    /// Saves <paramref name="request"/> as <paramref name="actor"/>'s working copy of
    /// <paramref name="participantId"/>'s note for <paramref name="noteDate"/> (§5.6): with no live note, creates the
    /// note, a draft whose author the actor is, and their working copy, the template its snapshot, with
    /// <c>note.draft_started</c> (§5.5 item 2), if the note may be started; with the actor's own draft, replaces its
    /// picks, ticks, text and flag as §5.6 numbers a page's saves, the working copy keeping the page that saved last
    /// and that page's last number: a save of that page's numbered after it, or the first save of another page, which
    /// takes the working copy over; with anyone else's draft, saves nothing, as only its author may save it (§6.3), and
    /// says who started it and when; with a submitted note, on which the actor has no working copy until T14 gives
    /// pending edits, saves nothing, as a draft's base, 0, is stale (§6.7), and so recreates no working copy. A save
    /// sent again after its answer was lost finds the actor's own draft, with its page and number, as does a first save
    /// that another of the actor's own first saves beat (§5.5 item 3), and is decided as a later save, never as a
    /// conflict.
    /// </summary>
    /// <returns>
    /// The time saved; or, for a save of the page that saved last numbered at or below its last, which the working
    /// copy holds already, the time of the save that it holds, changing nothing; or who started another user's draft,
    /// also when that user's first save beat this one; or, for a save that would create the note, the first of §5.5
    /// item 4's rules that it breaks, in that item's order, then a lists' version that is missing or no longer the
    /// template's (§5.7); or <see cref="DraftSaveResult.VersionConflict"/> for a submitted note, also when a submit
    /// deleted the working copy after this save found the draft; or <see cref="DraftSaveResult.TakenOver"/> for any
    /// save but the first from a page other than the one that saved last; or the refusal of a tick or a pick that the
    /// snapshot refuses (<see cref="NoteSnapshot.RefusalOf"/>), the template's for a new note and the snapshot's for
    /// a draft, so that an ID sent twice is one tick; or <see cref="DraftSaveResult.NotFound"/>. A refusal writes
    /// nothing.
    /// </returns>
    public async Task<DraftSaveResult> SaveAsync(
        Actor actor,
        Guid participantId,
        DateOnly noteDate,
        DraftSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (await ParticipantStatusAsync(participantId, cancellationToken) is not { } status)
        {
            return DraftSaveResult.NotFound;
        }

        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is null)
        {
            try
            {
                return await StartAsync(actor, participantId, status, noteDate, request, cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The one-live-note index refused the note (§5.5 item 3): another first save for the participant and
                // day committed after this one found no live note. The rolled-back rows are still tracked as added.
                db.ChangeTracker.Clear();
                note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
                if (note is null)
                {
                    throw;
                }
            }
        }

        if (note.StartedByOtherThan(actor) is { } inProgress)
        {
            return DraftSaveResult.StartedByOther(inProgress);
        }

        // Set by each attempt, as a transient failure runs the whole work again (§5.9). A commit whose answer was lost
        // runs it again too, and the save's number then finds it held.
        var result = DraftSaveResult.VersionConflict;
        await db.InTransactionAsync(
            async ct => result = await UpdateAsync(note.Id, actor, request, ct), cancellationToken);
        return result;
    }

    // The note and the working copy go in one save, and the event in the audit writer's, so the transaction is what
    // keeps them together: an event that fails to save leaves neither row.
    private async Task<DraftSaveResult> StartAsync(
        Actor actor,
        Guid participantId,
        ParticipantStatus status,
        DateOnly noteDate,
        DraftSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (await StartRefusalAsync(actor, status, noteDate, cancellationToken) is { } refusal)
        {
            return DraftSaveResult.NotStarted(refusal);
        }

        var template = await TemplateSnapshotAsync(participantId, cancellationToken);
        if (request.ListsVersion != ListsVersion.Of(template))
        {
            return DraftSaveResult.NotStarted(ErrorCode.ListsChanged);
        }

        if (template.RefusalOf(request) is { } invalid)
        {
            return DraftSaveResult.Invalid(invalid);
        }

        var snapshot = template.With(request);

        var now = StoredTime.UtcNow(timeProvider);
        var organisationId = tenant.OrganisationId;
        await db.InTransactionAsync(async ct =>
        {
            var note = new Note
            {
                OrganisationId = organisationId,
                ParticipantId = participantId,
                NoteDate = noteDate,
                AuthorUserId = actor.UserId,
                Status = NoteStatus.Draft,
                CreatedAtUtc = now,
            };
            db.Notes.Add(note);
            db.NoteDrafts.Add(new NoteDraft
            {
                NoteId = note.Id,
                EditorUserId = actor.UserId,
                OrganisationId = organisationId,
                Groups = [.. snapshot.Groups],
                Items = [.. snapshot.Items],
                Narrative = request.Narrative,
                IsFlagged = request.IsFlagged,
                FlagReason = FlagReasonOf(request),
                LastClientId = request.ClientId,
                LastSeq = request.Seq,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            await db.SaveChangesAsync(ct);

            // IDs alone, as the note row holds its date and author, and a draft has no version number (§5.3).
            await audit.WriteAsync(
                organisationId, actor.UserId, AuditEventTypes.Note.DraftStarted, AuditEntityTypes.Note, note.Id,
                participantId, details: null, ct);
        }, cancellationToken);
        return DraftSaveResult.Started(new(now));
    }

    // In the save's transaction. The working copy is read under an update lock, held until the transaction ends, so
    // that two saves of it at once, such as a page-hidden save beside the save in flight, are decided one after the
    // other, the second on what the first saved, and the higher number wins whichever comes first (autosave-status.md
    // *Behaviour* 1). A submit that has deleted the working copy holds the read until it commits, so that a save the
    // submit beat finds none, rather than updating a row deleted since: a plain read would not wait, as Azure SQL
    // reads committed snapshots. The picks and ticks are checked against the snapshot, which keeps the wording as the
    // draft started with it (A3).
    private async Task<DraftSaveResult> UpdateAsync(
        Guid noteId, Actor actor, DraftSaveRequest request, CancellationToken cancellationToken)
    {
        var draft = await db.NoteDrafts
            .FromSql($"""
                SELECT * FROM [NoteDraft] WITH (UPDLOCK)
                WHERE [NoteId] = {noteId} AND [EditorUserId] = {actor.UserId}
                """)
            .SingleOrDefaultAsync(cancellationToken);

        // A draft's author has a working copy until its version is saved (§5.6), so with none the note is submitted,
        // and a draft's base, 0, is stale (§6.7).
        if (draft is null)
        {
            return DraftSaveResult.VersionConflict;
        }

        if (draft.LastClientId == request.ClientId)
        {
            // The page's save sent again after its answer was lost, or one that a later save of the page overtook.
            if (request.Seq <= draft.LastSeq)
            {
                return DraftSaveResult.Unchanged(new(draft.UpdatedAtUtc));
            }
        }
        else if (request.Seq != 1)
        {
            // Another page has saved since this one last did. Only a page that has not saved the note before, such as
            // a second tab or another device, takes the working copy over.
            return DraftSaveResult.TakenOver;
        }

        var current = NoteSnapshot.Of(draft);
        if (current.RefusalOf(request) is { } invalid)
        {
            return DraftSaveResult.Invalid(invalid);
        }

        var now = StoredTime.UtcNow(timeProvider);
        var saved = current.With(request);
        draft.Groups = [.. saved.Groups];
        draft.Items = [.. saved.Items];
        draft.Narrative = request.Narrative;
        draft.IsFlagged = request.IsFlagged;
        draft.FlagReason = FlagReasonOf(request);
        draft.LastClientId = request.ClientId;
        draft.LastSeq = request.Seq;
        draft.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return DraftSaveResult.Updated(new(now));
    }

    // The reason as sent while the note is flagged, a missing one included, as only Submit needs one (§3.4); never
    // while it is not, as an unflagged note has no reason (§5.3).
    private static string? FlagReasonOf(DraftSaveRequest request) => request.IsFlagged ? request.FlagReason : null;

    // §5.5 item 4's rules for creating a note, in its order: no date after today in Melbourne, for anyone; no day
    // before it but by a manager; no archived participant.
    private async Task<ErrorCode?> StartRefusalAsync(
        Actor actor, ParticipantStatus status, DateOnly noteDate, CancellationToken cancellationToken)
    {
        var today = clock.Today();
        if (noteDate > today)
        {
            return ErrorCode.FutureDate;
        }

        if (noteDate < today && !await StoredRole.IsManagerAsync(db, actor, cancellationToken))
        {
            return ErrorCode.PastDateManagerOnly;
        }

        return status == ParticipantStatus.Archived ? ErrorCode.ParticipantArchived : null;
    }

    // Whether a new note's form opens (A65): never for a date after today, and for a day before yesterday only to a
    // manager, as a worker's first save there could only be refused with the after-midnight sentence, which names
    // yesterday (§3.3). Yesterday's opens to a worker too: the sentence is true of a first save there.
    private async Task<bool> MayOpenANewNoteAsync(Actor actor, DateOnly noteDate, CancellationToken cancellationToken)
    {
        var today = clock.Today();
        return noteDate <= today
            && (noteDate >= today.AddDays(-1) || await StoredRole.IsManagerAsync(db, actor, cancellationToken));
    }

    // The participant's status, or null for no participant of that ID in the tenant's organisation.
    private Task<ParticipantStatus?> ParticipantStatusAsync(Guid participantId, CancellationToken cancellationToken) =>
        db.Participants
            .Where(participant => participant.Id == participantId)
            .Select(participant => (ParticipantStatus?)participant.Status)
            .SingleOrDefaultAsync(cancellationToken);

    // The template of a new note: the lists as a new draft's snapshot will hold them (§5.7), with the lists' version,
    // which the picks do not change, and the groups that the participant's latest earlier note picked picked.
    private async Task<DraftForm> TemplateAsync(
        Guid participantId, DateOnly noteDate, CancellationToken cancellationToken)
    {
        var template = await TemplateSnapshotAsync(participantId, cancellationToken);
        var startingPicks = await StartingPicksAsync(participantId, noteDate, cancellationToken);
        return FormOf(
            template.WithStartingPicks(startingPicks),
            baseVersion: 0,
            isSaved: false,
            ListsVersion.Of(template),
            await GuidePromptsAsync(cancellationToken),
            narrative: "",
            isFlagged: false,
            flagReason: null,
            savedAtUtc: null);
    }

    // The lists a new draft's snapshot will hold (§5.7), all unpicked and unticked: the participant's active goals in
    // the order of the manager's list (GoalList.InListOrder); the active groups in the groups' list order, Every note
    // first and empty groups included; and the active items group by group, in the items' list order.
    private async Task<NoteSnapshot> TemplateSnapshotAsync(Guid participantId, CancellationToken cancellationToken)
    {
        var goals = await GoalList
            .InListOrder(db.Goals.AsNoTracking()
                .Where(goal => goal.ParticipantId == participantId && goal.ArchivedAtUtc == null))
            .ToListAsync(cancellationToken);
        var groups = await CommonItemGroupList
            .InListOrder(db.CommonItemGroups.AsNoTracking().Where(group => group.ArchivedAtUtc == null))
            .ToListAsync(cancellationToken);

        // A lookup keeps each group's items in the order the query gives them.
        var itemsByGroup = (await CommonItemGroupList
            .InListOrder(db.CommonItems.AsNoTracking().Where(item => item.ArchivedAtUtc == null))
            .ToListAsync(cancellationToken))
            .ToLookup(item => item.GroupId);
        return new(
            [.. groups.Select(group => new DraftGroup(group.Id, group.Name, group.IsEveryNote, IsPicked: false))],
            [
                .. goals.Select(goal =>
                    new DraftItem(NoteSection.Goal, goal.Id, GroupId: null, goal.Text, IsTicked: false)),
                .. groups.SelectMany(group => itemsByGroup[group.Id].Select(item =>
                    new DraftItem(NoteSection.CommonItem, item.Id, group.Id, item.Text, IsTicked: false))),
            ]);
    }

    // The IDs of the groups picked in the current version of the participant's latest submitted note dated before
    // noteDate, by anyone (S02.05.03). Nothing keeps where they came from (D83).
    private async Task<HashSet<Guid>> StartingPicksAsync(
        Guid participantId, DateOnly noteDate, CancellationToken cancellationToken) =>
        (await (
            from note in db.Notes
                .AsNoTracking()
                .Where(n => n.ParticipantId == participantId
                    && n.Status == NoteStatus.Submitted
                    && n.NoteDate < noteDate)
                .OrderByDescending(n => n.NoteDate)
                .Take(1)
            join version in db.NoteVersions on note.Id equals version.NoteId
            where version.VersionNumber == note.CurrentVersionNumber
            join pick in db.NoteVersionGroups on version.Id equals pick.NoteVersionId
            where pick.IsPicked
            select pick.CommonItemGroupId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    // The working copy's snapshot, with the names and wording it was started with (A3), however the lists have changed
    // since.
    private static DraftForm Saved(NoteDraft draft, string guidePrompts) =>
        FormOf(
            NoteSnapshot.Of(draft),
            draft.BaseVersionNumber,
            isSaved: true,
            listsVersion: null,
            guidePrompts,
            draft.Narrative,
            draft.IsFlagged,
            draft.FlagReason,
            draft.UpdatedAtUtc);

    private static DraftForm FormOf(
        NoteSnapshot snapshot,
        int baseVersion,
        bool isSaved,
        string? listsVersion,
        string guidePrompts,
        string narrative,
        bool isFlagged,
        string? flagReason,
        DateTime? savedAtUtc) =>
        new(
            baseVersion,
            isSaved,
            listsVersion,
            guidePrompts,
            [.. snapshot.Goals.Select(FormItemOf)],
            [
                .. snapshot.Groups.Select(group => new DraftFormGroup(
                    group.GroupId,
                    group.Name,
                    group.IsEveryNote,
                    group.IsPicked,
                    [.. snapshot.ItemsOf(group).Select(FormItemOf)])),
            ],
            narrative,
            isFlagged,
            flagReason,
            savedAtUtc);

    private static DraftFormItem FormItemOf(DraftItem item) => new(item.SourceId, item.Text, item.IsTicked);

    private Task<string> GuidePromptsAsync(CancellationToken cancellationToken) =>
        db.Organisations
            .AsNoTracking()
            .Where(organisation => organisation.Id == tenant.OrganisationId)
            .Select(organisation => organisation.GuidePrompts)
            .SingleAsync(cancellationToken);
}
