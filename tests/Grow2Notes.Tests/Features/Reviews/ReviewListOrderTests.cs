using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Reviews;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Reviews;

/// <summary>
/// The SQL of <see cref="ReviewListEndpoint.ToReviewInListOrder"/>, the To review list's order (design.md §6.4): oldest
/// flag first, by the flagged version's time, then by note ID. SQL Server gives two notes flagged in the same
/// millisecond in <c>Id</c> order with or without the <c>ORDER BY</c>'s <c>Id</c> step, as <c>Id</c> is the clustered
/// key, which every index on <c>Note</c> carries, so no rows that <see cref="ReviewListEndpointTests"/> reads can show
/// the step is there: this reads it in the query, after the time of the version that the join on
/// <c>FlaggedVersionNumber</c> gives. Writing the SQL opens no connection, so the test needs no database.
/// </summary>
public sealed class ReviewListOrderTests
{
    [Fact]
    public async Task The_list_s_order_is_the_flagged_version_s_CreatedAtUtc_then_the_note_s_Id()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        string sql;

        // The SQL carries the tenant filter's value, which is read as it is written.
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Guid.NewGuid()))
        {
            sql = ReviewListEndpoint.ToReviewInListOrder(db).ToQueryString();
        }

        Assert.Contains(
            ") AS [n1] ON [n].[Id] = [n1].[NoteId] AND [n].[FlaggedVersionNumber] = [n1].[VersionNumber]", sql);
        Assert.EndsWith("ORDER BY [n1].[CreatedAtUtc], [n].[Id]", sql);
    }

    // The Reviewed list's order is the review's time, then its ID, both newest first, and its cursor compares the ID in
    // SQL, which only a translation of CompareTo gives: client evaluation would throw, not return a wrong page. The
    // reason is the version number's highest flagged version at or below the review's, one row of a subquery. The
    // aliases are EF Core's, so the checks read the columns, not the aliases.
    [Fact]
    public async Task The_Reviewed_list_is_newest_first_by_Id_too_with_a_translated_cursor_and_a_TOP_1_reason()
    {
        await using var app = new AppWithoutDatabase("Production");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var before = (new DateTime(2026, 10, 4, 23, 42, 0, DateTimeKind.Utc), Guid.NewGuid());
        string sql;

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Guid.NewGuid()))
        {
            sql = ReviewListEndpoint.ReviewedInListOrder(db, before).ToQueryString();
        }

        Assert.Matches(@"(?s)ORDER BY \S*\[ReviewedAtUtc\] DESC, \S*\[Id\] DESC\s*$", sql);
        Assert.Matches(@"\[ReviewedAtUtc\] < @", sql);
        Assert.Matches(@"\[Id\] < @", sql);
        Assert.Matches(@"(?s)SELECT TOP\(1\) .*ORDER BY \S*\[VersionNumber\] DESC", sql);
    }
}
