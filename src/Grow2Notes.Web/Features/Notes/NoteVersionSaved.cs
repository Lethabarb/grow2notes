using System.Text.Json.Serialization;

namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The answer to a version save, <c>{versionNumber, createdAtUtc, flagStatus?}</c> (design.md §6.3): the number of the
/// version and the server's time of its save, as the version holds them, and the note's flag status, the
/// <c>FlagStatus</c> name, to a manager alone, as the save left it, or, for a retry, as it is now. The endpoint leaves
/// it out for a worker, so a worker never learns that a manager has reviewed their note.
/// </summary>
internal sealed record NoteVersionSaved(
    int VersionNumber,
    DateTime CreatedAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FlagStatus);
