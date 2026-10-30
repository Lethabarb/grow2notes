using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Email;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The handler that sends a user of the tenant's organisation a new setup link (design.md §4.11 <i>Resend invite</i>
/// and <i>Reset sign-in</i>, §6.6, §8.1 step 2, §8.6), as a handler class that takes the <see cref="Actor"/>
/// (mcp-server.md §3.5, convention 1): <c>POST /api/admin/users/{id}/setup-link</c> calls it with
/// <see cref="Actor.InApp"/>, and the <c>reset_user_sign_in</c> tool calls it with
/// <see cref="Channel.Mcp"/>, with the same checks, email and audit event. It resends an Invited user's invite, resets
/// an Active user's sign-in, and refuses a Deactivated user, who is reactivated first (D75).
/// </summary>
/// <remarks>
/// One per scope, as the setup-link step, the reset step, the last-manager rule, the audit writer and the transaction
/// all use the scope's context.
/// </remarks>
internal sealed class UserSetupLink(
    UserChanges changes,
    SetupLinkIssuer setupLinks,
    SignInReset signInReset,
    LastManagerRule lastManager,
    IAuditWriter audit,
    IEmailSender emailSender)
{
    /// <summary>
    /// In one transaction (<see cref="UserChanges"/>), which loads the user and, for the app, checks
    /// <paramref name="ifMatch"/> first: unless the user is Deactivated or is their organisation's only Active manager
    /// (A26), for an Invited user, gives them a new authenticator key, which rotates their security stamp, so that
    /// their older links stop working, as does a setup that one of them started (A53), and a new link
    /// (<see cref="SetupLinkIssuer.IssueAsync"/>); for an Active user, resets their sign-in
    /// (<see cref="SignInReset.ResetAsync"/>), which also ends every session of theirs; then writes
    /// <c>user.setup_link_sent</c>, with the reason, when one is given, and sends the link in the setup-link email,
    /// before the commit, so that a send that fails leaves the user as they were and writes nothing (T01's Notes). For
    /// an assistant's tool the Deactivated check, then the last-manager rule, come before the version refusal that the
    /// channel holds back (A137 and A138).
    /// </summary>
    /// <param name="actor">Who sends the link, and by which channel.</param>
    /// <param name="userId">The user to send it to.</param>
    /// <param name="ifMatch">The request's <c>If-Match</c> values, or the tool's <c>expectedVersion</c>.</param>
    /// <param name="reason">The reason to keep in the audit event, trimmed, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    /// <see cref="UserChangeResult.Done"/>; <see cref="UserChangeResult.NotFound"/>;
    /// <see cref="ErrorCode.UserNotActive"/> for a Deactivated user; <see cref="ErrorCode.LastManager"/> for the
    /// organisation's only Active manager, the actor themselves among them; <see cref="ErrorCode.PreconditionRequired"/>
    /// or <see cref="ErrorCode.PreconditionFailed"/> for the version, or the latter when the user changed after they
    /// were loaded. A refusal changes, writes and sends nothing.
    /// </returns>
    /// <exception cref="EmailNotSentException">
    /// The email could not be sent, so the transaction rolled back, and nothing was changed or written.
    /// </exception>
    public Task<UserChangeResult> SendAsync(
        Actor actor, Guid userId, StringValues ifMatch, string? reason, CancellationToken cancellationToken) =>
        changes.ChangeAsync(actor, userId, ifMatch, async (user, refuseIfChanged, ct) =>
        {
            if (user.Status == UserStatus.Deactivated)
            {
                return UserChangeResult.Refused(ErrorCode.UserNotActive);
            }

            if (await lastManager.IsOnlyActiveManagerAsync(user, ct))
            {
                return UserChangeResult.Refused(ErrorCode.LastManager);
            }

            if (refuseIfChanged is { } refused)
            {
                return UserChangeResult.Refused(refused);
            }

            // No link means that another request changed the user since they were loaded, and nothing was saved.
            var setupLink = user.Status == UserStatus.Active
                ? await signInReset.ResetAsync(user)
                : await setupLinks.IssueAsync(user);
            if (setupLink is null)
            {
                return UserChangeResult.Refused(ErrorCode.PreconditionFailed);
            }

            // Who sent it to whom, by ID, with the reason when one is given: the address stays out of the audit trail
            // (A28).
            var details = reason is null ? null : new JsonObject { ["reason"] = reason };
            await audit.WriteAsync(
                user.OrganisationId, actor.UserId, AuditEventTypes.User.SetupLinkSent, AuditEntityTypes.User, user.Id,
                participantId: null, details, ct);

            // Identity gives every account an email, which RequireUniqueEmail requires.
            await SetupLinkEmail.SendAsync(emailSender, user.Email!, setupLink, ct);
            return UserChangeResult.Done;
        }, cancellationToken);
}
