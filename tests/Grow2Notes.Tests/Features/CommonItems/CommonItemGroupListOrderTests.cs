using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.CommonItems;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// The SQL of the two <see cref="CommonItemGroupList.InListOrder(IQueryable{CommonItemGroup})"/> helpers, the orders in
/// which the groups list shows the groups and each group's items, and the reorders read them (design.md §6.6). SQL
/// Server gives two rows tied on every other column in <c>Id</c> order with or without the <c>ORDER BY</c>'s <c>Id</c>
/// step, as <c>Id</c> is the clustered key, which every index carries, so no rows that
/// <see cref="CommonItemGroupListEndpointTests"/> reads can show the step is there: this reads it in each query.
/// Writing the SQL opens no connection, so the tests need no database.
/// </summary>
public sealed class CommonItemGroupListOrderTests
{
    [Fact]
    public async Task The_groups_order_is_IsEveryNote_descending_then_SortOrder_then_CreatedAtUtc_then_Id()
    {
        var sql = await SqlOfAsync(db => CommonItemGroupList.InListOrder(db.CommonItemGroups));

        Assert.EndsWith("ORDER BY [c].[IsEveryNote] DESC, [c].[SortOrder], [c].[CreatedAtUtc], [c].[Id]", sql);
    }

    [Fact]
    public async Task The_items_order_is_SortOrder_then_CreatedAtUtc_then_Id()
    {
        var sql = await SqlOfAsync(db => CommonItemGroupList.InListOrder(db.CommonItems));

        Assert.EndsWith("ORDER BY [c].[SortOrder], [c].[CreatedAtUtc], [c].[Id]", sql);
    }

    private static async Task<string> SqlOfAsync<TRow>(Func<Grow2NotesDbContext, IQueryable<TRow>> query)
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        // The SQL carries the tenant filter's value, which is read as it is written.
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Guid.NewGuid()))
        {
            return query(db).ToQueryString();
        }
    }
}
