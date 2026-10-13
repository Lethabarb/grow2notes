using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform.Configuration;

/// <summary>
/// Archiving and restoring, on stand-in rows with a fake clock: a participant's goals as the list gives them, the
/// archived one among them. Each endpoint that archives or restores shows it again on its own table.
/// </summary>
public sealed class ArchivingTests
{
    private static readonly DateTime ShoppingArchivedAtUtc = TestClock.Start.AddDays(-1).UtcDateTime;

    private readonly FakeTimeProvider clock = new(TestClock.Start);

    private readonly StandInRow walk = new(Guid.NewGuid(), 0);

    private readonly StandInRow lunch = new(Guid.NewGuid(), 1);

    private readonly StandInRow shopping = new(Guid.NewGuid(), 2) { ArchivedAtUtc = ShoppingArchivedAtUtc };

    private readonly StandInRow bus = new(Guid.NewGuid(), 3);

    private StandInRow[] Goals => [walk, lunch, shopping, bus];

    // The clock has more digits than the datetime2(3) column keeps, which the row is given to the millisecond.
    [Fact]
    public void Archive_sets_ArchivedAtUtc_to_the_clocks_time_in_UTC_and_leaves_SortOrder()
    {
        clock.Advance(TimeSpan.FromTicks(4_567_891));

        Archiving.Archive(bus, clock);

        Assert.Equal(TestClock.Start.AddMilliseconds(456).UtcDateTime, bus.ArchivedAtUtc);
        Assert.Equal(DateTimeKind.Utc, bus.ArchivedAtUtc?.Kind);
        Assert.Equal(3, bus.SortOrder);
    }

    // The bus, the last goal, is archived after shopping, and returns to the end of the active goals, 2, not to the 3
    // it had: shopping keeps its 2, but an archived goal is not in the list.
    [Fact]
    public void Restore_of_the_last_row_archived_puts_it_at_the_end_of_the_active_rows()
    {
        Archiving.Archive(bus, clock);

        Archiving.Restore(bus, Goals);

        Assert.Null(bus.ArchivedAtUtc);
        Assert.Equal([0, 1, 2, 2], Goals.Select(goal => goal.SortOrder));
        Assert.Equal(ShoppingArchivedAtUtc, shopping.ArchivedAtUtc);
    }

    [Fact]
    public void Archiving_an_archived_row_changes_nothing()
    {
        clock.Advance(TimeSpan.FromMinutes(5));

        Archiving.Archive(shopping, clock);

        Assert.Equal(ShoppingArchivedAtUtc, shopping.ArchivedAtUtc);
        Assert.Equal(2, shopping.SortOrder);
    }

    // The end of the list is 4, which a restore that did not check would move it to.
    [Fact]
    public void Restoring_an_active_row_changes_nothing()
    {
        Archiving.Restore(lunch, Goals);

        Assert.Null(lunch.ArchivedAtUtc);
        Assert.Equal(1, lunch.SortOrder);
    }
}
