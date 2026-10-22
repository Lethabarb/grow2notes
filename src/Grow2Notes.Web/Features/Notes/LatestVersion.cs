using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// Who saved a note's current version, by their display name, and when (design.md §6.3, §6.9): what a version conflict
/// tells the editor whose save it beat, and what the read view shows as the note's last edit. It is a read model, with
/// no table.
/// </summary>
internal sealed record LatestVersion(int VersionNumber, string CreatedBy, DateTime CreatedAtUtc)
{
    /// <summary>
    /// The current version of the note, or <see langword="null"/> when it has none, as a draft has not, or a submit
    /// raced a discard.
    /// </summary>
    public static Task<LatestVersion?> FindAsync(
        Grow2NotesDbContext db, Guid noteId, CancellationToken cancellationToken) =>
        (
            from n in db.Notes.AsNoTracking()
            where n.Id == noteId
            join version in db.NoteVersions on n.Id equals version.NoteId
            where version.VersionNumber == n.CurrentVersionNumber
            join creator in db.Users on version.CreatedByUserId equals creator.Id
            select new LatestVersion(version.VersionNumber, creator.DisplayName, version.CreatedAtUtc))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Answers <c>409 note.version_conflict</c> with <c>{currentVersion, createdBy,
    /// createdAtUtc}</c>.</summary>
    public ProblemHttpResult ConflictProblem() =>
        ErrorCode.VersionConflict.Problem(new Dictionary<string, object?>
        {
            ["currentVersion"] = VersionNumber,
            ["createdBy"] = CreatedBy,
            ["createdAtUtc"] = CreatedAtUtc,
        });
}
