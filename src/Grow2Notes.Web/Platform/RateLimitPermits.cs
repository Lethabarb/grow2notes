namespace Grow2Notes.Web.Platform;

/// <summary>
/// The permits that each rate limit allows in a minute (design.md §9.9). It is a service of the app and never a
/// setting, so no configuration can change what production runs, which is <see cref="Design"/>; only a test host
/// replaces it.
/// </summary>
/// <param name="SignIn">The sign-in and setup endpoints, for each client address.</param>
/// <param name="Account">The password step and the code step, each for each account.</param>
/// <param name="Export">The two export endpoints together, for each user.</param>
/// <param name="General">Every other API endpoint, for each user, or for each client address when signed out.</param>
internal sealed record RateLimitPermits(int SignIn, int Account, int Export, int General)
{
    /// <summary>The numbers of design.md §9.9.</summary>
    public static readonly RateLimitPermits Design = new(30, 10, 10, 300);
}
