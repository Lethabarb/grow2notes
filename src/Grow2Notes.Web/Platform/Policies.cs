using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Authorization;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Who may call an endpoint (design.md §2, §9.1). Requests are denied by default: an endpoint with no policy of its own
/// gets the fallback policy, which admits a signed-in user whose role is Worker or Manager, and a manager-only endpoint
/// carries the <see cref="Manager"/> policy. An endpoint that anyone may call, signed in or not, is marked
/// <c>AllowAnonymous</c>.
/// </summary>
/// <remarks>
/// Each policy names its roles, and a role claim must equal one of the names exactly, so a role that no policy names,
/// such as Release 2's Support, is refused everywhere (mcp-server.md §3.5, convention 4). Neither reads the user's
/// status: deactivation and a reset rotate the security stamp, which ends the user's sessions (design.md §8.4, §8.6).
/// </remarks>
internal static class Policies
{
    /// <summary>The policy of manager-only endpoints, for <c>RequireAuthorization</c>.</summary>
    public const string Manager = nameof(Manager);

    public static IServiceCollection AddPolicies(this IServiceCollection services)
    {
        // The default policy, for a bare RequireAuthorization() or [Authorize], is the fallback's too, so neither admits
        // a role no policy names.
        var workerOrManager = SignedInAs(UserRole.Worker, UserRole.Manager);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(workerOrManager)
            .SetDefaultPolicy(workerOrManager)
            .AddPolicy(Manager, SignedInAs(UserRole.Manager));

        return services;
    }

    // By each role's UserRole name, which design.md §8.4's claims factory puts in the role claim. RequireRole compares
    // the claim with each name ordinally, so neither "worker" nor "1", Worker's stored value, is a role here.
    private static AuthorizationPolicy SignedInAs(params UserRole[] roles) =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole(roles.Select(role => role.ToString()))
            .Build();
}
