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
/// worker and a manager; or <c>404</c> for no live note, a discarded draft included (§5.5 item 1), and for a
/// participant not in the organisation, another organisation's included (§9.2).
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

        return TypedResults.Ok(note.Status == NoteStatus.Draft
            ? DraftHeader(note)
            : await SubmittedAsync(db, note, cancellationToken));
    }

    private static NoteRead DraftHeader(LiveNote note) =>
        new(
            note.Status.ToString(),
            note.AuthorDisplayName,
            Author: null,
            note.CreatedAtUtc,
            FirstSubmittedAtUtc: null,
            Current: null);

    // A draft has its author's working copy alone (§5.3), whose goals keep the snapshot's order and the wording it was
    // started with (A3), however the participant's goals have changed since.
    private static async Task<NoteReadVersion?> AuthorsWorkingCopyAsync(
        Grow2NotesDbContext db, LiveNote note, CancellationToken cancellationToken)
    {
        var draft = await db.NoteDrafts
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.NoteId == note.Id && d.EditorUserId == note.AuthorUserId, cancellationToken);
        return draft is null
            ? null
            : new(
                draft.Narrative,
                draft.IsFlagged,
                draft.FlagReason,
                [
                    .. draft.Items
                        .Where(item => item.Section == NoteSection.Goal)
                        .Select(item => new NoteReadItem(item.Text, item.IsTicked)),
                ]);
    }

    // The version's own goal rows, in their places, which keep the snapshot's order and wording (§5.3, A3).
    private static async Task<NoteRead> SubmittedAsync(
        Grow2NotesDbContext db, LiveNote note, CancellationToken cancellationToken)
    {
        var current = await db.NoteVersions
            .AsNoTracking()
            .Where(version => version.NoteId == note.Id && version.VersionNumber == note.CurrentVersionNumber)
            .Select(version => new NoteReadVersion(
                version.Narrative,
                version.IsFlagged,
                version.FlagReason,
                db.NoteVersionItems
                    .Where(item => item.NoteVersionId == version.Id && item.Section == NoteSection.Goal)
                    .OrderBy(item => item.Position)
                    .Select(item => new NoteReadItem(item.Text, item.IsTicked))
                    .ToList()))
            .SingleAsync(cancellationToken);
        return new(
            note.Status.ToString(),
            AuthorDisplayName: null,
            new(note.AuthorDisplayName),
            StartedAtUtc: null,
            note.FirstSubmittedAtUtc,
            current);
    }
}
