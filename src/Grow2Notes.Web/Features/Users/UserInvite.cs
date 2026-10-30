using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The handler that invites a user into the tenant's organisation (design.md §4.11 <i>Invite user</i>, §6.6, §8.1
/// steps 1 and 2), as a handler class that takes the <see cref="Actor"/> (mcp-server.md §3.5, convention 1):
/// <c>POST /api/admin/users</c> calls it with <see cref="Actor.InApp"/>, and the <c>invite_user</c> tool calls it with
/// <see cref="Channel.Mcp"/>, with the same checks, email and audit event.
/// </summary>
/// <remarks>
/// One per scope, as the invite core, the audit writer and the transaction all use the scope's context. The
/// organisation is the tenant's (<see cref="ITenantContext"/>), never the request's (§5.9).
/// </remarks>
internal sealed class UserInvite(
    Grow2NotesDbContext db,
    ITenantContext tenant,
    Invitations invitations,
    IAuditWriter audit,
    IEmailSender emailSender)
{
    /// <summary>
    /// Checks the request's fields (<see cref="UserDetailsChecks.Check"/>); then, in one transaction, invites the
    /// person through the invite core (<see cref="Invitations.InviteAsync"/>) as Invited, with the actor as the one who
    /// invited them, writes <c>user.invited</c>, and sends the setup-link email, before the commit, so that a send that
    /// fails leaves no user and no event (T01's Notes).
    /// </summary>
    /// <param name="actor">Who invites the person, and by which channel.</param>
    /// <param name="request">The person's name, address and role.</param>
    /// <param name="reason">The reason to keep in the audit event, trimmed, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// <see cref="UserChangeResult.Done"/>; <see cref="ErrorCode.ValidationFailed"/> for a missing or overlong name, a
    /// missing or overlong address, one that Identity finds is not an address or cannot be a user name, such as one
    /// with a letter outside ASCII, and a missing role or one that is no role's name; or
    /// <see cref="ErrorCode.EmailInUse"/> for an address that an account holds, in any organisation and any letter
    /// case (A27). A refusal creates, writes and sends nothing.
    /// </returns>
    /// <exception cref="EmailNotSentException">
    /// The email could not be sent, so the transaction rolled back, and nothing was created or written.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Identity refused the user for a reason that only a fault can cause, and nothing was created.
    /// </exception>
    public async Task<UserChangeResult> InviteAsync(
        Actor actor, UserDetailsRequest request, string? reason, CancellationToken cancellationToken)
    {
        if (UserDetailsChecks.Check(request) is { Count: > 0 } fieldErrors)
        {
            return UserChangeResult.Invalid(fieldErrors);
        }

        // Each is there, as checked.
        var (displayName, email, role) =
            (request.DisplayName!, request.Email!, UserDetailsChecks.RoleNamed(request.Role)!.Value);
        var organisationId = tenant.OrganisationId;

        // Set by each attempt, as a transient failure runs the whole work again.
        IReadOnlyList<IdentityError> refusal = [];
        await db.InTransactionAsync(async ct =>
        {
            var invite = await invitations.InviteAsync(organisationId, displayName, email, role, actor.UserId);
            refusal = invite.Errors;
            if (!invite.Succeeded)
            {
                // Identity saved nothing, so the transaction commits nothing.
                return;
            }

            // The values as GET /api/admin/users names them, all of them, as a new row's event has (§6.6).
            var details = ChangeDetails.Of(
                before: null,
                new JsonObject
                {
                    ["email"] = email,
                    ["displayName"] = displayName,
                    ["role"] = role.ToString(),
                },
                extraDetails: reason is null ? null : new JsonObject { ["reason"] = reason });
            await audit.WriteAsync(
                organisationId, actor.UserId, AuditEventTypes.User.Invited, AuditEntityTypes.User, invite.User.Id,
                participantId: null, details, ct);
            await SetupLinkEmail.SendAsync(emailSender, email, invite.SetupLink, ct);
        }, cancellationToken);

        return refusal is [] ? UserChangeResult.Done : UserDetailsChecks.Refused(refusal);
    }
}
