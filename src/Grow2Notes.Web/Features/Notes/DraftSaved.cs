namespace Grow2Notes.Web.Features.Notes;

/// <summary>
/// The answer to a save of the caller's working copy, <c>{savedAtUtc}</c> (design.md §6.3): the time it was saved, as
/// the working copy now holds it, which the form's save indicator shows as "Saved 9:42 am" (§4.3).
/// </summary>
internal sealed record DraftSaved(DateTime SavedAtUtc);
