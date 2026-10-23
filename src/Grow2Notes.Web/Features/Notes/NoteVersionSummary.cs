namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// One version of a submitted note in the list that <c>GET {base}/versions</c> answers (design.md §6.3): its number,
/// who saved it and when, and whether it is flagged. It is a read model, with no table.
/// </summary>
internal sealed record NoteVersionSummary(
    int VersionNumber, NoteReadAuthor CreatedBy, DateTime CreatedAtUtc, bool IsFlagged);
