using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The app's <see cref="Actor"/>, from the principal that the claims factory builds from a seeded user's row, as the
/// session check rebuilds it on every request (design.md §8.4) and as <c>Grow2NotesClaimsFactoryTests</c> builds it.
/// <see cref="ActorTests"/> covers a principal without a user ID claim.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ActorOnSqlServerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public async Task A_s_manager_acts_as_themselves_through_the_app()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var claimsFactory = Assert.IsType<Grow2NotesClaimsFactory>(
            scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>());
        var managerId = sqlServer.Seeded.A.ManagerId;
        var manager = await users.FindByIdAsync(managerId.ToString());
        Assert.NotNull(manager);

        var principal = await claimsFactory.CreateAsync(manager);

        Assert.Equal(new Actor(managerId, Channel.App), Actor.InApp(principal, users.Options));
    }
}
