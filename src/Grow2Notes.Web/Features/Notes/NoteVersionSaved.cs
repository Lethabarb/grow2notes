namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The answer to a version save, <c>{versionNumber, createdAtUtc}</c> (design.md §6.3): the number of the version and
/// the server's time of its save, as the version holds them. T11 adds <c>flagStatus</c>.
/// </summary>
internal sealed record NoteVersionSaved(int VersionNumber, DateTime CreatedAtUtc);
