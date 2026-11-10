namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The body of <c>POST /api/admin/participants/{participantId}/goals</c> and of <c>PUT /api/admin/goals/{goalId}</c>
/// (design.md §6.6): a goal's text as typed, and its term. The text may be missing, so that
/// <see cref="Platform.Configuration.ItemText.Goal"/> refuses it as it refuses an empty one, in the screen's words.
/// <see cref="IsMediumOrLongTerm"/> is true for a Medium or long term goal and false for a Short term one (A150); a
/// POST that leaves it out adds a Short term goal, and a PUT that leaves it out keeps the stored term.
/// </summary>
internal sealed record GoalTextRequest(string? Text, bool? IsMediumOrLongTerm = null);
