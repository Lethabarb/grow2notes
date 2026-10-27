using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.OAuth;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The handler that deactivates and reactivates a user of the tenant's organisation (design.md §3.7, §4.11
/// <i>Deactivate</i> and <i>Reactivate</i>, §6.6, §8.6 <i>Deactivate</i>, D75), as a handler class that takes the
/// <see cref="Actor"/> (mcp-server.md §3.5, convention 1): <c>POST /api/admin/users/{id}/deactivate</c> and
/// <c>…/reactivate</c> call it with <see cref="Actor.InApp"/>, and Release 2's <c>deactivate_user</c> tool will call it
/// with <see cref="Channel.Mcp"/>, with the same checks and audit event. Neither sends an email. Deactivating also
/// ends the user's assistant connections, in the same transaction.
/// </summary>
/// <remarks>
/// One per scope, as the transaction, the last-manager rule, the user manager's saves and the audit writer all use the
/// scope's context.
/// </remarks>
internal sealed class UserDeactivation(
    UserChanges changes,
    LastManagerRule lastManager,
    UserManager<ApplicationUser> users,
    IOpenIddictAuthorizationManager authorizations,
    IAuditWriter audit,
    TimeProvider timeProvider)
{
    /// <summary>
    /// In one transaction (<see cref="UserChanges.ChangeAsync"/>), which loads the user and checks
    /// <paramref name="ifMatch"/> first: unless the user is their organisation's only Active manager (A26), sets them
    /// Deactivated, with the time, locks them out for good and rotates their security stamp, which ends every session
    /// of theirs on its next request and every setup link made for them, and revokes their assistant authorisations,
    /// which ends every assistant connection of theirs on its next call and refuses its refresh; then writes
    /// <c>user.deactivated</c>, with no details, as the actor and the user's ID say who did it to whom. A user already
    /// Deactivated is left as they are, with nothing written, and the call succeeds, as mcp-server.md §5.1 has such a
    /// call succeed with nothing changed.
    /// </summary>
    /// <returns>
    /// <see cref="UserChangeResult.Done"/>; <see cref="UserChangeResult.NotFound"/>, or
    /// <see cref="ErrorCode.PreconditionRequired"/> or <see cref="ErrorCode.PreconditionFailed"/> for the version, as
    /// <see cref="UserChanges.ChangeAsync"/> gives them; <see cref="ErrorCode.LastManager"/> for the organisation's
    /// only Active manager, the actor themselves among them; or <see cref="ErrorCode.PreconditionFailed"/> when the
    /// user changed after they were loaded. A refusal changes and writes nothing.
    /// </returns>
    public Task<UserChangeResult> DeactivateAsync(
        Actor actor, Guid userId, StringValues ifMatch, CancellationToken cancellationToken) =>
        changes.ChangeAsync(userId, ifMatch, async (user, ct) =>
        {
            if (user.Status == UserStatus.Deactivated)
            {
                return UserChangeResult.Done;
            }

            if (await lastManager.IsOnlyActiveManagerAsync(user, ct))
            {
                return UserChangeResult.Refused(ErrorCode.LastManager);
            }

            user.Status = UserStatus.Deactivated;
            user.DeactivatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            if (!await TrySaveWithLockoutEndAsync(user, DateTimeOffset.MaxValue))
            {
                return UserChangeResult.Refused(ErrorCode.PreconditionFailed);
            }

            ThrowIfRefused(await users.UpdateSecurityStampAsync(user));
            await authorizations.RevokeConnectionsAsync(user.Id, ct);
            await WriteAsync(actor, user, AuditEventTypes.User.Deactivated, ct);
            return UserChangeResult.Done;
        }, cancellationToken);

    /// <summary>
    /// In one transaction (<see cref="UserChanges.ChangeAsync"/>), which loads the user and checks
    /// <paramref name="ifMatch"/> first: ends a Deactivated user's lockout and clears the time they were deactivated,
    /// so that the list gives one only while they are Deactivated; sets them Active if they have a password or a
    /// passkey, with which they sign in again, otherwise Invited, for Resend invite to send them a link (D75); then
    /// writes <c>user.reactivated</c>, with no details. It leaves their security stamp, which no session or link of
    /// theirs holds since their deactivation, and their count of failed sign-ins. A user who is not Deactivated is left
    /// as they are, with nothing written, and the call succeeds.
    /// </summary>
    /// <returns>
    /// <see cref="UserChangeResult.Done"/>; <see cref="UserChangeResult.NotFound"/>, or
    /// <see cref="ErrorCode.PreconditionRequired"/> or <see cref="ErrorCode.PreconditionFailed"/> for the version, as
    /// <see cref="UserChanges.ChangeAsync"/> gives them; or <see cref="ErrorCode.PreconditionFailed"/> when the user
    /// changed after they were loaded. A refusal changes and writes nothing.
    /// </returns>
    public Task<UserChangeResult> ReactivateAsync(
        Actor actor, Guid userId, StringValues ifMatch, CancellationToken cancellationToken) =>
        changes.ChangeAsync(userId, ifMatch, async (user, ct) =>
        {
            if (user.Status != UserStatus.Deactivated)
            {
                return UserChangeResult.Done;
            }

            user.Status = await users.HasPasswordAsync(user) || (await users.GetPasskeysAsync(user)).Count > 0
                ? UserStatus.Active
                : UserStatus.Invited;
            user.DeactivatedAtUtc = null;
            if (!await TrySaveWithLockoutEndAsync(user, lockoutEnd: null))
            {
                return UserChangeResult.Refused(ErrorCode.PreconditionFailed);
            }

            await WriteAsync(actor, user, AuditEventTypes.User.Reactivated, ct);
            return UserChangeResult.Done;
        }, cancellationToken);

    private static void ThrowIfRefused(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity refused to change the user: " +
                $"{string.Join(" ", result.Errors.Select(error => error.Code))}.");
        }
    }

    // Saves the user's changes with their new lockout end, in one save, the change's first: false when it finds them
    // changed since they were loaded, which Identity reports as ConcurrencyFailure, having saved nothing. Once it is
    // made, the transaction holds the user's row, so no later save of the change can find them changed.
    private async Task<bool> TrySaveWithLockoutEndAsync(ApplicationUser user, DateTimeOffset? lockoutEnd)
    {
        var saved = await users.SetLockoutEndDateAsync(user, lockoutEnd);
        if (saved.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            return false;
        }

        ThrowIfRefused(saved);
        return true;
    }

    private Task WriteAsync(Actor actor, ApplicationUser user, string eventType, CancellationToken cancellationToken) =>
        audit.WriteAsync(
            user.OrganisationId, actor.UserId, eventType, AuditEntityTypes.User, user.Id, participantId: null,
            details: null, cancellationToken);
}
