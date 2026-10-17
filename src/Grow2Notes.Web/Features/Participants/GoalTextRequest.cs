namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// The body of <c>POST /api/admin/participants/{participantId}/goals</c> and of <c>PUT /api/admin/goals/{goalId}</c>
/// (design.md §6.6): a goal's text as typed. It may be missing, so that <see cref="GoalText.TryRead"/> refuses it as
/// it refuses an empty one, in the screen's words.
/// </summary>
internal sealed record GoalTextRequest(string? Text);
