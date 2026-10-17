namespace Grow2Notes.Web.Data;

/// <summary>
/// A goal or a common item in a working copy's snapshot (<see cref="NoteDraft.Items"/>), with its wording as it was
/// when the working copy was created.
/// </summary>
/// <param name="SourceId">The goal's or the common item's ID.</param>
/// <param name="GroupId">The common item's group; null for a goal.</param>
/// <param name="IsTicked">Ticked means reached, for a goal, or done, for a common item (D7, D10).</param>
internal sealed record DraftItem(NoteSection Section, Guid SourceId, Guid? GroupId, string Text, bool IsTicked);
