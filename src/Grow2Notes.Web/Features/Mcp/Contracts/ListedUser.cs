namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// A user account that <c>list_users</c> returned. Users are addressed by their email (mcp-server.md §5.1).
/// </summary>
/// <param name="Email">As stored.</param>
/// <param name="DisplayName">As typed.</param>
/// <param name="Role"><c>worker</c>, <c>manager</c> or <c>support</c>.</param>
/// <param name="Status"><c>invited</c> or <c>active</c>, or <c>deactivated</c> for an exact email search only.</param>
/// <param name="ExpectedVersion">The account's version, which a later change needs.</param>
internal sealed record ListedUser(string Email, string DisplayName, string Role, string Status, string ExpectedVersion);
