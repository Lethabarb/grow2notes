using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// A common item group other than Every note that a test adds: its name and <c>SortOrder</c>; when it was added, by
/// default <see cref="TestCommonItems.AddedAtUtc"/>; its ID, by default a new one; and when it was archived, or
/// <see langword="null"/> for an active group.
/// </summary>
internal sealed record TestCommonItemGroup(
    string Name, int SortOrder, DateTime? CreatedAtUtc = null, Guid? Id = null, DateTime? ArchivedAtUtc = null);

/// <summary>
/// A common item that a test adds to a group: its text and <c>SortOrder</c>; when it was added, by default
/// <see cref="TestCommonItems.AddedAtUtc"/>; its ID, by default a new one; and when it was archived, or
/// <see langword="null"/> for an active item.
/// </summary>
internal sealed record TestCommonItem(
    string Text, int SortOrder, DateTime? CreatedAtUtc = null, Guid? Id = null, DateTime? ArchivedAtUtc = null);

/// <summary>
/// Groups and items that a test adds straight through the app's context, as no endpoint adds a group until T07, and
/// so that a test can set up gaps, ties and archived rows that no endpoint would leave.
/// </summary>
internal static class TestCommonItems
{
    /// <summary>When a group or an item that these add was added, unless it says; made up (A35).</summary>
    public static readonly DateTime AddedAtUtc = new(2026, 10, 3, 4, 5, 6, 789, DateTimeKind.Utc);

    /// <summary>
    /// Adds the groups to the organisation of ID <paramref name="organisationId"/>, through the app's context under
    /// its tenant, as the app adds them, with each group's values as given; and gives their IDs in the order given.
    /// </summary>
    public static async Task<List<Guid>> AddGroupsAsync(
        this IServiceProvider services, Guid organisationId, params TestCommonItemGroup[] groups)
    {
        var rows = groups.Select(group => new CommonItemGroup
        {
            Id = group.Id ?? Guid.NewGuid(),
            OrganisationId = organisationId,
            Name = group.Name,
            SortOrder = group.SortOrder,
            ArchivedAtUtc = group.ArchivedAtUtc,
            CreatedAtUtc = group.CreatedAtUtc ?? AddedAtUtc,
        }).ToList();
        await services.SaveAsync(organisationId, db => db.CommonItemGroups.AddRange(rows));
        return [.. rows.Select(group => group.Id)];
    }

    /// <summary>
    /// Adds the items to the group of ID <paramref name="groupId"/> in the organisation of ID
    /// <paramref name="organisationId"/>, as <see cref="AddGroupsAsync"/> adds groups; and gives their IDs in the order
    /// given.
    /// </summary>
    public static async Task<List<Guid>> AddItemsAsync(
        this IServiceProvider services, Guid organisationId, Guid groupId, params TestCommonItem[] items)
    {
        var rows = items.Select(item => new CommonItem
        {
            Id = item.Id ?? Guid.NewGuid(),
            OrganisationId = organisationId,
            GroupId = groupId,
            Text = item.Text,
            SortOrder = item.SortOrder,
            ArchivedAtUtc = item.ArchivedAtUtc,
            CreatedAtUtc = item.CreatedAtUtc ?? AddedAtUtc,
        }).ToList();
        await services.SaveAsync(organisationId, db => db.CommonItems.AddRange(rows));
        return [.. rows.Select(item => item.Id)];
    }

    private static async Task SaveAsync(
        this IServiceProvider services, Guid organisationId, Action<Grow2NotesDbContext> add)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        add(db);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}
