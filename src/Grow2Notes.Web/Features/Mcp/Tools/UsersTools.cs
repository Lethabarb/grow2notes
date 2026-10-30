using System.ComponentModel;
using System.Diagnostics;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>
/// The tools that look the organisation's user accounts up and change a person's access (mcp-server.md §5.2). They call
/// the handlers that the Users screen calls, as <see cref="Actor.InMcp"/>, and filter and map the list themselves:
/// <see cref="UserList"/> keeps the explicit organisation predicate that scopes <c>AspNetUsers</c>. Nothing about
/// signing in, and no time or ID, is returned; the IDs of the accounts a call names are reported to
/// <see cref="McpCall"/> for the call's audit row. A change finds the account by its email (an invite has none yet),
/// applies Support's limits from the role claim (<see cref="ToolActor.IsSupport"/>), and asks the person to confirm
/// (<see cref="ToolConfirmation"/>) before the handler acts. The handler's own refusals come only when the person
/// confirms (A137). A confirmed deactivation, invite or sign-in reset first uses one of the user's permits for the hour
/// (<see cref="ConfirmedChangeLimit"/>, A139), so everything that returns before the confirmation is read uses none.
/// </summary>
[McpServerToolType]
internal sealed class UsersTools(
    UserList users,
    UserDeactivation deactivation,
    UserInvite invite,
    UserSetupLink setupLink,
    ToolConfirmation confirmation,
    ConfirmedChangeLimit limit,
    IHttpContextAccessor httpContextAccessor,
    McpCall call)
{
    private const string DeactivateAction = "deactivate_user";

    private const string InviteAction = "invite_user";

    private const string ResetAction = "reset_user_sign_in";

    private const string BlankRole = "Choose worker or manager";

    private static readonly ToolError NoSuchUser = ToolError.NotFound(
        "No user has that email in this organisation. Nothing was changed. Find them with list_users.");

    private static readonly ToolError AccountChanged = new(
        ErrorCode.PreconditionFailed.Code,
        "This account changed after the person was asked. Nothing was changed. " +
        "Call the tool again to ask them afresh.");

    private static readonly ToolError LastManager = new(
        ErrorCode.LastManager.Code,
        "This is the organisation's only active manager, so they cannot be deactivated. Nothing was changed.");

    private static readonly ToolError LastManagerReset = new(
        ErrorCode.LastManager.Code,
        "This is the organisation's only active manager, so their sign-in cannot be reset. Nothing was changed.");

    private static readonly ToolError NotActive = new(
        ErrorCode.UserNotActive.Code,
        "This account is deactivated. Reactivate it in Grow2Notes first. Nothing was changed.");

    private static readonly ToolError EmailInUse = new(
        ErrorCode.EmailInUse.Code, "This email already has a Grow2Notes account. Nothing was changed.");

    [McpServerTool(
        Name = "list_users",
        Title = "List user accounts",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(ListedUsers))]
    [Description(
        "Lists the user accounts of this organisation, sorted by display name then email. Each has its email, " +
        "display name, role (worker, manager or support), status (invited or active) and the expectedVersion that a " +
        "later change needs. Every word of search must appear in the display name or the email; case is ignored. " +
        "Deactivated accounts are listed only when search is exactly their email. Never returns sign-in details. " +
        ToolResults.TypedByPeople)]
    public async Task<CallToolResult> ListUsersAsync(
        [Description(
            "Part of a display name or email: at most 256 characters. Each word must appear in the display name or " +
            "the email. Leave it out to list everyone. A deactivated account is listed only when this is its email.")]
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (search is not null && TextInput.Refusal("search", "Search", search, Limits.Email) is { } refusal)
        {
            return refusal.ToResult();
        }

        // The words are split as ParticipantLookups.FindAsync splits them.
        var words = (search ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var exactEmail = search?.Trim();
        var all = await users.ListAsync(ToolActor.Of(httpContextAccessor), cancellationToken);
        var listed = all
            .Where(user => user.Status == nameof(UserStatus.Deactivated)
                ? string.Equals(user.Email, exactEmail, StringComparison.OrdinalIgnoreCase)
                : ParticipantLookups.Matches(words, user.DisplayName, user.Email))
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        call.Returned(McpCall.UserIds, [.. listed.Select(user => user.Id)]);
        return ToolResults.Of(new ListedUsers(
            [.. listed.Select(user => new ListedUser(
                user.Email, user.DisplayName, RoleOf(user), StatusOf(user), user.ConcurrencyStamp))]));
    }

    [McpServerTool(
        Name = DeactivateAction,
        Title = "Deactivate a user",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(AccessChange))]
    [Description(
        "Deactivates a user account of this organisation, found by email: they are signed out everywhere at once and " +
        "can't sign in; their notes stay. Never acts on the first call. If your app shows Grow2Notes' confirmation " +
        "form, the person answers it there. Otherwise call first without confirm, show the person the preview, and " +
        "call again with confirm: true and the preview's expectedVersion only after they say yes in this " +
        "conversation. Support can deactivate worker accounts only and must send a reason. Someone already " +
        "deactivated answers changed: false. Reactivate in Grow2Notes, not here. " + ToolResults.TypedByPeople)]
    public async Task<CallToolResult> DeactivateUserAsync(
        RequestContext<CallToolRequestParams> context,
        [Description("The email of the account, as list_users gives it.")]
        string email,
        [Description("Why, and who asked: at most 200 characters. Required for Support; kept in the audit log.")]
        string? reason = null,
        [Description("true only after the person has seen the preview and said yes in this conversation.")]
        bool confirm = false,
        [Description("The expectedVersion that the preview returned. Needed with confirm: true.")]
        string? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (TextInput.Refusal("email", "Email", email, Limits.Email) is { } emailRefusal)
        {
            return emailRefusal.ToResult();
        }

        if (reason is not null
            && TextInput.Refusal("reason", "Reason", reason, Limits.UserChangeReason) is { } reasonRefusal)
        {
            return reasonRefusal.ToResult();
        }

        var target = await FindAsync(email, cancellationToken);
        if (target is null)
        {
            return NoSuchUser.ToResult();
        }

        call.Returned(McpCall.UserIds, [target.Id]);
        if (ToolActor.IsSupport(httpContextAccessor) && SupportRefusal(target.Role, reason) is { } refusal)
        {
            return refusal.ToResult();
        }

        if (target.Status == nameof(UserStatus.Deactivated))
        {
            return ToolResults.Of(new AccessChange(
                "done", DeactivateAction, UserOf(target, StatusOf(target)), Changed: false));
        }

        var actor = ToolActor.Of(httpContextAccessor);
        var answer = confirmation.Read(context, actor, confirm, expectedVersion);
        switch (answer.Step)
        {
            case ConfirmationStep.Ask:
                var effect = $"{target.DisplayName} will be signed out everywhere now and can't sign in. " +
                    "Their notes stay.";
                return confirmation.Ask(
                    context,
                    actor,
                    target.ConcurrencyStamp,
                    $"Deactivate {target.DisplayName} ({target.Email}, {RoleOf(target)})? {effect}",
                    $"Yes, deactivate {target.DisplayName}",
                    ToolResults.Of(new AccessChange(
                        "preview",
                        DeactivateAction,
                        UserOf(target, StatusOf(target)),
                        effect,
                        target.ConcurrencyStamp,
                        "Show this to the person. If they say yes, call deactivate_user again with confirm: true " +
                        "and this expectedVersion.")));
            case ConfirmationStep.StateRefused:
                return ToolConfirmation.StateRefused.ToResult();
            case ConfirmationStep.Declined:
                return ToolResults.Of(new AccessChange(
                    "declined", DeactivateAction, Message: "Nothing was changed."));
            case ConfirmationStep.Confirmed:
                if (limit.TryCount(actor.UserId) is { } limited)
                {
                    return limited.ToResult();
                }

                var result = await deactivation.DeactivateAsync(
                    actor,
                    target.Id,
                    IfMatch.FromExpectedVersion(answer.ExpectedVersion),
                    Trimmed(reason),
                    cancellationToken);

                // The target is the account as it was read before the change, so the state it ends in is said here.
                return RefusalOf(result, LastManager)?.ToResult() ?? ToolResults.Of(new AccessChange(
                    "done",
                    DeactivateAction,
                    UserOf(target, nameof(UserStatus.Deactivated).ToLowerInvariant()),
                    Changed: result.Changed));
            default:
                throw new UnreachableException($"A confirmation cannot be at {answer.Step}.");
        }
    }

    [McpServerTool(
        Name = InviteAction,
        Title = "Invite a user",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(AccessChange))]
    [Description(
        "Invites a person to this organisation as a worker or a manager: Grow2Notes emails them a setup link that " +
        "works once and lasts 7 days, and every user can read every participant's notes. Never acts on the first " +
        "call. If your app shows Grow2Notes' confirmation form, the person answers it there. Otherwise call first " +
        "without confirm, show the person the preview, and call again with the same arguments and confirm: true " +
        "only after they say yes in this conversation. Support can invite workers only and must send a reason. An " +
        "email that already has an account is refused when confirmed. Support accounts, and changes to a user's " +
        "name, email or role, are made in Grow2Notes, not here. " + ToolResults.TypedByPeople)]
    public async Task<CallToolResult> InviteUserAsync(
        RequestContext<CallToolRequestParams> context,
        [Description("The person's email address: at most 256 characters.")]
        string email,
        [Description("The person's name as Grow2Notes shows it: at most 100 characters.")]
        string displayName,
        [Description("worker or manager. Support can invite workers only.")]
        string role,
        [Description("Why, and who asked: at most 200 characters. Required for Support; kept in the audit log.")]
        string? reason = null,
        [Description("true only after the person has seen the preview and said yes in this conversation.")]
        bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        if (TextInput.Refusal("email", "Email", email, Limits.Email) is { } emailRefusal)
        {
            return emailRefusal.ToResult();
        }

        if (TextInput.Refusal("displayName", "Display name", displayName, Limits.DisplayName) is { } nameRefusal)
        {
            return nameRefusal.ToResult();
        }

        // The role is never stored, so its bound is only the shared text check.
        if (TextInput.Refusal("role", "Role", role, Limits.DisplayName) is { } roleRefusal)
        {
            return roleRefusal.ToResult();
        }

        if (reason is not null
            && TextInput.Refusal("reason", "Reason", reason, Limits.UserChangeReason) is { } reasonRefusal)
        {
            return reasonRefusal.ToResult();
        }

        // The tool takes the two exact words; the app's own check then finds a role in the name it is given.
        var roleName = role switch
        {
            "worker" => nameof(UserRole.Worker),
            "manager" => nameof(UserRole.Manager),
            _ => null,
        };
        var details = new UserDetailsRequest(displayName.Trim(), email.Trim(), roleName);
        var fieldErrors = UserDetailsChecks.Check(details);

        // The role rule is the tool's: a blank role is a field error, and any other word is not allowed.
        fieldErrors.Remove(nameof(UserDetailsRequest.Role));
        if (string.IsNullOrWhiteSpace(role))
        {
            fieldErrors[nameof(UserDetailsRequest.Role)] = [BlankRole];
        }

        if (fieldErrors.Count > 0)
        {
            return ToolError.ValidationFailed(fieldErrors).ToResult();
        }

        if (roleName is null)
        {
            return ToolError.InviteRoleNotAllowed.ToResult();
        }

        if (ToolActor.IsSupport(httpContextAccessor) && SupportRefusal(roleName, reason) is { } refusal)
        {
            return refusal.ToResult();
        }

        // There is no row yet, so the account is named from the arguments, as the invite would make it.
        var invitee = new AccessChangeUser(
            details.Email!, details.DisplayName!, role, nameof(UserStatus.Invited).ToLowerInvariant());
        var actor = ToolActor.Of(httpContextAccessor);
        var answer = confirmation.Read(context, actor, confirm, expectedVersion: null);
        switch (answer.Step)
        {
            case ConfirmationStep.Ask:
                var effect = $"Grow2Notes will email a setup link to {invitee.Email}. It works once and lasts " +
                    $"7 days. As a {role}, {invitee.DisplayName} will be able to read every participant's notes.";
                return confirmation.Ask(
                    context,
                    actor,
                    version: null,
                    $"Invite {invitee.DisplayName} ({invitee.Email}, {role})? {effect}",
                    $"Yes, invite {invitee.DisplayName}",
                    ToolResults.Of(new AccessChange(
                        "preview",
                        InviteAction,
                        invitee,
                        effect,
                        ToConfirm: "Show this to the person. If they say yes, call invite_user again with the same " +
                            "arguments and confirm: true.")));
            case ConfirmationStep.StateRefused:
                return ToolConfirmation.StateRefused.ToResult();
            case ConfirmationStep.Declined:
                return ToolResults.Of(new AccessChange("declined", InviteAction, Message: "Nothing was changed."));
            case ConfirmationStep.Confirmed:
                if (limit.TryCount(actor.UserId) is { } limited)
                {
                    return limited.ToResult();
                }

                var result = await invite.InviteAsync(actor, details, Trimmed(reason), cancellationToken);
                return RefusalOf(result)?.ToResult()
                    ?? ToolResults.Of(new AccessChange("done", InviteAction, invitee, Changed: true));
            default:
                throw new UnreachableException($"A confirmation cannot be at {answer.Step}.");
        }
    }

    [McpServerTool(
        Name = ResetAction,
        Title = "Reset a user's sign-in or resend their invite",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(AccessChange))]
    [Description(
        "Sends a user of this organisation, found by email, a new setup link. For an active user it resets their " +
        "sign-in: their password, authenticator and passkeys are removed and they are signed out everywhere. For an " +
        "invited user it resends the invite, and the old link stops working. Before a reset, ask the person whether " +
        "they have confirmed who is asking, by phone or in person. Never acts on the first call. If your app shows " +
        "Grow2Notes' confirmation form, the person answers it there. Otherwise call first without confirm, show the " +
        "person the preview, and call again with confirm: true and the preview's expectedVersion only after they " +
        "say yes in this conversation. Support can act on worker accounts only and must send a reason. A " +
        "deactivated user cannot be reset: reactivate them in Grow2Notes first. " + ToolResults.TypedByPeople)]
    public async Task<CallToolResult> ResetUserSignInAsync(
        RequestContext<CallToolRequestParams> context,
        [Description("The email of the account, as list_users gives it.")]
        string email,
        [Description("Why, and who asked: at most 200 characters. Required for Support; kept in the audit log.")]
        string? reason = null,
        [Description("true only after the person has seen the preview and said yes in this conversation.")]
        bool confirm = false,
        [Description("The expectedVersion that the preview returned. Needed with confirm: true.")]
        string? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (TextInput.Refusal("email", "Email", email, Limits.Email) is { } emailRefusal)
        {
            return emailRefusal.ToResult();
        }

        if (reason is not null
            && TextInput.Refusal("reason", "Reason", reason, Limits.UserChangeReason) is { } reasonRefusal)
        {
            return reasonRefusal.ToResult();
        }

        var target = await FindAsync(email, cancellationToken);
        if (target is null)
        {
            return NoSuchUser.ToResult();
        }

        call.Returned(McpCall.UserIds, [target.Id]);
        if (ToolActor.IsSupport(httpContextAccessor) && SupportRefusal(target.Role, reason) is { } refusal)
        {
            return refusal.ToResult();
        }

        var actor = ToolActor.Of(httpContextAccessor);
        var answer = confirmation.Read(context, actor, confirm, expectedVersion);
        switch (answer.Step)
        {
            case ConfirmationStep.Ask:
                var resend = target.Status == nameof(UserStatus.Invited);
                var name = target.DisplayName;
                var effect = resend
                    ? $"This sends {name} a new setup link at {target.Email}. The old link stops working."
                    : $"This removes {name}'s password, authenticator and passkeys, signs them out everywhere and " +
                        $"emails a new setup link to {target.Email}. Confirm who is asking first, by phone or in " +
                        "person.";
                return confirmation.Ask(
                    context,
                    actor,
                    target.ConcurrencyStamp,
                    resend
                        ? $"Resend the invite to {name} ({target.Email}, {RoleOf(target)})? {effect}"
                        : $"Reset sign-in for {name} ({target.Email}, {RoleOf(target)})? {effect}",
                    resend ? $"Yes, send {name} a new link" : $"Yes, reset {name}'s sign-in",
                    ToolResults.Of(new AccessChange(
                        "preview",
                        ResetAction,
                        UserOf(target, StatusOf(target)),
                        effect,
                        target.ConcurrencyStamp,
                        "Show this to the person. If they say yes, call reset_user_sign_in again with confirm: true " +
                        "and this expectedVersion.")));
            case ConfirmationStep.StateRefused:
                return ToolConfirmation.StateRefused.ToResult();
            case ConfirmationStep.Declined:
                return ToolResults.Of(new AccessChange("declined", ResetAction, Message: "Nothing was changed."));
            case ConfirmationStep.Confirmed:
                if (limit.TryCount(actor.UserId) is { } limited)
                {
                    return limited.ToResult();
                }

                var result = await setupLink.SendAsync(
                    actor,
                    target.Id,
                    IfMatch.FromExpectedVersion(answer.ExpectedVersion),
                    Trimmed(reason),
                    cancellationToken);

                // A reset and a resend both end with the account Invited and the link sent.
                return RefusalOf(result, LastManagerReset)?.ToResult() ?? ToolResults.Of(new AccessChange(
                    "done",
                    ResetAction,
                    UserOf(target, nameof(UserStatus.Invited).ToLowerInvariant()),
                    Changed: result.Changed));
            default:
                throw new UnreachableException($"A confirmation cannot be at {answer.Step}.");
        }
    }

    // The account of the tenant with this email, matched as list_users matches an exact email (A132); null when there
    // is none, whether none has it or another organisation's account has, which the tenant-named list cannot tell.
    private async Task<AdminUser?> FindAsync(string email, CancellationToken cancellationToken)
    {
        var wanted = email.Trim();
        var all = await users.ListAsync(ToolActor.Of(httpContextAccessor), cancellationToken);
        return all.FirstOrDefault(user => string.Equals(user.Email, wanted, StringComparison.OrdinalIgnoreCase));
    }

    // Support's limits (A137), applied to a call whose caller is Support: worker accounts only, and a reason that says
    // why and who asked.
    private static ToolError? SupportRefusal(string role, string? reason)
    {
        if (role != nameof(UserRole.Worker))
        {
            return ToolError.RoleNotAllowed;
        }

        return string.IsNullOrWhiteSpace(reason) ? ToolError.ReasonRequired : null;
    }

    // A blank reason is none.
    private static string? Trimmed(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    // What the handler refused with, in the tool's words, or null when it made the change. lastManager is the tool's
    // sentence for the rule, which only a tool whose handler can give it passes.
    private static ToolError? RefusalOf(UserChangeResult result, ToolError? lastManager = null)
    {
        if (result.IsNotFound)
        {
            return NoSuchUser;
        }

        if (result.Error is not { } error)
        {
            return null;
        }

        if (error == ErrorCode.ValidationFailed)
        {
            return ToolError.ValidationFailed(result.FieldErrors);
        }

        if (error == ErrorCode.PreconditionRequired)
        {
            return ToolError.PreconditionRequired;
        }

        if (error == ErrorCode.PreconditionFailed)
        {
            return AccountChanged;
        }

        if (error == ErrorCode.LastManager && lastManager is not null)
        {
            return lastManager;
        }

        if (error == ErrorCode.UserNotActive)
        {
            return NotActive;
        }

        if (error == ErrorCode.EmailInUse)
        {
            return EmailInUse;
        }

        throw new UnreachableException($"A user tool cannot be refused with {error.Code}.");
    }

    private static AccessChangeUser UserOf(AdminUser user, string status) =>
        new(user.Email, user.DisplayName, RoleOf(user), status);

    // The enum names that the Users screen's list gives, in lower case as every tool says a role (A132).
    private static string RoleOf(AdminUser user) => user.Role.ToLowerInvariant();

    private static string StatusOf(AdminUser user) => user.Status.ToLowerInvariant();
}
