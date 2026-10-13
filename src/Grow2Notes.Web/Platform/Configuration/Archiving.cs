namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// Archiving and restoring a configuration row, which takes the place of deleting it (design.md §3.7, A2). Each changes
/// nothing when the row is already archived or active, so the save finds no value changed and writes no event (D84).
/// </summary>
internal static class Archiving
{
    /// <summary>
    /// Archives the row at the clock's time in UTC. A row of an ordered list keeps its <c>SortOrder</c>, so the active
    /// rows can have gaps, which only a reorder closes. An archived row keeps the time it was archived at.
    /// </summary>
    public static void Archive(IArchivableRow row, TimeProvider timeProvider)
    {
        if (row.ArchivedAtUtc is not null)
        {
            return;
        }

        // To the millisecond, as the datetime2(3) column keeps it, so the archivedAtUtc that the audit event records is
        // the value stored, not one SQL Server rounds.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        row.ArchivedAtUtc = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }

    /// <summary>
    /// Restores the row to the end of its list (<see cref="ListOrder.End"/>): a goal to the end of the participant's
    /// goals, a common item to the end of its group, and a group to the end of the groups (A2). An active row stays
    /// where it is.
    /// </summary>
    /// <param name="row">The row to restore.</param>
    /// <param name="listRows">The rows of its list, with or without the row itself.</param>
    public static void Restore(IOrderedRow row, IEnumerable<IOrderedRow> listRows)
    {
        if (row.ArchivedAtUtc is null)
        {
            return;
        }

        // While the row is still archived, so that its own SortOrder does not count.
        row.SortOrder = ListOrder.End(listRows);
        row.ArchivedAtUtc = null;
    }
}
