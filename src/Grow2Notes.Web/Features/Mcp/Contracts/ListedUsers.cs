namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// What <c>list_users</c> returns (mcp-server.md §5.2): the accounts of the organisation that match, with no ID, no
/// sign-in detail and no time.
/// </summary>
/// <param name="Users">Sorted by display name, then email, ignoring case; empty when nobody matches.</param>
internal sealed record ListedUsers(IReadOnlyList<ListedUser> Users);
