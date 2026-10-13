using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// The reorder of a list, on stand-in rows: a participant's goals, the active ones as the list's query gives them.
/// Each endpoint that reorders (S01.02.07, S01.03.03, S01.03.06) shows it again on its own table.
/// </summary>
public sealed class ListOrderTests
{
    private static readonly Guid WalkId = Guid.Parse("4b1e7c2a-9d3f-4e68-a5b0-1c2d3e4f5a6b");

    // After the bus's ID in .NET's order, which starts at the first byte, and in SQL Server's, which starts at the
    // last six, so sorting by ID either way would put the bus first.
    private static readonly Guid LunchId = Guid.Parse("c8d2a4f6-7b9e-4c1d-b3a5-e6f708192a3b");

    private static readonly Guid BusId = Guid.Parse("2e5f8a1b-3c4d-4e7f-8a9b-0c1d2e3f4a5b");

    private static readonly Guid ShoppingId = Guid.Parse("9a3c5e7f-1b2d-4f40-8c6e-7a8b9c0d1e2f");

    private static readonly Guid OtherParticipantsGoalId = Guid.Parse("6d7e8f90-a1b2-4c3d-9e4f-5a6b7c8d9e0f");

    // The archived goal keeps the SortOrder it had, so the active ones have a gap that only a reorder closes.
    private static readonly (Guid, int)[] StoredSortOrders = [(WalkId, 0), (LunchId, 1), (ShoppingId, 2), (BusId, 3)];

    [Fact]
    public void A_missing_ID_gives_order_mismatch_and_changes_no_row() =>
        AssertMismatch([BusId, WalkId]);

    [Fact]
    public void An_extra_ID_gives_order_mismatch_and_changes_no_row() =>
        AssertMismatch([BusId, WalkId, LunchId, OtherParticipantsGoalId]);

    // In place of an active goal, so there are as many IDs as active goals.
    [Fact]
    public void An_archived_ID_gives_order_mismatch_and_changes_no_row() =>
        AssertMismatch([BusId, ShoppingId, WalkId]);

    // Beside every active goal, so the IDs name the active goals and no other.
    [Fact]
    public void A_repeated_ID_gives_order_mismatch_and_changes_no_row() =>
        AssertMismatch([BusId, WalkId, LunchId, WalkId]);

    [Fact]
    public void A_new_order_gives_the_active_rows_0_to_n_minus_1_and_the_old_and_new_order()
    {
        var goals = Goals();

        var result = ListOrder.Reorder(Active(goals), [BusId, WalkId, LunchId]);

        Assert.Null(result.Error);
        Assert.Equal([(WalkId, 1), (LunchId, 2), (ShoppingId, 2), (BusId, 0)], SortOrders(goals));
        Assert.Equal(Order(WalkId, LunchId, BusId), result.Before.ToJsonString());
        Assert.Equal(Order(BusId, WalkId, LunchId), result.After.ToJsonString());
    }

    // Two goals added at once, the list's query giving them in CreatedAtUtc order. The order that sorting them by ID
    // would give is a new order, which a helper that sorted would take for the stored one and so change nothing.
    [Fact]
    public void Rows_with_the_same_SortOrder_keep_in_the_old_order_the_order_they_were_given_in()
    {
        Row[] goals = [new(WalkId, 0), new(LunchId, 1), new(BusId, 1)];

        var result = ListOrder.Reorder(goals, [WalkId, BusId, LunchId]);

        Assert.Null(result.Error);
        Assert.Equal([(WalkId, 0), (LunchId, 2), (BusId, 1)], SortOrders(goals));
        Assert.Equal(Order(WalkId, LunchId, BusId), result.Before.ToJsonString());
        Assert.Equal(Order(WalkId, BusId, LunchId), result.After.ToJsonString());
    }

    // The gap stays, and the save finds no value changed, so it saves nothing and writes no event (D84).
    [Fact]
    public void The_stored_order_again_changes_nothing()
    {
        var goals = Goals();

        var result = ListOrder.Reorder(Active(goals), [WalkId, LunchId, BusId]);

        Assert.Null(result.Error);
        Assert.Equal(StoredSortOrders, SortOrders(goals));
        Assert.Null(ChangeDetails.Of(result.Before, result.After));
    }

    private static void AssertMismatch(Guid[] requestedIds)
    {
        var goals = Goals();

        var result = ListOrder.Reorder(Active(goals), requestedIds);

        Assert.Same(ErrorCode.OrderMismatch, result.Error);
        Assert.Equal(StoredSortOrders, SortOrders(goals));
        Assert.Equal(Order(WalkId, LunchId, BusId), result.Before.ToJsonString());
        Assert.Equal(Order(WalkId, LunchId, BusId), result.After.ToJsonString());
    }

    // A participant's goals as the list gives them, the archived one among them.
    private static Row[] Goals() =>
        [new(WalkId, 0), new(LunchId, 1), new(ShoppingId, 2) { Archived = true }, new(BusId, 3)];

    // What the reorder's query gives: the active goals, in the list's order.
    private static Row[] Active(Row[] goals) => [.. goals.Where(goal => !goal.Archived)];

    private static (Guid, int)[] SortOrders(Row[] goals) => [.. goals.Select(goal => (goal.Id, goal.SortOrder))];

    // A *.reordered event's old or new order, its IDs written as design.md §6.6's lists write them.
    private static string Order(params Guid[] ids) =>
        $$"""{"order":[{{string.Join(',', ids.Select(id => $"\"{id:D}\""))}}]}""";

    private sealed class Row(Guid id, int sortOrder) : IOrderedRow
    {
        public Guid Id { get; } = id;

        public int SortOrder { get; set; } = sortOrder;

        public bool Archived { get; init; }
    }
}
