namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The body of <c>POST /api/admin/users</c> (design.md §6.6): the person's name, their email address and their role,
/// by its <see cref="Data.UserRole"/> name, as the Users screen sends them, trimmed (users.md).
/// </summary>
/// <remarks>
/// Each is a string that may be missing, with no data annotations: the handler checks them itself
/// (<see cref="UserInvite"/>), so a missing value, and a role that is no role's name, gets its field's
/// <c>422 validation.failed</c>, as Release 2's <c>invite_user</c> tool will through the same handler, never the
/// <c>400</c> of a body that cannot be read.
/// </remarks>
internal sealed record InviteUserRequest(string? DisplayName, string? Email, string? Role);
