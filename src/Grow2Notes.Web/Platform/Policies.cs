using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Authorization;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// Who may call an endpoint (design.md §2, §9.1). Requests are denied by default: an endpoint with no policy of its own
/// gets the fallback policy, which admits a signed-in user whose role is Worker or Manager, a manager-only endpoint
/// carries the <see cref="Manager"/> policy, and an endpoint that every signed-in person may call carries the
/// <see cref="SignedIn"/> policy. An endpoint that anyone may call, signed in or not, is marked <c>AllowAnonymous</c>.
/// </summary>
/// <remarks>
/// Each policy names its roles, and a role claim must equal one of the names exactly, so a role that a policy
/// does not name is refused there (mcp-server.md §3.5, convention 4). <see cref="SignedIn"/> is the only policy that
/// names Release 2's Support, so Support is refused everywhere else. None reads the user's status: deactivation and a
/// reset rotate the security stamp, which ends the user's sessions (design.md §8.4, §8.6).
/// </remarks>
internal static class Policies
{
    /// <summary>The policy of manager-only endpoints, for <c>RequireAuthorization</c>.</summary>
    public const string Manager = nameof(Manager);

    /// <summary>
    /// The policy of endpoints that any signed-in Worker, Manager or Support person may call, for
    /// <c>RequireAuthorization</c>: who the person is, and signing out.
    /// </summary>
    public const string SignedIn = nameof(SignedIn);

    public static IServiceCollection AddPolicies(this IServiceCollection services)
    {
        // The default policy, for a bare RequireAuthorization() or [Authorize], is the fallback's too, so neither admits
        // Support.
        var workerOrManager = SignedInAs(UserRole.Worker, UserRole.Manager);
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(workerOrManager)
            .SetDefaultPolicy(workerOrManager)
            .AddPolicy(Manager, SignedInAs(UserRole.Manager))
            .AddPolicy(SignedIn, SignedInAs(UserRole.Worker, UserRole.Manager, UserRole.Support));

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
