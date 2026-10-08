using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Hosting the app on a fake clock (design.md §7.5). The app asks <see cref="TimeProvider"/> for the time, and so do
/// Identity's security stamp check and the session cookie, whose options name no clock of their own, so one
/// <see cref="FakeTimeProvider"/> drives the sign-in time, the cookie's expiry, the stamp check, the 12-hour limit and
/// <c>MelbourneClock</c> alike. Data Protection keeps reading the real clock, which does not matter: its keys do not
/// expire within a test run.
/// </summary>
internal static class TestClock
{
    /// <summary>
    /// When a session test starts the clock: on a whole second, because the cookie and the sign-in time keep times to
    /// the second, and the session limits are tested to the second.
    /// </summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 9, 13, 30, 0, TimeSpan.Zero);

    /// <summary>Hosts the app that <paramref name="builder"/> builds on <paramref name="clock"/>.</summary>
    public static IWebHostBuilder UseClock(this IWebHostBuilder builder, FakeTimeProvider clock) =>
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock));
}
