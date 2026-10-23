using System.Globalization;
using Grow2Notes.Web.Platform;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Platform;

public sealed class MelbourneClockTests
{
    [Theory]
    [InlineData("2026-07-15T02:00:00Z", "2026-07-15")]
    // The design.md §14 M2 daylight-saving cases; a fixed UTC+10 calculation fails the last two.
    [InlineData("2026-10-03T14:30:00Z", "2026-10-04")]
    [InlineData("2026-10-04T13:30:00Z", "2026-10-05")]
    [InlineData("2027-04-03T13:30:00Z", "2027-04-04")]
    public void Today_is_the_Melbourne_date_at_the_current_instant(string utcNow, string expectedToday)
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));
        var clock = new MelbourneClock(timeProvider);

        Assert.Equal(DateOnly.Parse(expectedToday, CultureInfo.InvariantCulture), clock.Today());
    }

    // Midnight in Melbourne is 14:00 UTC before daylight saving starts on 4 October 2026 and 13:00 UTC after. A stored
    // time of unspecified kind is read as UTC too, not as the machine's local time.
    [Theory]
    [InlineData("2026-09-30T13:59:00", "2026-09-30")]
    [InlineData("2026-09-30T14:00:00", "2026-10-01")]
    [InlineData("2026-10-04T12:59:00", "2026-10-04")]
    [InlineData("2026-10-04T13:00:00", "2026-10-05")]
    public void DateOf_is_the_Melbourne_date_of_a_UTC_time_of_either_kind(string utc, string expectedDate)
    {
        var unspecified = DateTime.Parse(utc, CultureInfo.InvariantCulture);
        var expected = DateOnly.Parse(expectedDate, CultureInfo.InvariantCulture);

        Assert.Equal(expected, MelbourneClock.DateOf(DateTime.SpecifyKind(unspecified, DateTimeKind.Utc)));
        Assert.Equal(expected, MelbourneClock.DateOf(DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified)));
    }
}
