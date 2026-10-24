using Grow2Notes.Web.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The rate limits in a test host (design.md §9.9). Every test of a suite calls the one host, and the host's clients
/// all have the same null address, so with the design's numbers a suite would soon be refused. The two hosts that tests
/// build on, <see cref="Grow2NotesFactory"/> and <see cref="AppWithoutDatabase"/>, lift the limits, so every other
/// test runs as if there were none; a test of the limits puts <see cref="RateLimitPermits.Design"/> back
/// (tests/README.md).
/// </summary>
internal static class TestRateLimits
{
    /// <summary>
    /// Replaces the permits with ones that no test reaches. A host that another test derives from this one keeps them,
    /// unless it registers permits of its own.
    /// </summary>
    public static IServiceCollection LiftRateLimits(this IServiceCollection services) =>
        services.AddSingleton(new RateLimitPermits(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
}
