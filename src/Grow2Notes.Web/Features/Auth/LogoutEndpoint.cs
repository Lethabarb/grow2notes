using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/logout</c> (design.md §6.2, §8.5): ends this browser's session, and answers <c>204</c> with
/// <c>Clear-Site-Data</c>, so the browser also drops what it kept of the signed-in pages (§9.6).
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>, before the <c>/api</c> group's antiforgery check, with no event. The
/// <c>auth.signed_out</c> event is written first, in the request's tenant, the session's organisation (§5.9 item 1),
/// with no transaction, as §5.9 lists none for a sign-in event: if writing it fails, the exception handler's
/// <c>500</c> comes before the sign-out, so the person stays signed in and no sign-out goes unrecorded. The sign-in
/// manager's sign-out deletes the session cookie and Identity's other sign-in cookies, and leaves the security stamp
/// alone: rotating it would end the person's sessions on their other devices too, which deactivation, a sign-in reset
/// and <c>admin signout-all</c> do (§7.4, §8.6), not Sign out.
/// </remarks>
internal static class LogoutEndpoint
{
    // Exactly as design.md §6.2 and §8.5 write it.
    private const string ClearSiteData = "\"cache\", \"cookies\", \"storage\"";

    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapLogoutEndpoint(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/logout", SignOutAsync);
        return api;
    }

    private static async Task<NoContent> SignOutAsync(
        ClaimsPrincipal user,
        HttpResponse response,
        SignInManager<ApplicationUser> signInManager,
        ITenantContext tenant,
        IAuditWriter audit,
        CancellationToken cancellationToken)
    {
        // The claims factory adds the user's ID to every principal it builds.
        var userId = Guid.Parse(signInManager.UserManager.GetUserId(user)
            ?? throw new InvalidOperationException("The signed-in user has no user ID claim."));

        await audit.WriteAsync(
            tenant.OrganisationId,
            actorUserId: userId,
            AuditEventTypes.Auth.SignedOut,
            AuditEntityTypes.User,
            entityId: userId,
            participantId: null,
            details: null,
            cancellationToken);
        await signInManager.SignOutAsync();

        response.Headers["Clear-Site-Data"] = ClearSiteData;
        return TypedResults.NoContent();
    }
}
