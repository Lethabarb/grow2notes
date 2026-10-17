using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Participants;

/// <summary>
/// The SQL of <see cref="GoalList.InListOrder"/>, the order in which the goals list shows a participant's goals and the
/// reorder reads them (design.md §6.6). SQL Server gives two goals tied on <c>SortOrder</c> and <c>CreatedAtUtc</c> in
/// <c>Id</c> order with or without the <c>ORDER BY</c>'s <c>Id</c> step, as <c>Id</c> is the clustered key, which every
/// index on <c>Goal</c> carries, so no rows that <see cref="GoalListEndpointTests"/> reads can show the step is there:
/// this reads it in the query. Writing the SQL opens no connection, so the test needs no database.
/// </summary>
public sealed class GoalListOrderTests
{
    [Fact]
    public async Task The_list_s_order_is_SortOrder_then_CreatedAtUtc_then_Id()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        string sql;

        // The SQL carries the tenant filter's value, which is read as it is written.
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Guid.NewGuid()))
        {
            sql = GoalList.InListOrder(db.Goals).ToQueryString();
        }

        Assert.EndsWith("ORDER BY [g].[SortOrder], [g].[CreatedAtUtc], [g].[Id]", sql);
    }
}
