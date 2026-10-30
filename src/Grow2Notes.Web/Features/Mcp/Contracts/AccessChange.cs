namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What a tool that changes a person's access returns (mcp-server.md §5.2, §5.3, A137): the preview that asks the
/// person to confirm, the outcome once they have, or the answer that they declined. One record with optional fields
/// serves all three, as the contract rules allow no union. It names the account by email and never by ID, and holds
/// no sign-in detail.
/// </summary>
/// <param name="Status">The outcome: <c>preview</c>, <c>done</c> or <c>declined</c>.</param>
/// <param name="Action">The tool that was called.</param>
/// <param name="User">
/// The account acted on; absent when the person declined. For <c>invite_user</c>'s preview it is the account that the
/// invite would make: it does not exist yet, so it has no <paramref name="ExpectedVersion"/>.
/// </param>
/// <param name="Effect">For a preview: what the change does.</param>
/// <param name="ExpectedVersion">For a preview: the account's version, which the confirming call sends back.</param>
/// <param name="ToConfirm">For a preview: what the assistant does next.</param>
/// <param name="Changed">For <c>done</c>: whether the account was changed, or was already so.</param>
/// <param name="Message">For <c>declined</c>: that nothing was changed.</param>
internal sealed record AccessChange(
    string Status,
    string Action,
    AccessChangeUser? User = null,
    string? Effect = null,
    string? ExpectedVersion = null,
    string? ToConfirm = null,
    bool? Changed = null,
    string? Message = null);

/// <summary>An account that <see cref="AccessChange"/> names.</summary>
/// <param name="Email">As stored.</param>
/// <param name="DisplayName">As typed.</param>
/// <param name="Role"><c>worker</c>, <c>manager</c> or <c>support</c>.</param>
/// <param name="Status">
/// <c>invited</c>, <c>active</c> or <c>deactivated</c>: the account's state; for an invite's preview, the state that the
/// account will have.
/// </param>
internal sealed record AccessChangeUser(string Email, string DisplayName, string Role, string Status);
