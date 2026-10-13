namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// A configuration row that is archived, never deleted (design.md §3.7, A2, A29): a participant, a goal, or a common
/// item or group. <c>ArchivedAtUtc</c> is null while the row is active. <see cref="Archiving.Archive"/> archives it,
/// and <see cref="Archiving.Restore"/> restores a row of an ordered list.
/// </summary>
internal interface IArchivableRow
{
    DateTime? ArchivedAtUtc { get; set; }
}
