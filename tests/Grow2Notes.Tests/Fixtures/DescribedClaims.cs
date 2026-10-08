using System.Security.Claims;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Claims written as <c>type: value</c>, as a test-only endpoint lists the claims of its request's user, so that a test
/// compares them with the claims of the principal that the app's claims factory builds.
/// </summary>
internal static class DescribedClaims
{
    public static string Describe(Claim claim) => $"{claim.Type}: {claim.Value}";

    /// <summary>
    /// The claims, in their order, of the principal that <paramref name="app"/>'s claims factory builds from the row of
    /// the user whose ID is <paramref name="userId"/>, as the session check rebuilds it on every request
    /// (design.md §8.4).
    /// </summary>
    public static async Task<List<string>> FromTheClaimsFactoryAsync(
        this WebApplicationFactory<Program> app, Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByIdAsync(userId.ToString());
        Assert.NotNull(user);

        var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>()
            .CreateAsync(user);
        return [.. principal.Claims.Select(Describe)];
    }
}
