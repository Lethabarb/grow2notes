using System.Security.Claims;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The app's policies, as it registers them, deciding for a user signed in with a role claim as design.md §8.4's claims
/// factory makes it, or with none, and for one signed out. The app has no database here, so they run without Docker;
/// <see cref="PoliciesOnSqlServerTests"/> calls an endpoint behind each policy as the seeded users.
/// </summary>
public sealed class PoliciesTests : IAsyncDisposable
{
    private readonly AppWithoutDatabase app = new("Production");

    [Fact]
    public async Task The_fallback_policy_admits_a_signed_in_worker_or_manager_and_nobody_else()
    {
        var policy = await app.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        Assert.NotNull(policy);

        Assert.True(await AdmitsAsync(policy, SignedIn("Worker")));
        Assert.True(await AdmitsAsync(policy, SignedIn("Manager")));
        Assert.False(await AdmitsAsync(policy, SignedIn(role: null)));
        Assert.False(await AdmitsAsync(policy, SignedOut("Worker")));
    }

    [Fact]
    public async Task The_Manager_policy_admits_a_signed_in_manager_only()
    {
        var policy = await app.Services.GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(Policies.Manager);
        Assert.NotNull(policy);

        Assert.True(await AdmitsAsync(policy, SignedIn("Manager")));
        Assert.False(await AdmitsAsync(policy, SignedIn("Worker")));
        Assert.False(await AdmitsAsync(policy, SignedOut("Manager")));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private async Task<bool> AdmitsAsync(AuthorizationPolicy policy, ClaimsPrincipal user) =>
        (await app.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy)).Succeeded;

    private ClaimsPrincipal SignedIn(string? role) => User(role, authenticationType: "Test");

    private ClaimsPrincipal SignedOut(string role) => User(role, authenticationType: null);

    // With the role in Identity's role claim type, as the claims factory builds the user; with an authentication type,
    // the user is signed in.
    private ClaimsPrincipal User(string? role, string? authenticationType)
    {
        var types = app.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity;
        Claim[] claims = role is null ? [] : [new(types.RoleClaimType, role)];
        return new(new ClaimsIdentity(claims, authenticationType, types.UserNameClaimType, types.RoleClaimType));
    }
}
