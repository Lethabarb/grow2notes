using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The reset step (design.md §8.6 <i>Reset sign-in</i>), built once for Reset sign-in, an email change and
/// <c>admin reset-signin</c>: it takes away every way the user signs in, then gives them a new authenticator key and
/// setup link through the setup-link step (<see cref="SetupLinkIssuer"/>), so that they set up again (§8.1 steps 3 to
/// 5). It opens no transaction, writes no audit event and sends nothing: its caller does each, so that the event and
/// the email go with the reset they record, or with none.
/// </summary>
/// <remarks>
/// One per scope, as its <see cref="UserManager{TUser}"/> saves through the scope's context, and so joins the
/// transaction its caller opened there, without which a failure part of the way through would leave part of the reset
/// saved.
/// </remarks>
internal sealed class SignInReset(UserManager<ApplicationUser> users, SetupLinkIssuer setupLinks)
{
    /// <summary>
    /// Sets <paramref name="user"/> Invited, unless they are Deactivated, who stay so until a manager reactivates them
    /// (D75); removes their password and each of their passkeys, and turns off their two-factor sign-in; and gives them
    /// a new key and link (<see cref="SetupLinkIssuer.IssueAsync"/>), whose new security stamp ends every session of
    /// theirs on its next request, every link made for them before, and any setup that one of those started (A53). It
    /// leaves the dates of their invite and activation, their confirmed address and their lockout as they are, as a
    /// resend does: completing setup sets the ones it changes (§8.1 step 5).
    /// </summary>
    /// <param name="user">The user, as loaded on the scope's context.</param>
    /// <returns>
    /// The new setup link, as <see cref="SetupLinkIssuer.IssueAsync"/> gives it; or <see langword="null"/> when the
    /// user was changed after <paramref name="user"/> was loaded, which Identity reports as <c>ConcurrencyFailure</c>,
    /// in which case nothing was saved.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Identity refused a step for another reason, which only a fault can cause, and the caller's transaction rolls
    /// back what the steps before it saved.
    /// </exception>
    public async Task<string?> ResetAsync(ApplicationUser user)
    {
        if (user.Status != UserStatus.Deactivated)
        {
            user.Status = UserStatus.Invited;
        }

        // The status goes first, on its own, as Identity saves each step separately: this save finds the user changed
        // since they were loaded and saves nothing, or holds their row for the rest of the caller's transaction, so
        // that no later step can find them changed and stop with the reset half saved.
        var first = await users.UpdateAsync(user);
        if (first.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            return null;
        }

        ThrowIfRefused(first);
        if (await users.HasPasswordAsync(user))
        {
            ThrowIfRefused(await users.RemovePasswordAsync(user));
        }

        foreach (var passkey in await users.GetPasskeysAsync(user))
        {
            ThrowIfRefused(await users.RemovePasskeyAsync(user, passkey.CredentialId));
        }

        if (await users.GetTwoFactorEnabledAsync(user))
        {
            ThrowIfRefused(await users.SetTwoFactorEnabledAsync(user, false));
        }

        return await setupLinks.IssueAsync(user) ?? throw new InvalidOperationException(
            "Identity found the user changed while the transaction held their row.");
    }

    private static void ThrowIfRefused(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity refused to reset the user's sign-in: " +
                $"{string.Join(" ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
