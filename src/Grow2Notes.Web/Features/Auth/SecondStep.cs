using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The one rule for the second step of a password sign-in (design.md §8.2, D97): a user with two-factor on takes the
/// passkey step if they hold a passkey, and the 6-digit code from their authenticator app otherwise. The password
/// step chooses its answer by it, and the code step and the passkey step's options refuse a user it does not name,
/// so the three cannot drift apart.
/// </summary>
internal static class SecondStep
{
    /// <summary>True where the user's second step is a passkey, not a code.</summary>
    internal static async Task<bool> IsPasskeyAsync(UserManager<ApplicationUser> users, ApplicationUser user) =>
        (await users.GetPasskeysAsync(user)).Count > 0;
}
