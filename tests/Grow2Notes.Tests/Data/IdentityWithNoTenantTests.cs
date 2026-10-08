using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// Identity reads and saves <c>AspNetUsers</c> with no tenant (design.md §5.9 item 5), as sign-in must before it has
/// loaded the user: the table is not filtered, and the tenant save interceptor reads no tenant for a save that holds
/// no tenant-owned row.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class IdentityWithNoTenantTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const string Email = "robin.hale@example.org";

    private static readonly DateTime TimeUtc = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UserManager_finds_creates_and_updates_a_user_on_the_app_s_context()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Outside a request and outside any Use block, so this scope has no tenant.
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        Assert.Throws<InvalidOperationException>(() => tenant.OrganisationId);

        // In a transaction that is never committed, so the user does not stay in the shared database. The retrying
        // execution strategy refuses a transaction begun outside it.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var organisation = new Organisation { Name = "Identity with no tenant test", CreatedAtUtc = TimeUtc };
            db.Organisations.Add(organisation);
            await db.SaveChangesAsync(cancellationToken);

            Assert.Null(await userManager.FindByEmailAsync(Email));

            var user = new ApplicationUser
            {
                OrganisationId = organisation.Id,
                DisplayName = "Robin Hale",
                Role = UserRole.Worker,
                Status = UserStatus.Invited,
                InvitedAtUtc = TimeUtc,
                UserName = Email,
                Email = Email,
            };
            AssertSucceeded(await userManager.CreateAsync(user));

            user.Status = UserStatus.Active;
            user.ActivatedAtUtc = TimeUtc;
            AssertSucceeded(await userManager.UpdateAsync(user));

            // Read back from the database, not the tracked user.
            db.ChangeTracker.Clear();
            var found = await userManager.FindByEmailAsync(Email);
            Assert.NotNull(found);
            Assert.Equal(user.Id, found.Id);
            Assert.Equal(UserStatus.Active, found.Status);
        });
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
}
