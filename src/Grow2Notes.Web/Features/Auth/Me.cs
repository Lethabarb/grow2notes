using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The signed-in user, as the SPA starts its session with it (design.md §6.2): <c>GET /api/auth/me</c> answers with it,
/// and so do setup and the sign-in endpoints once they have signed someone in. <see cref="Role"/> is the role's
/// <see cref="UserRole"/> name, <c>Worker</c> or <c>Manager</c>, which the SPA compares exactly, and
/// <see cref="Today"/> is the date in Melbourne, which drives every "today" in the SPA. E03 adds the managers'
/// <c>toReviewCount</c>.
/// </summary>
internal sealed record Me(Guid UserId, string DisplayName, string Role, string OrganisationName, DateOnly Today);
