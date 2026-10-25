using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>POST /api/auth/logout</c> (design.md §6.2, §8.5, A99): ends every session of the person, on every device, a copy
/// of the session cookie included, and answers <c>204</c> with <c>Clear-Site-Data</c>, so the browser also drops what it
/// kept of the signed-in pages (§9.6).
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>, before the <c>/api</c> group's antiforgery check, with no event. In one
/// transaction (§5.9) it gives the person a new security stamp, which the session check finds changed on each session's
/// next request (ops/security-review.md F-1), and writes <c>auth.signed_out</c> in the session's organisation (§5.9
/// item 1). If either fails, the exception handler's <c>500</c> comes before the sign-out, with nothing changed, so no
/// sign-out goes unrecorded. Only then does the sign-in manager delete the session cookie and Identity's other sign-in
/// cookies.
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
        Grow2NotesDbContext db,
        ITenantContext tenant,
        IAuditWriter audit,
        CancellationToken cancellationToken)
    {
        var users = signInManager.UserManager;

        // The claims factory adds the user's ID to every principal it builds.
        var userId = Guid.Parse(users.GetUserId(user)
            ?? throw new InvalidOperationException("The signed-in user has no user ID claim."));
        var organisationId = tenant.OrganisationId;

        await db.InTransactionAsync(async ct =>
        {
            // AspNetUsers has no tenant query filter, so the query names the organisation (§5.9 item 5). Each attempt
            // loads the person again, as InTransactionAsync clears the change tracker first.
            var person = await db.Users.SingleAsync(row => row.Id == userId && row.OrganisationId == organisationId, ct);
            var rotated = await users.UpdateSecurityStampAsync(person);
            if (!rotated.Succeeded)
            {
                throw new InvalidOperationException(
                    "Identity refused the user's new security stamp: "
                    + $"{string.Join(" ", rotated.Errors.Select(error => error.Code))}.");
            }

            await audit.WriteAsync(
                organisationId,
                actorUserId: userId,
                AuditEventTypes.Auth.SignedOut,
                AuditEntityTypes.User,
                entityId: userId,
                participantId: null,
                details: null,
                ct);
        }, cancellationToken);

        // Only after the commit: a failure above leaves the person signed in everywhere.
        await signInManager.SignOutAsync();

        response.Headers["Clear-Site-Data"] = ClearSiteData;
        return TypedResults.NoContent();
    }
}
