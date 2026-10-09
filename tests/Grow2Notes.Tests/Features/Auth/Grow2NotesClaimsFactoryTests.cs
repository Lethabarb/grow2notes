using System.Security.Claims;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// The principal that the app's claims factory builds from a seeded user's row, as the session check rebuilds it on
/// every request (design.md §8.4).
/// </summary>
[Collection<SqlServerCollection>]
public sealed class Grow2NotesClaimsFactoryTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task Each_seeded_user_s_principal_holds_their_ID_organisation_role_name_and_display_name()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var claimsFactory = Assert.IsType<Grow2NotesClaimsFactory>(
            scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>());
        var types = users.Options.ClaimsIdentity;
        var (a, b) = sqlServer.Seeded;

        foreach (var (userId, organisationId, role) in new[]
        {
            (a.WorkerId, a.OrganisationId, "Worker"),
            (a.ManagerId, a.OrganisationId, "Manager"),
            (b.WorkerId, b.OrganisationId, "Worker"),
            (b.ManagerId, b.OrganisationId, "Manager"),
        })
        {
            var user = await users.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);

            var principal = await claimsFactory.CreateAsync(user);

            // Each exactly once.
            Assert.Equal([userId.ToString()], Values(principal, types.UserIdClaimType));
            Assert.Equal([organisationId.ToString()], Values(principal, TenantContext.OrganisationIdClaimType));
            Assert.Equal([role], Values(principal, types.RoleClaimType));
            Assert.Equal([user.DisplayName], Values(principal, Grow2NotesClaimsFactory.DisplayNameClaimType));

            // The policies ask for the role through IsInRole, and Identity's name claim keeps the user name.
            Assert.True(principal.IsInRole(role));
            Assert.Equal(user.UserName, principal.Identity?.Name);
        }
    }

    private static IEnumerable<string> Values(ClaimsPrincipal principal, string type) =>
        principal.FindAll(type).Select(claim => claim.Value);
}
