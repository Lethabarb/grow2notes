using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// A manager's reading of a submitted note's versions (design.md §6.3): <c>GET {base}/versions</c>, every version as a
/// <see cref="NoteVersionSummary"/>, newest first; and <c>GET {base}/versions/{versionNumber}</c>, one version in the
/// shape of the note's <c>current</c> plus <c>createdBy</c> and <c>createdAtUtc</c>. Both answer <c>404</c> for no live
/// note, a discarded draft, a draft, a participant not in the organisation, and, for one version, a number that the
/// note does not have. A number that is not a whole number from 1 matches no route, so it gets the unknown path's
/// <c>404</c>.
/// </summary>
/// <remarks>
/// <see cref="NoteVersionEndpoints"/> has the <c>POST</c> that saves a version. These have the
/// <see cref="Policies.Manager"/> policy of their own, which gives a worker a <c>403</c> and a request with no session
/// the session cookie's <c>401</c>. The tenant query filter keeps every query to the caller's organisation (§5.9 item
/// 2), and reading writes nothing, not even an audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class NoteVersionHistoryEndpoints
{
    /// <summary>
    /// Maps them in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not a GUID,
    /// or a date that is not a date written <c>yyyy-MM-dd</c> (<see cref="IsoDateRouteConstraint"/>), matches no
    /// route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteVersionHistoryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/participants/{participantId:guid}/notes/{noteDate:isodate}/versions", ListAsync)
            .RequireAuthorization(Policies.Manager);
        api.MapGet(
                "/participants/{participantId:guid}/notes/{noteDate:isodate}/versions/{versionNumber:int:min(1)}",
                GetAsync)
            .RequireAuthorization(Policies.Manager);
        return api;
    }

    private static async Task<Results<Ok<List<NoteVersionSummary>>, NotFound>> ListAsync(
        Guid participantId, DateOnly noteDate, Grow2NotesDbContext db, CancellationToken cancellationToken)
    {
        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is not { Status: NoteStatus.Submitted })
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(await Summaries(db, note.Id, versionNumber: null).ToListAsync(cancellationToken));
    }

    // The summary row comes first: it tells a number the note has from one it does not, which the content read, with a
    // SingleAsync, would throw on, and it holds who saved the version and when.
    private static async Task<Results<Ok<NoteReadVersion>, NotFound>> GetAsync(
        Guid participantId,
        DateOnly noteDate,
        int versionNumber,
        Grow2NotesDbContext db,
        CancellationToken cancellationToken)
    {
        var note = await LiveNote.FindAsync(db, participantId, noteDate, cancellationToken);
        if (note is not { Status: NoteStatus.Submitted })
        {
            return TypedResults.NotFound();
        }

        var summary = await Summaries(db, note.Id, versionNumber).SingleOrDefaultAsync(cancellationToken);
        if (summary is null)
        {
            return TypedResults.NotFound();
        }

        var content = await NoteVersionContent.ReadAsync(db, note.Id, versionNumber, cancellationToken);
        return TypedResults.Ok(
            NoteReadVersion.Of(
                versionNumber, content.Snapshot, content.Narrative, content.IsFlagged, content.FlagReason)
            with
            {
                CreatedBy = summary.CreatedBy,
                CreatedAtUtc = summary.CreatedAtUtc,
            });
    }

    // The filters and the order come before the projection, as EF cannot translate them on a record that a select
    // builds.
    private static IQueryable<NoteVersionSummary> Summaries(Grow2NotesDbContext db, Guid noteId, int? versionNumber) =>
        from version in db.NoteVersions.AsNoTracking()
        where version.NoteId == noteId && (versionNumber == null || version.VersionNumber == versionNumber)
        join creator in db.Users on version.CreatedByUserId equals creator.Id
        orderby version.VersionNumber descending
        select new NoteVersionSummary(
            version.VersionNumber,
            new NoteReadAuthor(creator.Id, creator.DisplayName),
            version.CreatedAtUtc,
            version.IsFlagged);
}
