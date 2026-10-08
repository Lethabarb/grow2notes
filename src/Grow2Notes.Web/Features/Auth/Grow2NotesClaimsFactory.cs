using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// Builds a user's principal from their row (design.md §8.4): Identity's own claims, which are the user ID, the user
/// name, the email and the security stamp, and then the user's organisation ID in
/// <see cref="TenantContext.OrganisationIdClaimType"/>, their role by its <see cref="UserRole"/> name in Identity's
/// role claim type, and their display name in <see cref="DisplayNameClaimType"/>.
/// </summary>
/// <remarks>
/// The session check rebuilds the principal with this factory on every request, so these claims always follow the
/// row: a role change applies on the user's next request. A claim added only at sign-in would be lost on that next
/// request, so <see cref="Grow2NotesSignInManager.AuthTimeClaimType"/> is carried forward instead.
/// </remarks>
internal sealed class Grow2NotesClaimsFactory(
    UserManager<ApplicationUser> userManager, IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
{
    /// <summary>The claim that holds the user's display name, as the app shows it.</summary>
    public const string DisplayNameClaimType = "display_name";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new(TenantContext.OrganisationIdClaimType, user.OrganisationId.ToString()));

        // The name, which the policies compare exactly, never the stored value (Policies).
        identity.AddClaim(new(Options.ClaimsIdentity.RoleClaimType, user.Role.ToString()));
        identity.AddClaim(new(DisplayNameClaimType, user.DisplayName));
        return identity;
    }
}
