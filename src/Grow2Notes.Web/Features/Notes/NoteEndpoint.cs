using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET /api/participants/{participantId}/notes/{noteDate}</c>, <c>GET {base}</c> (design.md §6.3): a participant's
/// live note for a day, found by its natural key as <see cref="LiveNote"/> finds it, as a <see cref="NoteRead"/>: a
/// draft's header, its status, who started it and when, which tells the shared note address to open the form or to
/// say whose draft it is (note-form.md *Routes*, §4.2), with its author's working copy, read-only, for a manager alone,
/// by the role as stored (§3.4, §4.2's "Someone else's draft"); or a submitted note's current version, the same for a
/// worker and a manager, bar the flag status, the reviews and the number of versions, which are a manager's; or
/// <c>404</c> for no live note, a discarded draft included (§5.5 item 1), and for a participant not in the
/// organisation, another organisation's included (§9.2).
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. The tenant query filter keeps every query to the caller's
/// organisation (§5.9 item 2), and reading writes nothing, not even an audit event, as §5.4 lists none for reading
/// (A28).
/// </remarks>
internal static class NoteEndpoint
{
    /// <summary>
    /// Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not
    /// a GUID, or a date that is not a date written <c>yyyy-MM-dd</c> (<see cref="IsoDateRouteConstraint"/>), matches
    /// no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/participants/{participantId:guid}/notes/{noteDate:isodate}", GetAsync);
        return api;
    }

    private static async Task<Results<Ok<NoteRead>, NotFound>> GetAsync(
        Guid participantId,
        DateOnly noteDate,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        Grow2NotesDbContext db,
        CancellationToken cancellationToken)
    {
        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is { Status: NoteStatus.Draft }
            && await StoredRole.IsManagerAsync(db, Actor.InApp(user, identityOptions.Value), cancellationToken))
        {
            if (await AuthorsWorkingCopyAsync(db, note, cancellationToken) is { } workingCopy)
            {
                return TypedResults.Ok(DraftHeader(note) with { Current = workingCopy });
            }

            // Only a submit takes a draft's working copy away, moving the note on in the same transaction (§5.8 steps
            // 5 and 7), so a submit has committed since the note was found, and the note is read again, submitted.
            note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        }

        if (note is null)
        {
            return TypedResults.NotFound();
        }

        if (note.Status == NoteStatus.Draft)
        {
            return TypedResults.Ok(DraftHeader(note));
        }

        var isManager = await StoredRole.IsManagerAsync(
            db, Actor.InApp(user, identityOptions.Value), cancellationToken);
        return TypedResults.Ok(await SubmittedAsync(db, note, isManager, cancellationToken));
    }

    private static NoteRead DraftHeader(LiveNote note) =>
        new(
            note.Status.ToString(),
            note.AuthorDisplayName,
            Author: null,
            note.CreatedAtUtc,
            FirstSubmittedAtUtc: null,
            note.IsPastDayNote,
            IsEdited: null,
            LastEdit: null,
            Current: null);

    // A draft has its author's working copy alone (§5.3), whose goals and common items keep the snapshot's order and
    // the wording it was started with (A3), however the participant's lists have changed since.
    private static async Task<NoteReadVersion?> AuthorsWorkingCopyAsync(
        Grow2NotesDbContext db, LiveNote note, CancellationToken cancellationToken)
    {
        var draft = await db.NoteDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.NoteId == note.Id && d.EditorUserId == note.AuthorUserId, cancellationToken);
        if (draft is null)
        {
            return null;
        }

        return NoteReadVersion.Of(
            versionNumber: null, NoteSnapshot.Of(draft), draft.Narrative, draft.IsFlagged, draft.FlagReason);
    }

    // The version's own goal and common item rows, in their places, which keep the snapshot's order and wording (§5.3,
    // A3). A group shows when it is Every note or picked and has an item row, the rule that ShownGroups applies to a
    // working copy. A note is edited once it has a version after the first, whose creator and time are its last edit. A
    // manager alone also gets its flag status and every review of it, newest first (§6.3).
    private static async Task<NoteRead> SubmittedAsync(
        Grow2NotesDbContext db, LiveNote note, bool isManager, CancellationToken cancellationToken)
    {
        var content = await NoteVersionContent.ReadAsync(
            db, note.Id, note.CurrentVersionNumber, cancellationToken);
        var isEdited = note.CurrentVersionNumber > 1;
        var lastEdit = isEdited && await LatestVersion.FindAsync(db, note.Id, cancellationToken) is { } latest
            ? new NoteReadLastEdit(latest.CreatedBy, latest.CreatedAtUtc)
            : null;
        return new(
            note.Status.ToString(),
            AuthorDisplayName: null,
            new(note.AuthorUserId, note.AuthorDisplayName),
            note.CreatedAtUtc,
            note.FirstSubmittedAtUtc,
            note.IsPastDayNote,
            isEdited,
            lastEdit,
            NoteReadVersion.Of(
                note.CurrentVersionNumber, content.Snapshot, content.Narrative, content.IsFlagged, content.FlagReason),
            isManager ? note.FlagStatus.ToString() : null,
            isManager ? await NoteReadReview.NewestFirstAsync(db, note.Id, cancellationToken) : null,
            // Versions are numbered 1 to the current number with no gap (§5.8), so no count is needed.
            isManager ? note.CurrentVersionNumber : null);
    }
}
