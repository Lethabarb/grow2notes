using System.Security.Claims;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <c>GET /api/auth/me</c> (design.md §6.2): the signed-in user, as a <see cref="Me"/>. The SPA calls it on page load,
/// on window focus and when the page becomes visible again (§6.8).
/// </summary>
/// <remarks>
/// It has no policy of its own, so the fallback policy admits any signed-in Worker or Manager, and a request with no
/// session gets the session cookie's <c>401</c>. The user's ID, display name and role come from the principal, which
/// the session check rebuilds from the user's row on every request (§8.4), so a change to the row shows on the user's
/// next call. The organisation's name is read for the request's tenant (<see cref="ITenantContext"/>), which is the
/// organisation in the principal's <c>org_id</c>.
/// </remarks>
internal static class MeEndpoint
{
    /// <summary>Maps the endpoint in the <c>/api</c> group, which <see cref="ApiGroup.MapApiGroup"/> returns.</summary>
    public static RouteGroupBuilder MapMeEndpoint(this RouteGroupBuilder api)
    {
        api.MapGet("/auth/me", GetAsync);
        return api;
    }

    private static async Task<Ok<Me>> GetAsync(
        ClaimsPrincipal user,
        IOptions<IdentityOptions> identityOptions,
        ITenantContext tenant,
        Grow2NotesDbContext db,
        MelbourneClock clock,
        CancellationToken cancellationToken)
    {
        // An organisation is the tenant, not tenant-owned, so no query filter keeps this to the tenant's row: the query
        // names it.
        var organisationId = tenant.OrganisationId;
        var organisationName = await db.Organisations
            .Where(organisation => organisation.Id == organisationId)
            .Select(organisation => organisation.Name)
            .SingleAsync(cancellationToken);

        var claimTypes = identityOptions.Value.ClaimsIdentity;
        return TypedResults.Ok(new Me(
            Guid.Parse(ClaimValue(user, claimTypes.UserIdClaimType)),
            ClaimValue(user, Grow2NotesClaimsFactory.DisplayNameClaimType),
            ClaimValue(user, claimTypes.RoleClaimType),
            organisationName,
            clock.Today()));
    }

    // The claims factory adds each of these claims to every principal it builds, and the fallback policy admits no user
    // without a role.
    private static string ClaimValue(ClaimsPrincipal user, string claimType) =>
        user.FindFirstValue(claimType)
            ?? throw new InvalidOperationException($"The signed-in user has no {claimType} claim.");
}
