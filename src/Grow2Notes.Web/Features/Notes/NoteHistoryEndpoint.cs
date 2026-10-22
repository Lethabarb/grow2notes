using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET /api/participants/{participantId}/notes?before=</c> (design.md §6.3), a participant's history (D20), as a
/// <see cref="NoteHistory"/>: their live notes dated before <c>before</c>, by default tomorrow in Melbourne, so that
/// today's comes first, newest first and 30 at a time, each row as anyone signed in sees it, with its flag status for a
/// manager alone, by the caller's role as stored; or <c>404</c> for a participant not in the organisation, another
/// organisation's included (§9.2). An archived participant's notes are listed too, as they are kept (§3.7).
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. <c>before</c> is bound as a date, so a value that is not one gets the
/// framework's <c>400</c>. The tenant query filter keeps every query to the caller's organisation (§5.9 item 2), and
/// reading writes nothing, not even an audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class NoteHistoryEndpoint
{
    // design.md §4.4 item 2: "30 at a time, then Show older".
    private const int PageSize = 30;

    /// <summary>
    /// Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns. An ID that is not
    /// a GUID matches no route, so it gets the unknown path's <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapNoteHistoryEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/participants/{participantId:guid}/notes", GetAsync);
        return api;
    }

    // One live note per participant and day (§5.5 item 1), so the note date orders the rows and is the next page's
    // cursor. One row more than a page is read, which says whether older notes remain without counting them. A draft
    // has no version, so its current version number, 0, matches none, and it is never flagged.
    private static async Task<Results<Ok<NoteHistory>, NotFound>> GetAsync(
        Guid participantId,
        DateOnly? before,
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        Grow2NotesDbContext db,
        MelbourneClock clock,
        CancellationToken cancellationToken)
    {
        if (!await db.Participants.AnyAsync(participant => participant.Id == participantId, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        var cursor = before ?? clock.Today().AddDays(1);
        var rows = await (
            from n in LiveNote.All(db)
            where n.ParticipantId == participantId && n.NoteDate < cursor
            join author in db.Users on n.AuthorUserId equals author.Id
            orderby n.NoteDate descending
            select new
            {
                n.NoteDate,
                n.Status,
                author.DisplayName,
                n.CurrentVersionNumber,
                IsFlagged = db.NoteVersions.Any(
                    v => v.NoteId == n.Id && v.VersionNumber == n.CurrentVersionNumber && v.IsFlagged),
                n.FlagStatus,
            })
            .Take(PageSize + 1)
            .ToListAsync(cancellationToken);
        var isManager = await StoredRole.IsManagerAsync(
            db, Actor.InApp(user, identityOptions.Value), cancellationToken);
        return TypedResults.Ok(new NoteHistory(
            [
                .. rows.Take(PageSize).Select(row => new NoteHistoryRow(
                    row.NoteDate,
                    row.Status.ToString(),
                    row.DisplayName,
                    row.CurrentVersionNumber,
                    row.CurrentVersionNumber > 1,
                    row.IsFlagged,
                    isManager ? row.FlagStatus.ToString() : null)),
            ],
            rows.Count > PageSize));
    }
}
