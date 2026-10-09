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
/// is reached (<see cref="Data.UniqueEmailTests"/> shows the index), even for a copy of an address with a space or an
/// invisible character in it; and an address with any of RFC 5322's characters accepted as a user name. Each test adds
/// its own users to seeded organisation A, as tests/README.md's seeded-rows rule asks.
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

    // Identity compares addresses as they are written, so a copy of sam@example.org with a space or an invisible
    // character in it can be another address to it and to the unique index, and so a second account for the same
    // mailbox; each is refused for its characters. They are, in order: a leading space, a leading no-break space, a
    // space before the @, a zero-width space before the @, which a paste can carry unseen, a trailing space and a
    // trailing tab.
    [Theory]
    [InlineData(" sam@example.org")]
    [InlineData("\u00A0sam@example.org")]
    [InlineData("sam @example.org")]
    [InlineData("sam\u200B@example.org")]
    [InlineData("sam@example.org ")]
    [InlineData("sam@example.org\t")]
    public async Task A_copy_of_an_address_another_account_holds_with_a_space_or_an_invisible_character_is_refused(
        string copy)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // An address of its own, which no other test or run of this one uses.
        var localPart = $"sam.{Guid.NewGuid():N}";
        var firstUserId = await AddUserAsync(sqlServer.Seeded.A, $"{localPart}@example.org");

        await using var scope = factory.Services.CreateAsyncScope();
        // In another organisation, because the address is unique across the whole app.
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(NewUser(sqlServer.Seeded.B, copy.Replace("sam", localPart, StringComparison.Ordinal)));

        Assert.Contains(nameof(IdentityErrorDescriber.InvalidUserName), result.Errors.Select(error => error.Code));
        // Every account whose address holds this test's own part, with anything around it: only the first.
        var holders = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Users
            .Where(user => user.Email!.Contains(localPart))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        Assert.Equal([firstUserId], holders);
    }

    // Identity's default user-name characters have only + - _ of RFC 5322's characters for an address, which may hold
    // any of them, such as an apostrophe.
    [Theory]
    [InlineData("o'brien")]
    [InlineData("!#$%&'*+-/=?^_`{|}~")]
    public async Task An_address_with_any_of_RFC_5322_s_characters_such_as_o_brien_is_accepted_as_the_user_name(
        string localPart)
    {
        var email = $"{localPart}.{Guid.NewGuid():N}@example.org";
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
