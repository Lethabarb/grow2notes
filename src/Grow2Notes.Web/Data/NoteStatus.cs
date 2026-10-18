namespace Grow2Notes.Web.Data;

/// <summary>
/// Where a note is in its life (design.md §3.4, §5.3): a draft moves to Submitted or Discarded, never back. Stored as
/// <c>tinyint</c> with fixed values, which the <c>Note</c> check constraint and the one-live-note index's filter name.
/// </summary>
internal enum NoteStatus : byte
{
    Draft = 0,
    Submitted = 1,
    Discarded = 2,
}
