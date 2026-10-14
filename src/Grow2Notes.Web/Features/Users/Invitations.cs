using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The invite core (design.md §8.1 steps 1 and 2): creates a user as Invited, then gives them their authenticator key
/// and setup link through the setup-link step (<see cref="SetupLinkIssuer"/>), which a resend and a sign-in reset take
/// for a user who exists. <c>admin bootstrap</c> invites the first manager with it, and Invite user will invite the
/// others. It opens no transaction and writes no audit event: its caller does both, so that the event commits or rolls
/// back with the invite it records (S00.03.05's Notes).
/// </summary>
internal sealed class Invitations(
    UserManager<ApplicationUser> users,
    SetupLinkIssuer setupLinks,
    TimeProvider timeProvider,
    IConfiguration configuration)
{
    /// <summary>
    /// Creates the user through <see cref="UserManager{TUser}"/> as Invited, with no password, so they cannot sign in,
    /// and an unconfirmed address, which is also their user name (§5.3); then gives them their authenticator key, the
    /// 7-day setup token and the link that carries it (<see cref="SetupLinkIssuer.IssueAsync"/>).
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
        // Read here as well as by the setup-link step, so that a missing setting is found before the user is created.
        AppOrigin.Require(configuration);

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

        // Nothing else has the new user, so nothing can have changed them since they were created.
        var setupLink = await setupLinks.IssueAsync(user) ?? throw new InvalidOperationException(
            "Identity found the user it had just created changed before their authenticator key was saved.");
        return InviteResult.Invited(user, setupLink);
    }
}
