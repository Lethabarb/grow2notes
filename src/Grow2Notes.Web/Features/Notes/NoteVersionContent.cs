using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// A saved version of a note as the content that a working copy or the read view needs (design.md §5.3): the snapshot
/// that its group, goal and common item rows make, and its text and flag. It is a read model, with no table.
/// </summary>
/// <remarks>
/// It is the only conversion from a version's rows back into a <see cref="NoteSnapshot"/>, so that creating a pending
/// edit, opening one and reading the current version all see the version the same way. A version saved before common
/// items had groups has no group rows and no common item rows, so it gives no groups and no common items.
/// </remarks>
internal sealed record NoteVersionContent(NoteSnapshot Snapshot, string Narrative, bool IsFlagged, string? FlagReason)
{
    /// <summary>
    /// Version <paramref name="versionNumber"/> of the note: its groups in their places, then its goals and its common
    /// items, each in their places, with each common item in the group at its group position.
    /// </summary>
    public static async Task<NoteVersionContent> ReadAsync(
        Grow2NotesDbContext db, Guid noteId, int versionNumber, CancellationToken cancellationToken)
    {
        var version = await db.NoteVersions
            .AsNoTracking()
            .Where(v => v.NoteId == noteId && v.VersionNumber == versionNumber)
            .Select(v => new { v.Id, v.Narrative, v.IsFlagged, v.FlagReason })
            .SingleAsync(cancellationToken);
        var groups = await db.NoteVersionGroups
            .AsNoTracking()
            .Where(group => group.NoteVersionId == version.Id)
            .OrderBy(group => group.Position)
            .ToListAsync(cancellationToken);
        var items = await db.NoteVersionItems
            .AsNoTracking()
            .Where(item => item.NoteVersionId == version.Id)
            .OrderBy(item => item.Section)
            .ThenBy(item => item.Position)
            .ToListAsync(cancellationToken);
        var snapshot = new NoteSnapshot(
            [
                .. groups.Select(group =>
                    new DraftGroup(group.CommonItemGroupId, group.Name, group.IsEveryNote, group.IsPicked)),
            ],
            [
                .. items.Select(item => item.Section == NoteSection.Goal
                    ? new DraftItem(item.Section, item.GoalId!.Value, GroupId: null, item.Text, item.IsTicked)
                    : new DraftItem(
                        item.Section,
                        item.CommonItemId!.Value,
                        groups[item.GroupPosition!.Value].CommonItemGroupId,
                        item.Text,
                        item.IsTicked)),
            ]);
        return new(snapshot, version.Narrative, version.IsFlagged, version.FlagReason);
    }
}
