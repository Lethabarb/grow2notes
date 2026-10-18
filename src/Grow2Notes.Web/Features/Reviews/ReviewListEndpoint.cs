using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// <c>GET /api/reviews?status=toReview</c> (design.md §6.4), the Flagged screen's To review list (§4.6 item 2), as
/// <see cref="ToReviewRow"/>s: the organisation's submitted notes in To review, each with the reason and time of its
/// flagged version, oldest flag first, returned whole (§6.1 <i>Lists</i>); or <c>400</c> for a request with no
/// <c>status</c> or another value. T19 adds <c>status=reviewed</c>, the Reviewed list.
/// </summary>
/// <remarks>
/// It has the <see cref="Policies.Manager"/> policy of its own, as §6.4 puts it outside <c>/api/admin</c>, which gives
/// a worker a <c>403</c> and a request with no session the session cookie's <c>401</c>. The tenant query filter keeps
/// every query to the caller's organisation (§5.9 item 2), and reading writes nothing, not even an audit event, as
/// §5.4 lists none for reading (A28).
/// </remarks>
internal static class ReviewListEndpoint
{
    private const string ToReview = "toReview";

    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapReviewListEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/reviews", GetAsync).RequireAuthorization(Policies.Manager);
        return api;
    }

    // A note leaves To review only through a review (§3.6, A15), so an archived participant's stays listed (§3.7). Only
    // a submitted note has versions, so a draft or a discarded draft is never listed, whatever its flag status says. A
    // flagged version always has a reason, as a save refuses one without (§5.8 step 4). Two notes flagged in the same
    // millisecond are ordered by ID, so the order is the same from one call to the next.
    private static async Task<Results<Ok<IReadOnlyList<ToReviewRow>>, BadRequest>> GetAsync(
        string? status, Grow2NotesDbContext db, CancellationToken cancellationToken)
    {
        // The bare status, which the status code pages make problem details.
        if (status != ToReview)
        {
            return TypedResults.BadRequest();
        }

        var rows = await (
            from n in db.Notes.AsNoTracking()
            where n.Status == NoteStatus.Submitted && n.FlagStatus == FlagStatus.ToReview
            join participant in db.Participants on n.ParticipantId equals participant.Id
            join author in db.Users on n.AuthorUserId equals author.Id
            join flagged in db.NoteVersions
                on new { NoteId = n.Id, VersionNumber = n.FlaggedVersionNumber }
                equals new { flagged.NoteId, VersionNumber = (int?)flagged.VersionNumber }
            join current in db.NoteVersions
                on new { NoteId = n.Id, VersionNumber = n.CurrentVersionNumber }
                equals new { current.NoteId, current.VersionNumber }
            orderby flagged.CreatedAtUtc, n.Id
            select new ToReviewRow(
                participant.Id,
                participant.GivenName,
                participant.FamilyName,
                n.NoteDate,
                author.DisplayName,
                flagged.FlagReason!,
                flagged.CreatedAtUtc,
                n.CurrentVersionNumber,
                !current.IsFlagged))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<ToReviewRow>>(rows);
    }
}
