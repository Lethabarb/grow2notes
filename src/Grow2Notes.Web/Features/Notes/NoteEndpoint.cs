using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET /api/participants/{participantId}/notes/{noteDate}</c>, <c>GET {base}</c> (design.md §6.3): a participant's
/// live note for a day, found by its natural key as <see cref="LiveNote"/> finds it, as a <see cref="NoteRead"/>: a
/// draft's status alone, which tells the shared note address to open the form (note-form.md *Routes*), or a submitted
/// note's current version, the same for a worker and a manager; or <c>404</c> for no live note, a discarded draft
/// included (§5.5 item 1), and for a participant not in the organisation, another organisation's included (§9.2).
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
        Guid participantId, DateOnly noteDate, Grow2NotesDbContext db, CancellationToken cancellationToken)
    {
        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is null)
        {
            return TypedResults.NotFound();
        }

        if (note.Status == NoteStatus.Draft)
        {
            return TypedResults.Ok(new NoteRead(note.Status.ToString(), null, null, null));
        }

        // The version's own goal rows, in their places, which keep the snapshot's order and wording (§5.3, A3).
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
        return TypedResults.Ok(new NoteRead(
            note.Status.ToString(), new(note.AuthorDisplayName), note.FirstSubmittedAtUtc, current));
    }
}
