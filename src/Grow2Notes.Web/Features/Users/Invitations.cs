using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The invite core (design.md §8.1 steps 1 and 2): creates a user as Invited, generates their authenticator key, and
/// makes their setup link. <c>admin bootstrap</c> invites the first manager with it, and Invite user will invite the
/// others. It only creates a new user: a resend and a sign-in reset, which need a new link for a user who exists, split
/// out its key, token and link step when they are built (S00.06.03, S00.06.04). It opens no transaction and writes no
/// audit event: its caller does both, so that the event commits or rolls back with the invite it records (S00.03.05's
/// Notes).
/// </summary>
internal sealed class Invitations(
    UserManager<ApplicationUser> users, TimeProvider timeProvider, IConfiguration configuration)
{
    /// <summary>
    /// Creates the user through <see cref="UserManager{TUser}"/> as Invited, with no password, so they cannot sign in,
    /// and an unconfirmed address, which is also their user name (§5.3); then generates their authenticator key; then
    /// makes the 7-day setup token (<see cref="SetupTokenProvider"/>) and the link that carries it.
    /// </summary>
    /// <param name="organisationId">The organisation the user joins.</param>
    /// <param name="displayName">The user's name, as shown on what they write and review.</param>
    /// <param name="email">The user's address, as given, and also their user name.</param>
    /// <param name="role">The user's role.</param>
    /// <param name="invitedByUserId">The inviting manager, or null for the first manager, whom no one invites.</param>
    /// <returns>
    /// The user and their setup link, or the errors with which Identity refused the user, such as
    /// <c>DuplicateEmail</c> for an address that another account holds, in which case nothing was created.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <c>App:Origin</c> is not set, so no link can be made, which is found before anything is created; or Identity
    /// refused to save the authenticator key of the user it had just created, which only a fault can cause and which
    /// the caller's transaction rolls back.
    /// </exception>
    public async Task<InviteResult> InviteAsync(
        Guid organisationId, string displayName, string email, UserRole role, Guid? invitedByUserId)
    {
        var origin = AppOrigin.Find(configuration) ?? throw new InvalidOperationException(
            $"{AppOrigin.Key} is not set, so no setup link can be made. Set it to the address the app is reached at.");

        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = displayName,
            Role = role,
            Status = UserStatus.Invited,
            InvitedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            InvitedByUserId = invitedByUserId,
            UserName = email,
            Email = email,
        };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            return InviteResult.Refused(created.Errors);
        }

        // Generating the key rotates the security stamp, which the setup token holds, so it comes first. Setup then
        // only reads the key, so nothing changes the stamp before setup completes, and the link works until then, for
        // up to 7 days (§8.1 step 1).
        var keyed = await users.ResetAuthenticatorKeyAsync(user);
        if (!keyed.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity refused the authenticator key of the user it had just created: " +
                $"{string.Join(" ", keyed.Errors.Select(error => error.Code))}.");
        }

        var token = await users.GenerateUserTokenAsync(
            user, SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose);

        // The token is in the fragment, so it never reaches a server (§8.1 step 2). It is base64, whose + the SPA's
        // URLSearchParams would read as a space, so it is escaped, which URLSearchParams reads back exactly.
        var setupLink = $"{new Uri(origin, "/setup").AbsoluteUri}#u={user.Id:D}&t={Uri.EscapeDataString(token)}";
        return InviteResult.Invited(user, setupLink);
    }
}
