using Grow2Notes.Web.Platform;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// The dates and times in a report file, ported from the SPA's <c>copy/format.ts</c> (microcopy.md §3). Each is put
/// together from numbers and the fixed English names below, so no culture's own wording can reach a file, and every
/// time is Melbourne's (D37, A33).
/// </summary>
internal static class ReportDates
{
    // Between a day and its month and between a time and am or pm, so that neither pair splits across two lines.
    private const char Nbsp = '\u00A0';

    private static readonly string[] Days =
        ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private static readonly string[] Months =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    /// <summary>"Thursday 1 October 2026".</summary>
    public static string DateLong(DateOnly date) =>
        $"{Days[(int)date.DayOfWeek]} {date.Day}{Nbsp}{Months[date.Month - 1]} {date.Year}";

    /// <summary>"Thu 1 Oct 2026".</summary>
    public static string DateWithYear(DateOnly date) => $"{DayAndMonth(date)} {date.Year}";

    /// <summary>"Fri 2 Oct 2026, 9:14 am", for a time in UTC.</summary>
    public static string DateTimeWithYear(DateTime utc)
    {
        var melbourne = MelbourneClock.InMelbourne(utc);
        return $"{DateWithYear(DateOnly.FromDateTime(melbourne))}, {TimeOf(melbourne)}";
    }

    /// <summary>"Thu 1 Oct, 4:42 pm", for a time in UTC.</summary>
    public static string DateTimeShort(DateTime utc)
    {
        var melbourne = MelbourneClock.InMelbourne(utc);
        return $"{DayAndMonth(DateOnly.FromDateTime(melbourne))}, {TimeOf(melbourne)}";
    }

    private static string DayAndMonth(DateOnly date) =>
        $"{Days[(int)date.DayOfWeek][..3]} {date.Day}{Nbsp}{Months[date.Month - 1][..3]}";

    private static string TimeOf(DateTime melbourne) => (melbourne.Hour, melbourne.Minute) switch
    {
        (12, 0) => "midday",
        (0, 0) => "midnight",
        var (hour, minute) =>
            $"{(hour % 12 == 0 ? 12 : hour % 12)}:{minute:D2}{Nbsp}{(hour < 12 ? "am" : "pm")}",
    };
}
