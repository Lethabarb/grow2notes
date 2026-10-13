using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Users;

/// <summary>
/// A user as <c>GET /api/admin/users</c> lists them (design.md §6.6), for the Users screen. <see cref="Role"/> and
/// <see cref="Status"/> are their enums' names (<see cref="UserRole"/>, <see cref="UserStatus"/>), as
/// <c>/api/auth/me</c> gives the role; the times are UTC, so they are written with <c>Z</c> (§6.1); and
/// <see cref="ConcurrencyStamp"/> is the row's Identity <c>ConcurrencyStamp</c>, which the SPA sends, in quotes, as
/// <c>If-Match</c> when it changes the user (D78).
/// </summary>
internal sealed record AdminUser(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    DateTime InvitedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? DeactivatedAtUtc,
    string ConcurrencyStamp);
