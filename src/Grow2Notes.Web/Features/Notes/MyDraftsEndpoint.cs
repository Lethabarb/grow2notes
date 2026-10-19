using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// <c>GET /api/me/drafts</c> (design.md §6.3), the caller's unfinished drafts (§4.2 item 2, A17): each of their own
/// working copies of a live note, a draft or a pending edit, as a <see cref="MyDraft"/>, whatever its date and whether
/// or not its participant is archived since (§3.7), in no promised order, as Today sorts the section and drops the
/// rows that its list shows already (today.md). A discarded draft's working copy, which is kept (§5.3), is never
/// listed, as its note is not live (§5.5 item 1).
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. The tenant query filter keeps every query to the caller's
/// organisation (§5.9 item 2), and reading writes nothing, not even an audit event, as §5.4 lists none for reading
/// (A28). The working copies are found by <c>NoteDraft</c>'s <c>(OrganisationId, EditorUserId)</c> index (§5.3).
/// </remarks>
internal static class MyDraftsEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapMyDraftsEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/me/drafts", GetAsync);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<MyDraft>>> GetAsync(
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        Grow2NotesDbContext db,
        CancellationToken cancellationToken)
    {
        var callerId = Actor.InApp(user, identityOptions.Value).UserId;
        var drafts = await (
            from workingCopy in db.NoteDrafts.AsNoTracking()
            where workingCopy.EditorUserId == callerId
            join n in LiveNote.All(db) on workingCopy.NoteId equals n.Id
            join participant in db.Participants on n.ParticipantId equals participant.Id
            select new MyDraft(
                participant.Id,
                participant.GivenName,
                participant.FamilyName,
                n.NoteDate,
                workingCopy.BaseVersionNumber == 0 ? MyDraft.DraftKind : MyDraft.PendingEditKind,
                workingCopy.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok<IReadOnlyList<MyDraft>>(drafts);
    }
}
