namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// The body of <c>POST /api/admin/users</c> and <c>PUT /api/admin/users/{id}</c> (design.md §6.6): the person's name,
/// their email address and their role, by its <see cref="Data.UserRole"/> name, as the Users screen sends them, trimmed
/// (users.md).
/// </summary>
/// <remarks>
/// Each is a string that may be missing, with no data annotations: the handlers check them themselves
/// (<see cref="UserDetailsChecks"/>), so a missing value, and a role that is no role's name, gets its field's
/// <c>422 validation.failed</c>, as the <c>invite_user</c> tool does through the same handler, never the
/// <c>400</c> of a body that cannot be read.
/// </remarks>
internal sealed record UserDetailsRequest(string? DisplayName, string? Email, string? Role);
