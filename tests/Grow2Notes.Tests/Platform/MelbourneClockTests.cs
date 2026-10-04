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
}
