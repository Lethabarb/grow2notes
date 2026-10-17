namespace Grow2Notes.Web.Data;

/// <summary>
/// A common item group in a working copy's snapshot (<see cref="NoteDraft.Groups"/>), with its name as it was when the
/// working copy was created.
/// </summary>
/// <param name="IsPicked">
/// Whether the writer picked the group. Always false for the Every note group, which is always shown.
/// </param>
internal sealed record DraftGroup(Guid GroupId, string Name, bool IsEveryNote, bool IsPicked);
