using Grow2Notes.Web.Data;
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
    // The screen sends only the IDs of the goals it shows, so this answers only a request made some other way.
    private static readonly IReadOnlyDictionary<string, string[]> TickedOutsideTheGoals =
        new Dictionary<string, string[]>
        {
            [nameof(DraftSaveRequest.TickedGoalIds)] = ["Each ticked goal must be one of the note's goals."],
        };

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

        var note = await LiveNoteAsync(participantId, noteDate, cancellationToken);
        if (note is null)
        {
            return await MayOpenANewNoteAsync(actor, noteDate, cancellationToken)
                ? DraftRead.Of(await TemplateAsync(participantId, cancellationToken))
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
    /// note, a draft whose author the actor is, and their working copy, the template's goals its snapshot, with
    /// <c>note.draft_started</c> (§5.5 item 2), if the note may be started; with the actor's own draft, replaces its
    /// ticks, text and flag; with anyone else's draft, saves nothing, as only its author may save it (§6.3), and says
    /// who started it and when. A save sent again after its answer was lost finds the actor's own draft, and updates
    /// it, as does a first save that another of the actor's own first saves beat (§5.5 item 3).
    /// </summary>
    /// <returns>
    /// The time saved; or who started another user's draft, also when that user's first save beat this one; or, for a
    /// save that would create the note, the first of §5.5 item 4's rules that it breaks, in that item's order, then a
    /// lists' version that is missing or no longer the template's (§5.7); or the refusal of a ticked ID that is not
    /// one of the note's goals, the template's for a new note and the snapshot's for a draft, the ticks a set, so that
    /// an ID sent twice is one tick; or <see cref="DraftSaveResult.NotFound"/>. A refusal writes nothing.
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

        var note = await LiveNoteAsync(participantId, noteDate, cancellationToken);
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
                note = await LiveNoteAsync(participantId, noteDate, cancellationToken);
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

        var draft = await db.NoteDrafts
            .SingleOrDefaultAsync(d => d.NoteId == note.Id && d.EditorUserId == actor.UserId, cancellationToken);
        return draft is null ? DraftSaveResult.NotFound : await UpdateAsync(draft, request, cancellationToken);
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

        var goals = await TemplateGoalsAsync(participantId, cancellationToken);
        if (request.ListsVersion != ListsVersion.Of(goals))
        {
            return DraftSaveResult.NotStarted(ErrorCode.ListsChanged);
        }

        var ticked = request.TickedGoalIds.ToHashSet();
        if (!ticked.IsSubsetOf(goals.Select(goal => goal.Id)))
        {
            return DraftSaveResult.Invalid(TickedOutsideTheGoals);
        }

        var now = Now();
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
                Items =
                [
                    .. goals.Select(goal =>
                        new DraftItem(NoteSection.Goal, goal.Id, GroupId: null, goal.Text, ticked.Contains(goal.Id))),
                ],
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

    // The ticks are checked against the snapshot, which keeps the goals' wording as the draft started with it (A3).
    private async Task<DraftSaveResult> UpdateAsync(
        NoteDraft draft, DraftSaveRequest request, CancellationToken cancellationToken)
    {
        var ticked = request.TickedGoalIds.ToHashSet();
        if (!ticked.IsSubsetOf(draft.Items.Where(IsGoal).Select(item => item.SourceId)))
        {
            return DraftSaveResult.Invalid(TickedOutsideTheGoals);
        }

        var now = Now();
        draft.Items =
        [
            .. draft.Items.Select(item =>
                IsGoal(item) ? item with { IsTicked = ticked.Contains(item.SourceId) } : item),
        ];
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

        if (noteDate < today && !await IsManagerAsync(actor, cancellationToken))
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
            && (noteDate >= today.AddDays(-1) || await IsManagerAsync(actor, cancellationToken));
    }

    // The role as stored, named as convention 4 of mcp-server.md §3.5 has it.
    private Task<bool> IsManagerAsync(Actor actor, CancellationToken cancellationToken) =>
        db.Users.AnyAsync(user => user.Id == actor.UserId && user.Role == UserRole.Manager, cancellationToken);

    // The participant's status, or null for no participant of that ID in the tenant's organisation.
    private Task<ParticipantStatus?> ParticipantStatusAsync(Guid participantId, CancellationToken cancellationToken) =>
        db.Participants
            .Where(participant => participant.Id == participantId)
            .Select(participant => (ParticipantStatus?)participant.Status)
            .SingleOrDefaultAsync(cancellationToken);

    // Written as the one-live-note index's filter, [Status] IN (0, 1), so that SQL Server reads it there.
    private Task<LiveNote?> LiveNoteAsync(Guid participantId, DateOnly noteDate, CancellationToken cancellationToken) =>
        (
            from n in db.Notes.AsNoTracking()
            where n.ParticipantId == participantId
                && n.NoteDate == noteDate
                && (n.Status == NoteStatus.Draft || n.Status == NoteStatus.Submitted)
            join author in db.Users on n.AuthorUserId equals author.Id
            select new LiveNote(n.Id, n.Status, n.AuthorUserId, n.CreatedAtUtc, author.DisplayName))
        .SingleOrDefaultAsync(cancellationToken);

    // The template of a new note: the participant's goals as a new draft's snapshot will hold them (§5.7), unticked,
    // with the lists' version.
    private async Task<DraftForm> TemplateAsync(Guid participantId, CancellationToken cancellationToken)
    {
        var goals = await TemplateGoalsAsync(participantId, cancellationToken);
        return new(
            BaseVersion: 0,
            IsSaved: false,
            ListsVersion.Of(goals),
            await GuidePromptsAsync(cancellationToken),
            goals,
            Narrative: "",
            IsFlagged: false,
            FlagReason: null,
            SavedAtUtc: null);
    }

    // The participant's active goals in the order of the manager's list (GoalList.InListOrder), unticked.
    private Task<List<DraftFormGoal>> TemplateGoalsAsync(Guid participantId, CancellationToken cancellationToken) =>
        GoalList
            .InListOrder(db.Goals.AsNoTracking()
                .Where(goal => goal.ParticipantId == participantId && goal.ArchivedAtUtc == null))
            .Select(goal => new DraftFormGoal(goal.Id, goal.Text, false))
            .ToListAsync(cancellationToken);

    // The goals of the working copy's snapshot, with the wording it was started with (A3), however the participant's
    // goals have changed since.
    private static DraftForm Saved(NoteDraft draft, string guidePrompts) =>
        new(
            draft.BaseVersionNumber,
            IsSaved: true,
            ListsVersion: null,
            guidePrompts,
            [.. draft.Items.Where(IsGoal).Select(item => new DraftFormGoal(item.SourceId, item.Text, item.IsTicked))],
            draft.Narrative,
            draft.IsFlagged,
            draft.FlagReason,
            draft.UpdatedAtUtc);

    private static bool IsGoal(DraftItem item) => item.Section == NoteSection.Goal;

    private Task<string> GuidePromptsAsync(CancellationToken cancellationToken) =>
        db.Organisations
            .AsNoTracking()
            .Where(organisation => organisation.Id == tenant.OrganisationId)
            .Select(organisation => organisation.GuidePrompts)
            .SingleAsync(cancellationToken);

    // To the millisecond, as the datetime2(3) columns keep it, so that the savedAtUtc answered is the time stored, not
    // one that SQL Server rounds.
    private DateTime Now()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    /// <summary>The live note of a participant and day, with its author's display name.</summary>
    private sealed record LiveNote(
        Guid Id, NoteStatus Status, Guid AuthorUserId, DateTime CreatedAtUtc, string AuthorDisplayName)
    {
        /// <summary>
        /// Who started the note and when, when it is a draft that another user than <paramref name="actor"/> started,
        /// which only its author may open or save (§6.3); otherwise <see langword="null"/>.
        /// </summary>
        public DraftInProgress? StartedByOtherThan(Actor actor) =>
            Status == NoteStatus.Draft && AuthorUserId != actor.UserId ? new(AuthorDisplayName, CreatedAtUtc) : null;
    }
}
