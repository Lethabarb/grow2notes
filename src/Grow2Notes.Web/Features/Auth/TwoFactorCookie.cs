namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// Identity's two-factor cookie (<c>IdentityConstants.TwoFactorUserIdScheme</c>, design.md §8.4), which holds a
/// password sign-in's code step, from the accepted password to the code (§8.2), and a passkey ceremony, setup's or
/// sign-in's, from its options to its credential. Each answer that starts one replaces the other.
/// </summary>
internal static class TwoFactorCookie
{
    /// <summary>
    /// How long the cookie lasts from when it is set, never renewed: the code step's 5 minutes (D69), which are also a
    /// passkey ceremony's. Program.cs sets it on the cookie, and every test of the cookie reads it, so the code step's
    /// and the ceremonies' windows cannot drift apart.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
}
