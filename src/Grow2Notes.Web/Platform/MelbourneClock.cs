namespace Grow2Notes.Web.Platform;

/// <summary>
/// The server's one source of "today": the current calendar date in Australia/Melbourne, read from
/// <see cref="TimeProvider"/> and never from the machine's local time zone (A33, D37).
/// </summary>
internal sealed class MelbourneClock(TimeProvider timeProvider)
{
    // The zone's own rules give the daylight-saving changes, which a fixed UTC+10 would miss. Linux reads the IANA id
    // from the tz database; Windows maps it through ICU, so the app must not use invariant globalization.
    private static readonly TimeZoneInfo Melbourne = TimeZoneInfo.FindSystemTimeZoneById("Australia/Melbourne");

    public DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Melbourne).DateTime);
}
