using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Web.Features.Participants;

/// <summary>
/// A goal as <c>GET /api/admin/participants/{participantId}/goals</c> lists it (design.md §6.6), for the Goals section
/// of a participant's page (§4.8): <see cref="ArchivedAtUtc"/> is <see langword="null"/> while the goal is active, and
/// <see cref="RowVersion"/> is the row's <c>RowVersion</c> as base64, which the SPA sends, in quotes, as
/// <c>If-Match</c> when it changes the goal (D78).
/// </summary>
internal sealed record AdminGoal(Guid Id, string Text, int SortOrder, DateTime? ArchivedAtUtc, string RowVersion)
{
    public static AdminGoal Of(Goal goal) =>
        new(goal.Id, goal.Text, goal.SortOrder, goal.ArchivedAtUtc, RowVersionToken.Of(goal));
}
