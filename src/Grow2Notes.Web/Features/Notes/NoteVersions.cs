using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The handler of a version save of a participant's note for a day (design.md §5.8, §6.3), found by its natural key as
/// <see cref="NoteDrafts"/> finds it, as a handler class that takes the <see cref="Actor"/> (mcp-server.md §3.5,
/// convention 1): <c>POST {base}/versions</c> calls it with <see cref="Actor.InApp"/>. It saves version 1, Submit; T14
/// adds Save changes on a submitted note.
/// </summary>
/// <remarks>
/// Each save runs in one transaction (§5.9), so the header's change, the version, its goal rows, the working copy's
/// deletion and <c>note.submitted</c> commit together or not at all. The tenant query filter keeps every query, and
/// the header's conditional update, to the tenant's organisation (§5.9 item 2). Submit leaves the note's
/// <c>FlagStatus</c> and <c>FlaggedVersionNumber</c> as they are until T11 adds §5.8's flag rules.
/// </remarks>
internal sealed class NoteVersions(
    Grow2NotesDbContext db, ITenantContext tenant, IAuditWriter audit, TimeProvider timeProvider)
{
    private const int FirstVersion = 1;

    // The screen makes a key for each opening of its confirmation, so this answers only a request made some other way.
    private static readonly IReadOnlyDictionary<string, string[]> KeyOfAnotherNote = new Dictionary<string, string[]>
    {
        ["IdempotencyKey"] = ["The Idempotency-Key was used for a version of another note."],
    };

    private static readonly IReadOnlyDictionary<string, string[]> FlaggedWithNoReason =
        new Dictionary<string, string[]>
        {
            [nameof(NoteVersionRequest.FlagReason)] = ["A flagged note needs a reason."],
        };

    /// <summary>
    /// Saves <paramref name="request"/> as version 1 of <paramref name="participantId"/>'s note for
    /// <paramref name="noteDate"/>, submitting <paramref name="actor"/>'s own draft (§5.8): moves the note to
    /// Submitted, with version 1 its current version and now its first submitted time, by one conditional update;
    /// adds version 1 with the request's text and flag, a reason only while flagged, and the goals of the working
    /// copy's snapshot, in its order and with its wording (A3), each ticked as the request says; deletes the working
    /// copy (§5.6); and writes <c>note.submitted</c>, with the version number and no text.
    /// </summary>
    /// <param name="actor">Who submits.</param>
    /// <param name="participantId">The note's participant.</param>
    /// <param name="noteDate">The note's date, which the draft keeps whenever it is submitted (§3.3, §3.4).</param>
    /// <param name="idempotencyKey">
    /// The request's <c>Idempotency-Key</c>, which finds the version that an earlier try with it saved, so that a retry
    /// saves no second one.
    /// </param>
    /// <param name="request">The base version, 0, and the note's content.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// In this order: <see cref="NoteVersionResult.NotFound"/> for no participant of that ID in the organisation, or
    /// for no live note; who started another user's draft, which only its author may submit (§6.3); the version saved
    /// by an earlier try with the key, changing nothing, or the refusal of a key that a version of another note has;
    /// <see cref="NoteVersionResult.VersionConflict"/> for a submitted note; the refusal of a flagged note with a
    /// blank reason, or of a ticked ID that is not one of the snapshot's goals, the ticks a set; then the version
    /// saved. A refusal writes nothing. There is no date or archive check, as a draft already started can still be
    /// submitted (§3.7, §5.5 item 4).
    /// </returns>
    public async Task<NoteVersionResult> SaveAsync(
        Actor actor,
        Guid participantId,
        DateOnly noteDate,
        Guid idempotencyKey,
        NoteVersionRequest request,
        CancellationToken cancellationToken)
    {
        // Set by each attempt, as a transient failure runs the whole work again (§5.9). A commit whose answer was lost
        // runs it again too, and the key then finds the version that it saved.
        var result = NoteVersionResult.NotFound;
        await db.InTransactionAsync(
            async ct => result = await SubmitAsync(actor, participantId, noteDate, idempotencyKey, request, ct),
            cancellationToken);
        return result;
    }

    private async Task<NoteVersionResult> SubmitAsync(
        Actor actor,
        Guid participantId,
        DateOnly noteDate,
        Guid idempotencyKey,
        NoteVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return NoteVersionResult.NotFound;
        }

        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is null)
        {
            return NoteVersionResult.NotFound;
        }

        if (note.StartedByOtherThan(actor) is { } inProgress)
        {
            return NoteVersionResult.StartedByOther(inProgress);
        }

        if (await VersionOfKeyAsync(idempotencyKey, cancellationToken) is { } keyed)
        {
            return keyed.NoteId == note.Id
                ? NoteVersionResult.Retried(keyed.Saved)
                : NoteVersionResult.Invalid(KeyOfAnotherNote);
        }

        if (note.Status == NoteStatus.Submitted)
        {
            return NoteVersionResult.VersionConflict;
        }

        if (request.IsFlagged && string.IsNullOrWhiteSpace(request.FlagReason))
        {
            return NoteVersionResult.Invalid(FlaggedWithNoReason);
        }

        // A draft's author has a working copy until its version is saved (§5.6), so with none another submit has saved
        // version 1 since the note was read.
        var draft = await db.NoteDrafts
            .SingleOrDefaultAsync(d => d.NoteId == note.Id && d.EditorUserId == actor.UserId, cancellationToken);
        if (draft is null)
        {
            return await AfterAnotherSaveAsync(note.Id, idempotencyKey, cancellationToken);
        }

        var goals = draft.Items.Where(item => item.Section == NoteSection.Goal).ToList();
        var ticked = request.TickedGoalIds.ToHashSet();
        if (!ticked.IsSubsetOf(goals.Select(goal => goal.SourceId)))
        {
            return NoteVersionResult.Invalid(NoteDrafts.TickedOutsideTheGoals);
        }

        var now = StoredTime.UtcNow(timeProvider);

        // §5.8 step 5, on base 0. ExecuteUpdate bypasses the save interceptors, which is why only this header update
        // uses it; it still applies the tenant query filter.
        var advanced = await db.Notes
            .Where(n => n.Id == note.Id
                && n.CurrentVersionNumber == 0
                && (n.Status == NoteStatus.Draft || n.Status == NoteStatus.Submitted))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(n => n.CurrentVersionNumber, FirstVersion)
                    .SetProperty(n => n.Status, NoteStatus.Submitted)
                    .SetProperty(n => n.FirstSubmittedAtUtc, now),
                cancellationToken);
        if (advanced == 0)
        {
            return await AfterAnotherSaveAsync(note.Id, idempotencyKey, cancellationToken);
        }

        var organisationId = tenant.OrganisationId;
        var version = new NoteVersion
        {
            OrganisationId = organisationId,
            NoteId = note.Id,
            VersionNumber = FirstVersion,
            Narrative = request.Narrative,
            IsFlagged = request.IsFlagged,
            FlagReason = request.IsFlagged ? request.FlagReason : null,
            CreatedByUserId = actor.UserId,
            CreatedAtUtc = now,
            IdempotencyKey = idempotencyKey,
        };
        db.NoteVersions.Add(version);
        db.NoteVersionItems.AddRange(goals.Select((goal, position) => new NoteVersionItem
        {
            NoteVersionId = version.Id,
            Section = NoteSection.Goal,
            Position = position,
            OrganisationId = organisationId,
            GoalId = goal.SourceId,
            Text = goal.Text,
            IsTicked = ticked.Contains(goal.SourceId),
        }));
        db.NoteDrafts.Remove(draft);
        await db.SaveChangesAsync(cancellationToken);

        // The version number alone, as the note row holds its date and author, and an event never copies note text.
        await audit.WriteAsync(
            organisationId, actor.UserId, AuditEventTypes.Note.Submitted, AuditEntityTypes.Note, note.Id,
            participantId, new JsonObject { ["versionNumber"] = FirstVersion }, cancellationToken);
        return NoteVersionResult.Added(new(FirstVersion, now));
    }

    // Another save advanced the note after this one read it: an earlier try with the same key, whose version this is
    // then a retry of, or a save with another key, which made this one's base stale.
    private async Task<NoteVersionResult> AfterAnotherSaveAsync(
        Guid noteId, Guid idempotencyKey, CancellationToken cancellationToken) =>
        await VersionOfKeyAsync(idempotencyKey, cancellationToken) is { } keyed && keyed.NoteId == noteId
            ? NoteVersionResult.Retried(keyed.Saved)
            : NoteVersionResult.VersionConflict;

    // The version that has the key in the tenant's organisation, in which the key is unique (§5.3), with its note.
    private Task<KeyedVersion?> VersionOfKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        db.NoteVersions
            .AsNoTracking()
            .Where(version => version.IdempotencyKey == idempotencyKey)
            .Select(version => new KeyedVersion(
                version.NoteId, new NoteVersionSaved(version.VersionNumber, version.CreatedAtUtc)))
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>A saved version, with the note it is a version of.</summary>
    private sealed record KeyedVersion(Guid NoteId, NoteVersionSaved Saved);
}
