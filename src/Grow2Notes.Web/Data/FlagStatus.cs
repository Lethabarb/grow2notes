namespace Grow2Notes.Web.Data;

/// <summary>
/// A note's review state (design.md §3.6, §5.8 <i>Flag rules</i>): a flagged version puts the note in To review, and
/// only a manager's review takes it out. Stored as <c>tinyint</c> with fixed values, which the flagged notes index's
/// filter names.
/// </summary>
internal enum FlagStatus : byte
{
    None = 0,
    ToReview = 1,
    Reviewed = 2,
}
