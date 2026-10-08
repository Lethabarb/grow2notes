using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// The shared database holds the two organisations of the two-organisation tests (design.md §5.9 item 7) as
/// <see cref="SqlServerFixture.Seeded"/> describes them, read through the app's own services.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SeededOrganisationsTests(SqlServerFixture sqlServer, Grow2NotesFactory factory)
    : IClassFixture<Grow2NotesFactory>
{
    [Fact]
    public void The_two_organisations_and_their_four_users_are_distinct()
    {
        var (a, b) = sqlServer.Seeded;

        Assert.NotEqual(a.OrganisationId, b.OrganisationId);
        Assert.Distinct([a.ManagerId, a.WorkerId, b.ManagerId, b.WorkerId]);
    }

    [Fact]
    public async Task Each_organisation_is_there_with_its_Active_manager_and_Active_worker()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        foreach (var seeded in new[] { sqlServer.Seeded.A, sqlServer.Seeded.B })
        {
            Assert.True(await db.Organisations.AnyAsync(o => o.Id == seeded.OrganisationId, cancellationToken));

            // AspNetUsers has no tenant filter, so the query names the organisation itself (design.md §5.9 item 5).
            // Other tests may add users to a seeded organisation, so it reads the seeded two by their IDs.
            var users = await db.Users
                .Where(u => u.OrganisationId == seeded.OrganisationId
                    && (u.Id == seeded.ManagerId || u.Id == seeded.WorkerId))
                .ToListAsync(cancellationToken);

            Assert.Equal(
                [
                    (seeded.ManagerId, UserRole.Manager, UserStatus.Active),
                    (seeded.WorkerId, UserRole.Worker, UserStatus.Active),
                ],
                // Manager (2) sorts before Worker (1).
                users.OrderByDescending(u => u.Role).Select(u => (u.Id, u.Role, u.Status)));
        }
    }

    [Fact]
    public async Task Identity_finds_each_seeded_user_by_a_confirmed_example_org_address_and_has_a_security_stamp()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var (a, b) = sqlServer.Seeded;

        foreach (var userId in new[] { a.ManagerId, a.WorkerId, b.ManagerId, b.WorkerId })
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            Assert.NotNull(user.Email);
            Assert.EndsWith("@example.org", user.Email, StringComparison.Ordinal);
            Assert.True(user.EmailConfirmed);

            // Identity looks a user up by the normalised email and user name, and the session check on every request
            // reads the security stamp (design.md §8.4).
            Assert.Equal(userId, (await userManager.FindByEmailAsync(user.Email))?.Id);
            Assert.Equal(userId, (await userManager.FindByNameAsync(user.Email))?.Id);
            Assert.False(string.IsNullOrEmpty(await userManager.GetSecurityStampAsync(user)));
        }
    }
}
