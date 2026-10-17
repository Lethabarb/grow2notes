namespace Grow2Notes.Web.Data;

/// <summary>
/// The section of a note that a ticked row is in (design.md §3.1, §5.3): a goal, or a common item. Fixed values, so a
/// stored row never changes section.
/// </summary>
internal enum NoteSection : byte
{
    Goal = 1,
    CommonItem = 2,
}
