namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>A goal of the participant that <c>get_participant</c> returned.</summary>
/// <param name="GoalId">The goal's ID.</param>
/// <param name="Text">As typed.</param>
/// <param name="Archived">Whether the goal is archived.</param>
/// <param name="ExpectedVersion">The goal's version, the app's ETag token, which a later change needs.</param>
internal sealed record ParticipantGoal(Guid GoalId, string Text, bool Archived, string ExpectedVersion);
