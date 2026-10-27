using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The handler that changes the name, email address or role of a user of the tenant's organisation (design.md §4.11
/// <i>Edit</i>, §6.6, §8.4, §8.6, D75, D76), as a handler class that takes the <see cref="Actor"/> (mcp-server.md
/// §3.5, convention 1): <c>PUT /api/admin/users/{id}</c> calls it with <see cref="Actor.InApp"/>.
/// </summary>
/// <remarks>
/// One per scope, as the transaction, the last-manager rule, the user manager's save, the reset step and the audit
/// writer all use the scope's context.
/// </remarks>
internal sealed class UserEdit(
    UserChanges changes,
    LastManagerRule lastManager,
    UserManager<ApplicationUser> users,
    SignInReset signInReset,
    IAuditWriter audit,
    IEmailSender emailSender)
{
    /// <summary>
    /// Checks the request's fields (<see cref="UserDetailsChecks.Check"/>); then, in one transaction
    /// (<see cref="UserChanges.ChangeAsync"/>), which loads the user and checks <paramref name="ifMatch"/> first, gives
    /// the user the request's name, address and role in one save, and writes <c>user.updated</c> with the old and new
    /// values of those that changed. That save leaves the security stamp, so after a new name or role the user's
    /// sessions go on, a new role applying on their next request (§8.4), and so they do after an address that differs
    /// from theirs in letter case alone, which is saved as typed (D76). A new address also marks the address
    /// unconfirmed, until setup confirms it, and resets the user's sign-in as Reset sign-in does
    /// (<see cref="SignInReset.ResetAsync"/>), which ends their sessions and their links: an Invited or Active user is
    /// then Invited, <c>user.setup_link_sent</c> is written, and the link is sent to the new address, before the
    /// commit, so that a send that fails leaves the user as they were and writes nothing; a Deactivated user stays so,
    /// and gets no link (D75). Values that the user has already change and write nothing.
    /// </summary>
    /// <returns>
    /// <see cref="UserChangeResult.Done"/>; <see cref="ErrorCode.ValidationFailed"/> for a field, as an invite gives
    /// it, before the user is looked for, or, for an address that Identity finds is not one or cannot be a user name,
    /// as it saves; <see cref="UserChangeResult.NotFound"/>, or <see cref="ErrorCode.PreconditionRequired"/> or
    /// <see cref="ErrorCode.PreconditionFailed"/> for the version, as <see cref="UserChanges.ChangeAsync"/> gives
    /// them; <see cref="ErrorCode.LastManager"/> to make a worker or Support of, or give a new address to, the
    /// organisation's only Active manager, the actor themselves among them (A26);
    /// <see cref="ErrorCode.EmailInUse"/> for an address that another account holds, in any organisation and any
    /// letter case (A27); or <see cref="ErrorCode.PreconditionFailed"/> when the user changed after they were loaded.
    /// A refusal changes, writes and sends nothing.
    /// </returns>
    /// <exception cref="EmailNotSentException">
    /// The email could not be sent, so the transaction rolled back, and nothing was changed or written.
    /// </exception>
    public async Task<UserChangeResult> EditAsync(
        Actor actor,
        Guid userId,
        UserDetailsRequest request,
        StringValues ifMatch,
        CancellationToken cancellationToken)
    {
        if (UserDetailsChecks.Check(request) is { Count: > 0 } fieldErrors)
        {
            return UserChangeResult.Invalid(fieldErrors);
        }

        // Each is there, as checked.
        var (displayName, email, role) =
            (request.DisplayName!, request.Email!, UserDetailsChecks.RoleNamed(request.Role)!.Value);
        return await changes.ChangeAsync(userId, ifMatch, async (user, ct) =>
        {
            // Identity gives every account an email, which RequireUniqueEmail requires.
            var details = ChangeDetails.Of(
                Values(user.Email!, user.DisplayName, user.Role), Values(email, displayName, role));
            if (details is null)
            {
                return UserChangeResult.Done;
            }

            // Compared normalised, as Identity compares addresses, so a change of letter case alone is no new address.
            var isNewAddress = users.NormalizeEmail(email) != user.NormalizedEmail;
            if ((isNewAddress || role != UserRole.Manager) && await lastManager.IsOnlyActiveManagerAsync(user, ct))
            {
                return UserChangeResult.Refused(ErrorCode.LastManager);
            }

            // One save, which checks the address and the user name together, so that one it refuses saves nothing.
            // SetEmailAsync and SetUserNameAsync would each save on their own and rotate the stamp (T04's Notes).
            user.DisplayName = displayName;
            user.Role = role;
            user.Email = email;
            user.UserName = email;
            if (isNewAddress)
            {
                // Until setup confirms it (§8.1 step 5), as SetEmailAsync would mark it.
                user.EmailConfirmed = false;
            }

            var saved = await users.UpdateAsync(user);
            if (!saved.Succeeded)
            {
                return saved.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))
                    ? UserChangeResult.Refused(ErrorCode.PreconditionFailed)
                    : UserDetailsChecks.Refused([.. saved.Errors]);
            }

            await WriteAsync(actor, user, AuditEventTypes.User.Updated, details, ct);
            if (!isNewAddress)
            {
                return UserChangeResult.Done;
            }

            // The save above holds the user's row for the rest of the transaction, so the reset cannot find them
            // changed.
            var setupLink = await signInReset.ResetAsync(user) ?? throw new InvalidOperationException(
                "Identity found the user changed while the transaction held their row.");
            if (user.Status == UserStatus.Deactivated)
            {
                return UserChangeResult.Done;
            }

            // As Reset sign-in writes it: who sent it to whom, by ID, with no details (A28).
            await WriteAsync(actor, user, AuditEventTypes.User.SetupLinkSent, details: null, ct);
            await SetupLinkEmail.SendAsync(emailSender, email, setupLink, ct);
            return UserChangeResult.Done;
        }, cancellationToken);
    }

    // The values as GET /api/admin/users names them, for the old and new values of user.updated (S00.06.06's Notes).
    private static JsonObject Values(string email, string displayName, UserRole role) => new()
    {
        ["email"] = email,
        ["displayName"] = displayName,
        ["role"] = role.ToString(),
    };

    private Task WriteAsync(
        Actor actor,
        ApplicationUser user,
        string eventType,
        JsonObject? details,
        CancellationToken cancellationToken) =>
        audit.WriteAsync(
            user.OrganisationId, actor.UserId, eventType, AuditEntityTypes.User, user.Id, participantId: null, details,
            cancellationToken);
}
