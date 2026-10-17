using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A goal that a test adds to a participant of its own: its text and <c>SortOrder</c>; when it was added, by default
/// <see cref="TestGoals.AddedAtUtc"/>; its ID, by default a new one; and when it was archived, or
/// <see langword="null"/> for an active goal.
/// </summary>
internal sealed record TestGoal(
    string Text, int SortOrder, DateTime? CreatedAtUtc = null, Guid? Id = null, DateTime? ArchivedAtUtc = null);

/// <summary>
/// A goal that <see cref="TestGoals.AddGoalAsync"/> added: its ID, and its <c>ETag</c>, the <c>If-Match</c> that a
/// change to it sends.
/// </summary>
internal sealed record AddedGoal(Guid Id, string ETag);

internal static class TestGoals
{
    /// <summary>When a goal that <see cref="AddGoalsAsync"/> adds was added, unless it says; made up (A35).</summary>
    public static readonly DateTime AddedAtUtc = new(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);

    /// <summary>
    /// Adds the goals to a participant of <paramref name="organisation"/>'s whom the test added, through the app's
    /// context under its tenant, as the app adds them, with each goal's values as given, so that a test can set up
    /// gaps, ties and archived goals that no endpoint would leave; and gives their IDs in the order given.
    /// </summary>
    public static async Task<List<Guid>> AddGoalsAsync(
        this IServiceProvider services, SeededOrganisation organisation, Guid participantId, params TestGoal[] goals) =>
        [.. (await services.AddRowsAsync(organisation, participantId, goals)).Select(goal => goal.Id)];

    /// <summary>
    /// Adds one goal as <see cref="AddGoalsAsync"/> adds them, and gives its ID and its <c>ETag</c>, for a test that
    /// changes it.
    /// </summary>
    public static async Task<AddedGoal> AddGoalAsync(
        this IServiceProvider services, SeededOrganisation organisation, Guid participantId, TestGoal goal)
    {
        var row = Assert.Single(await services.AddRowsAsync(organisation, participantId, goal));

        // EF Core reads back the version that SQL Server gave the row as it saved it.
        return new(row.Id, RowVersionToken.ETag(row));
    }

    private static async Task<List<Goal>> AddRowsAsync(
        this IServiceProvider services, SeededOrganisation organisation, Guid participantId, params TestGoal[] goals)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var rows = goals.Select(goal => new Goal
        {
            Id = goal.Id ?? Guid.NewGuid(),
            OrganisationId = organisation.OrganisationId,
            ParticipantId = participantId,
            Text = goal.Text,
            SortOrder = goal.SortOrder,
            ArchivedAtUtc = goal.ArchivedAtUtc,
            CreatedAtUtc = goal.CreatedAtUtc ?? AddedAtUtc,
        }).ToList();
        db.Goals.AddRange(rows);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return rows;
    }
}
