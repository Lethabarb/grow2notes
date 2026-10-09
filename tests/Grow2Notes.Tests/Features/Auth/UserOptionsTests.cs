using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// Identity's user options as Program.cs sets them (design.md §8.4), through the app's
/// <see cref="UserManager{TUser}"/>, which creates accounts with the address as both the user name and the email
/// (§5.3): one account per address across the whole app (A27), refused by the app before the database's unique index
/// is reached (<see cref="Data.UniqueEmailTests"/> shows the index), and any valid address accepted as a user name.
/// Each test adds its own users to seeded organisation A, as tests/README.md's seeded-rows rule asks.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UserOptionsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime TimeUtc = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_second_account_with_an_address_another_account_holds_is_refused_with_DuplicateEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // An address of its own, which no other test or run of this one uses.
        var email = $"sam.taylor.{Guid.NewGuid():N}@example.org";
        var firstUserId = await AddUserAsync(sqlServer.Seeded.A, email);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        // In another letter case and in another organisation, because the address is unique across the whole app.
        var result = await users.CreateAsync(NewUser(sqlServer.Seeded.B, email.ToUpperInvariant()));

        // The user name holds the address too, so Identity refuses it as a duplicate user name as well.
        Assert.Equal(
            [nameof(IdentityErrorDescriber.DuplicateUserName), nameof(IdentityErrorDescriber.DuplicateEmail)],
            result.Errors.Select(error => error.Code));
        var normalizedEmail = users.NormalizeEmail(email);
        var holders = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        Assert.Equal([firstUserId], holders);
    }

    // Identity's default user-name characters have no apostrophe, which an address may hold.
    [Fact]
    public async Task An_address_such_as_o_brien_at_example_org_is_accepted_as_the_user_name()
    {
        var email = $"o'brien.{Guid.NewGuid():N}@example.org";
        var userId = await AddUserAsync(sqlServer.Seeded.A, email);

        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByNameAsync(email);
        Assert.NotNull(user);
        Assert.Equal(userId, user.Id);
        Assert.Equal(email, user.UserName);
        Assert.Equal(email, user.Email);
    }

    private async Task<Guid> AddUserAsync(SeededOrganisation organisation, string email)
    {
        var user = NewUser(organisation, email);
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
        return user.Id;
    }

    // As the invite core creates one (design.md §8.1 step 1): Invited, with no password and an unconfirmed address.
    private static ApplicationUser NewUser(SeededOrganisation organisation, string email) => new()
    {
        OrganisationId = organisation.OrganisationId,
        DisplayName = "Sam Taylor",
        Role = UserRole.Worker,
        Status = UserStatus.Invited,
        InvitedAtUtc = TimeUtc,
        InvitedByUserId = organisation.ManagerId,
        UserName = email,
        Email = email,
    };
}
