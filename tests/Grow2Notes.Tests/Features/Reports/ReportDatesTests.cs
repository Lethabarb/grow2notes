using System.Globalization;
using Grow2Notes.Web.Features.Reports;

namespace Grow2Notes.Tests.Features.Reports;

public sealed class ReportDatesTests
{
    private const string Nbsp = "\u00A0";

    private static DateTime Utc(string utc) => DateTime.Parse(
        utc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    [Fact]
    public void DateLong_names_the_day_and_the_month_in_full()
    {
        Assert.Equal($"Thursday 1{Nbsp}October 2026", ReportDates.DateLong(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void DatePlain_is_the_day_the_month_in_full_and_the_year_with_no_weekday()
    {
        Assert.Equal($"1{Nbsp}January 2026", ReportDates.DatePlain(new DateOnly(2026, 1, 1)));
        Assert.Equal($"30{Nbsp}September 2026", ReportDates.DatePlain(new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public void DateWithYear_is_the_short_day_and_month_with_the_year()
    {
        Assert.Equal($"Thu 1{Nbsp}Oct 2026", ReportDates.DateWithYear(new DateOnly(2026, 10, 1)));
    }

    // 23:14 UTC on 1 October is 9:14 am on 2 October in Melbourne, before daylight saving starts on 4 October.
    [Fact]
    public void DateTimeWithYear_is_the_Melbourne_day_with_its_year_and_time()
    {
        Assert.Equal($"Fri 2{Nbsp}Oct 2026, 9:14{Nbsp}am", ReportDates.DateTimeWithYear(Utc("2026-10-01T23:14:00Z")));
    }

    // A time read back from the database has unspecified kind and is still UTC.
    [Fact]
    public void DateTimeShort_reads_a_time_of_unspecified_kind_as_UTC()
    {
        var stored = new DateTime(2026, 10, 1, 6, 42, 0, DateTimeKind.Unspecified);

        Assert.Equal($"Thu 1{Nbsp}Oct, 4:42{Nbsp}pm", ReportDates.DateTimeShort(stored));
    }

    [Theory]
    [InlineData("2026-10-01T02:00:00Z", $"Thu 1{Nbsp}Oct, midday")]
    [InlineData("2026-09-30T14:00:00Z", $"Thu 1{Nbsp}Oct, midnight")]
    // After daylight saving starts, Melbourne is UTC+11.
    [InlineData("2026-10-05T02:30:00Z", $"Mon 5{Nbsp}Oct, 1:30{Nbsp}pm")]
    public void DateTimeShort_says_midday_and_midnight_and_follows_daylight_saving(string utc, string expected)
    {
        Assert.Equal(expected, ReportDates.DateTimeShort(Utc(utc)));
    }

    [Theory]
    [InlineData("2026-09-14", "Monday 14.09.2026")]
    [InlineData("2026-10-01", "Thursday 01.10.2026")]
    [InlineData("2026-01-05", "Monday 05.01.2026")]
    public void DayDotted_is_the_weekday_and_the_numbers_of_the_date(string date, string expected)
    {
        Assert.Equal(expected, ReportDates.DayDotted(DateOnly.Parse(date, CultureInfo.InvariantCulture)));
    }

    // The weekday is fixed English wording and the digits are ASCII, whatever the current culture says.
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    [InlineData("ja-JP")]
    public void DayDotted_is_invariant_English_under_a_non_English_current_culture(string culture)
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Assert.Equal("Thursday 01.10.2026", ReportDates.DayDotted(new DateOnly(2026, 10, 1)));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
