using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Reviews;

/// <summary>
/// <c>GET /api/reviews?status=…</c> (design.md §6.4), the Flagged screen's two lists (§4.6).
/// <c>status=toReview</c> answers the To review list as <see cref="ToReviewRow"/>s: the organisation's submitted notes
/// in To review, each with the reason and time of its flagged version, oldest flag first, returned whole (§6.1
/// <i>Lists</i>). <c>status=reviewed</c> answers the Reviewed list as a <see cref="ReviewedPage"/>: the organisation's
/// reviews, newest first, at most 30 at a time, the next page after <c>before</c> and <c>beforeId</c>, the time and ID
/// of the last review of the page before it. Any other <c>status</c>, or none, or only one of the two cursor values
/// for Reviewed, gets <c>400</c>.
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

    private const string Reviewed = "reviewed";

    private const int PageSize = 30;

    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapReviewListEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/reviews", GetAsync).RequireAuthorization(Policies.Manager);
        return api;
    }

    /// <summary>
    /// The organisation's submitted notes in To review, as <see cref="ToReviewRow"/>s in the list's order,
    /// <c>ORDER BY</c> the flagged version's <c>CreatedAtUtc</c>, then the note's <c>Id</c> (design.md §6.4).
    /// </summary>
    /// <remarks>
    /// A note leaves To review only through a review (§3.6, A15), so an archived participant's stays listed (§3.7).
    /// Only a submitted note has versions, so a draft or a discarded draft is never listed, whatever its flag status
    /// says. A flagged version always has a reason, as a save refuses one without (§5.8 step 4). Two notes flagged in
    /// the same millisecond are ordered by ID, so the order is the same from one call to the next.
    /// </remarks>
    public static IQueryable<ToReviewRow> ToReviewInListOrder(Grow2NotesDbContext db) =>
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
            !current.IsFlagged);

    /// <summary>
    /// The organisation's reviews as <see cref="ReviewedRow"/>s in the list's order, <c>ORDER BY</c> the review's
    /// <c>ReviewedAtUtc</c> descending, then its <c>Id</c> descending, those after <paramref name="before"/> only when
    /// it is given (design.md §6.4).
    /// </summary>
    /// <remarks>
    /// The cursor is exclusive and ordered as the list is, so no review is skipped or repeated, even among reviews of
    /// one millisecond. SQL Server compares an <c>Id</c> in the same order in the <c>WHERE</c> and the <c>ORDER BY</c>.
    /// Every reviewed note is submitted, so an archived participant's stays listed (§3.7) and a draft is never
    /// listed. The reason is the one of the latest flagged version at or before the reviewed one: only a version that
    /// raised the flag has a reason of its own, and every flagged version after it up to the review carries the same,
    /// while the note's own flagged version number moves on when it is flagged again (A15).
    /// </remarks>
    public static IQueryable<ReviewedRow> ReviewedInListOrder(
        Grow2NotesDbContext db, (DateTime ReviewedAtUtc, Guid Id)? before)
    {
        var reviews = db.NoteReviews.AsNoTracking();
        if (before is { } cursor)
        {
            var (reviewedAtUtc, id) = cursor;
            reviews = reviews.Where(r =>
                r.ReviewedAtUtc < reviewedAtUtc || (r.ReviewedAtUtc == reviewedAtUtc && r.Id.CompareTo(id) < 0));
        }

        return
            from r in reviews
            join n in db.Notes on r.NoteId equals n.Id
            join participant in db.Participants on n.ParticipantId equals participant.Id
            join author in db.Users on n.AuthorUserId equals author.Id
            join reviewer in db.Users on r.ReviewedByUserId equals reviewer.Id
            orderby r.ReviewedAtUtc descending, r.Id descending
            select new ReviewedRow(
                r.Id,
                participant.Id,
                participant.GivenName,
                participant.FamilyName,
                n.NoteDate,
                author.DisplayName,
                db.NoteVersions
                    .Where(v => v.NoteId == r.NoteId && v.IsFlagged && v.VersionNumber <= r.VersionNumber)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => v.FlagReason)
                    .FirstOrDefault()!,
                reviewer.DisplayName,
                r.ReviewedAtUtc,
                r.Comment);
    }

    // before is bound as a DateTimeOffset, so a value that ends in Z, as the answer's does, is the same instant on any
    // host.
    private static async Task<Results<Ok<IReadOnlyList<ToReviewRow>>, Ok<ReviewedPage>, BadRequest>> GetAsync(
        string? status,
        DateTimeOffset? before,
        Guid? beforeId,
        Grow2NotesDbContext db,
        CancellationToken cancellationToken)
    {
        if (status == ToReview)
        {
            var toReview = await ToReviewInListOrder(db).ToListAsync(cancellationToken);
            return TypedResults.Ok<IReadOnlyList<ToReviewRow>>(toReview);
        }

        // The bare status or cursor, which the status code pages make problem details.
        if (status != Reviewed || before.HasValue != beforeId.HasValue)
        {
            return TypedResults.BadRequest();
        }

        (DateTime, Guid)? after = before is { } time && beforeId is { } id ? (time.UtcDateTime, id) : null;
        var rows = await ReviewedInListOrder(db, after).Take(PageSize + 1).ToListAsync(cancellationToken);
        return TypedResults.Ok(new ReviewedPage([.. rows.Take(PageSize)], rows.Count > PageSize));
    }
}
