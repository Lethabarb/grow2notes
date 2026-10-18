namespace Grow2Notes.Web.Platform;

/// <summary>
/// The time to store in a <c>datetime2(3)</c> column (design.md §5.3), which keeps milliseconds.
/// </summary>
internal static class StoredTime
{
    /// <summary>
    /// <paramref name="timeProvider"/>'s time in UTC, cut to the millisecond, so that the time an answer or an audit
    /// event gives is the time stored, not one that SQL Server rounds.
    /// </summary>
    public static DateTime UtcNow(TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMillisecond));
    }
}
