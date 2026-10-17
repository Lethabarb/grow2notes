using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The setup-link step (design.md §8.1 steps 1 and 2), for a user who exists: the invite core takes it once it has
/// created the user, and a resend and a sign-in reset take it for a user who has one already (A23). It opens no
/// transaction and writes no audit event: its caller does both, so that the event commits or rolls back with the new
/// key it records.
/// </summary>
internal sealed class SetupLinkIssuer(UserManager<ApplicationUser> users, IConfiguration configuration)
{
    /// <summary>
    /// Gives <paramref name="user"/> a new authenticator key, which rotates their security stamp, so that every link
    /// made for them before stops working, as does a setup that one of those links started (A53); then makes the
    /// 7-day setup token (<see cref="SetupTokenProvider"/>) and the link that carries it.
    /// </summary>
    /// <param name="user">The user, as loaded or created on the scope's context.</param>
    /// <returns>
    /// <c>&lt;App:Origin&gt;/setup#u=&lt;user ID&gt;&amp;t=&lt;setup token&gt;</c>, which goes to the user and is kept
    /// nowhere, because it holds the token; or <see langword="null"/> when the user was changed after
    /// <paramref name="user"/> was loaded, which Identity reports as <c>ConcurrencyFailure</c>, in which case nothing
    /// was saved, and a later save in the scope fails the same way.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <c>App:Origin</c> is not set, so no link can be made, which is found before anything is changed; or Identity
    /// refused the new key for another reason, which only a fault can cause.
    /// </exception>
    public async Task<string?> IssueAsync(ApplicationUser user)
    {
        var setupPage = new Uri(AppOrigin.Require(configuration), "/setup");

        // Generating the key rotates the security stamp, which the setup token holds, so it comes first. Setup then
        // only reads the key, so nothing changes the stamp before setup completes, and the link works until then, for
        // up to 7 days (§8.1 step 1).
        var keyed = await users.ResetAuthenticatorKeyAsync(user);
        if (!keyed.Succeeded)
        {
            return keyed.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))
                ? null
                : throw new InvalidOperationException(
                    "Identity refused the user's new authenticator key: " +
                    $"{string.Join(" ", keyed.Errors.Select(error => error.Code))}.");
        }

        var token = await users.GenerateUserTokenAsync(
            user, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose);

        // The token is in the fragment, so it never reaches a server (§8.1 step 2). It is base64, whose + the SPA's
        // URLSearchParams would read as a space, so it is escaped, which URLSearchParams reads back exactly.
        return $"{setupPage.AbsoluteUri}#u={user.Id:D}&t={Uri.EscapeDataString(token)}";
    }
}
