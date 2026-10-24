using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// One version of a submitted note in the list that <c>GET {base}/versions</c> answers (design.md §6.3): its number,
/// who saved it and when, and whether it is flagged. It is a read model, with no table.
/// </summary>
internal sealed record NoteVersionSummary(
    int VersionNumber, NoteReadAuthor CreatedBy, DateTime CreatedAtUtc, bool IsFlagged)
{
    /// <summary>
    /// Every version of the note <paramref name="noteId"/>, newest first, or only version
    /// <paramref name="versionNumber"/> when it is not <c>null</c>.
    /// </summary>
    /// <remarks>
    /// The filters and the order come before the projection, as EF cannot translate them on a record that a select
    /// builds.
    /// </remarks>
    public static IQueryable<NoteVersionSummary> Query(Grow2NotesDbContext db, Guid noteId, int? versionNumber) =>
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
