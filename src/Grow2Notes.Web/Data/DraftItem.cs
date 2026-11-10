namespace Grow2Notes.Web.Data;

/// <summary>
/// A goal or a common item in a working copy's snapshot (<see cref="NoteDraft.Items"/>), with its wording as it was
/// when the working copy was created.
/// </summary>
/// <param name="SourceId">The goal's or the common item's ID.</param>
/// <param name="GroupId">The common item's group; null for a goal.</param>
/// <param name="IsTicked">Ticked means reached, for a goal, or done, for a common item (D7, D10).</param>
/// <param name="IsMediumOrLongTerm">
/// Whether a goal is Medium or long term (A150); null, and false, for a common item and for a Short term goal.
/// </param>
/// <param name="NeedsDetail">
/// Whether a common item asks for details when ticked (A151); null, and false, otherwise.
/// </param>
/// <param name="Detail">The details written for a ticked common item that needs them; null otherwise (A151).</param>
/// <remarks>
/// The last three are nullable and read as "not" when null, so a working copy written before T44, or by the release
/// that a deploy swaps out, reads safely whatever the JSON holds (A154). Readers test <c>is true</c>. The code that
/// makes a snapshot writes <c>true</c> only where it is set and null otherwise, so the snapshot of lists that use
/// none of them is what it was before T44.
/// </remarks>
internal sealed record DraftItem(
    NoteSection Section,
    Guid SourceId,
    Guid? GroupId,
    string Text,
    bool IsTicked,
    bool? IsMediumOrLongTerm = null,
    bool? NeedsDetail = null,
    string? Detail = null);
