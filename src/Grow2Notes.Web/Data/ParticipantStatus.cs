namespace Grow2Notes.Web.Data;

/// <summary>
/// Whether a participant is on Today and can get new notes (design.md §3.7, §5.3). Stored as <c>tinyint</c> with fixed
/// values, so a stored status never changes meaning.
/// </summary>
internal enum ParticipantStatus : byte
{
    Active = 1,
    Archived = 2,
}
