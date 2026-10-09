using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// The setup token in an invite's link (design.md §8.1, §8.4), made and checked through the app's
/// <see cref="UserManager{TUser}"/> with <see cref="SetupTokenProvider"/>'s name and purpose, as the invite core and
/// setup do: it works for its own user only, and only until the user's security stamp changes. Each step runs in a
/// scope of its own, as each request has one, on the user as loaded afresh. Each test adds its own Invited users to
/// seeded organisation A, as tests/README.md's seeded-rows rule asks.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class SetupTokenProviderTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime TimeUtc = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_setup_token_verifies_for_its_own_user()
    {
        var userId = await AddInvitedUserToAAsync();
        var token = await MakeSetupTokenAsync(userId);

        Assert.True(await VerifySetupTokenAsync(userId, token));
    }

    [Fact]
    public async Task A_setup_token_fails_for_another_user()
    {
        var userId = await AddInvitedUserToAAsync();
        var otherUserId = await AddInvitedUserToAAsync();
        var token = await MakeSetupTokenAsync(userId);

        Assert.False(await VerifySetupTokenAsync(otherUserId, token));
        Assert.True(await VerifySetupTokenAsync(userId, token));
    }

    [Fact]
    public async Task A_setup_token_fails_once_the_user_s_security_stamp_has_changed()
    {
        var userId = await AddInvitedUserToAAsync();
        var token = await MakeSetupTokenAsync(userId);
        Assert.True(await VerifySetupTokenAsync(userId, token));

        // As setup completion, a resend and a sign-in reset change it (design.md §8.1, §8.6).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            AssertSucceeded(await users.UpdateSecurityStampAsync(await FindUserAsync(users, userId)));
        }

        Assert.False(await VerifySetupTokenAsync(userId, token));
    }

    // Identity's DataProtectorTokenProvider reads DateTimeOffset.UtcNow, not TimeProvider, when it makes a token and
    // when it checks one, so no fake clock can age a token (S00.03.05's Notes), and it shows its lifespan only to
    // subclasses. So this checks the lifespan in the options that UserManager's setup token provider is built with;
    // the expiry itself is Identity's.
    [Fact]
    public void The_provider_that_UserManager_uses_for_setup_tokens_is_built_with_a_7_day_lifespan()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        // As UserManager builds each provider it uses, from the type its name maps to.
        var descriptor = services.GetRequiredService<IOptions<IdentityOptions>>().Value.Tokens
            .ProviderMap[SetupTokenProvider.ProviderName];
        Assert.Null(descriptor.ProviderInstance);
        var provider = Assert.IsType<SetupTokenProvider>(services.GetRequiredService(descriptor.ProviderType));

        // The provider's name is the one in its options, and only SetupTokenProviderOptions give it this one, so it was
        // built with them rather than with the options of Identity's other Data Protection tokens.
        Assert.Equal(SetupTokenProvider.ProviderName, provider.Name);
        var options = services.GetRequiredService<IOptions<SetupTokenProviderOptions>>().Value;
        Assert.Equal(TimeSpan.FromDays(7), options.TokenLifespan);
    }

    // As the invite core creates one (design.md §8.1 step 1): Invited, with no password, and an unconfirmed address of
    // its own, which no other test or run of this one uses.
    private async Task<Guid> AddInvitedUserToAAsync()
    {
        var organisation = sqlServer.Seeded.A;
        var email = $"{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
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
        await using var scope = factory.Services.CreateAsyncScope();
        AssertSucceeded(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(user));
        return user.Id;
    }

    private async Task<string> MakeSetupTokenAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.GenerateUserTokenAsync(
            await FindUserAsync(users, userId), SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose);
    }

    private async Task<bool> VerifySetupTokenAsync(Guid userId, string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await users.VerifyUserTokenAsync(
            await FindUserAsync(users, userId), SetupTokenProvider.ProviderName, SetupTokenProvider.Purpose, token);
    }

    private static async Task<ApplicationUser> FindUserAsync(UserManager<ApplicationUser> users, Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        return user;
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
}
