using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Today;

/// <summary>
/// <c>GET /api/today</c> (design.md §6.3): the Today list (§4.2), as a <see cref="TodayList"/>, for everyone's home
/// screen, each participant with their live note for the day as the caller sees it.
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. The tenant query filter keeps the list to the caller's organisation
/// (§5.9 item 2), and it writes no audit event, as §5.4 lists none for reading (A28).
/// </remarks>
internal static class TodayEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapTodayEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/today", GetAsync);
        return api;
    }

    // The active participants only, as an archived one is not on Today (§3.7), in SQL Server's collation, as the list
    // is shown; the ID last, as the admin list breaks ties, so that two people of one name keep one order from one
    // call to the next.
    private static async Task<Ok<TodayList>> GetAsync(
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        Grow2NotesDbContext db,
        MelbourneClock clock,
        CancellationToken cancellationToken)
    {
        var callerId = Actor.InApp(user, identityOptions.Value).UserId;
        var today = clock.Today();
        var participants = await db.Participants
            .AsNoTracking()
            .Where(participant => participant.Status == ParticipantStatus.Active)
            .OrderBy(participant => participant.FamilyName)
            .ThenBy(participant => participant.GivenName)
            .ThenBy(participant => participant.Id)
            .Select(participant => new TodayParticipant(
                participant.Id, participant.GivenName, participant.FamilyName, null))
            .ToListAsync(cancellationToken);
        var notes = await NotesOfTheDayAsync(db, today, callerId, cancellationToken);
        return TypedResults.Ok(new TodayList(
            today, [.. participants.Select(person => person with { Note = notes.GetValueOrDefault(person.Id) })]));
    }

    // Each participant's live note for the day, by participant ID, at most one each (§5.5 item 1). A draft has no
    // version, so its current version number, 0, matches none, and it is never flagged. Only a draft's author has a
    // working copy of it, and only their own draft shows when they last saved it, never a pending edit of a submitted
    // note.
    private static async Task<Dictionary<Guid, TodayNote>> NotesOfTheDayAsync(
        Grow2NotesDbContext db, DateOnly day, Guid callerId, CancellationToken cancellationToken)
    {
        var notes = await (
            from n in LiveNote.All(db)
            where n.NoteDate == day
            join author in db.Users on n.AuthorUserId equals author.Id
            select new
            {
                n.ParticipantId,
                n.Status,
                author.DisplayName,
                IsMine = n.AuthorUserId == callerId,
                n.CreatedAtUtc,
                SavedAtUtc = db.NoteDrafts
                    .Where(d => d.NoteId == n.Id && d.EditorUserId == callerId)
                    .Select(d => (DateTime?)d.UpdatedAtUtc)
                    .FirstOrDefault(),
                n.FirstSubmittedAtUtc,
                IsFlagged = db.NoteVersions.Any(
                    v => v.NoteId == n.Id && v.VersionNumber == n.CurrentVersionNumber && v.IsFlagged),
            })
            .ToListAsync(cancellationToken);
        return notes.ToDictionary(
            note => note.ParticipantId,
            note => new TodayNote(
                note.Status.ToString(),
                note.DisplayName,
                note.IsMine,
                note.CreatedAtUtc,
                note.Status == NoteStatus.Draft && note.IsMine ? note.SavedAtUtc : null,
                note.FirstSubmittedAtUtc,
                note.IsFlagged));
    }
}
