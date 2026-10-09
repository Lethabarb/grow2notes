using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// Whom the app's sign-in manager lets sign in (design.md §8.1, §8.2): Active users only. Each test adds its own user
/// to seeded organisation A, as tests/README.md's seeded-rows rule asks.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class Grow2NotesSignInManagerTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime TimeUtc = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task An_Active_user_can_sign_in() => Assert.True(await CanSignInAsync(UserStatus.Active));

    [Fact]
    public async Task An_Invited_user_cannot_sign_in() => Assert.False(await CanSignInAsync(UserStatus.Invited));

    [Fact]
    public async Task A_Deactivated_user_cannot_sign_in() =>
        Assert.False(await CanSignInAsync(UserStatus.Deactivated));

    // Adds a user to A as setup completion leaves one (design.md §8.1), except for the status, then asks the app's
    // sign-in manager about the user as loaded afresh, as a sign-in loads the user it signs in. The users differ in
    // status alone, so nothing else can refuse them: an Invited user with a confirmed email and an activation time is
    // one whose sign-in a manager has reset (§8.6).
    private async Task<bool> CanSignInAsync(UserStatus status)
    {
        var organisation = sqlServer.Seeded.A;
        // An address of its own, which no other test or run of this one uses.
        var email = $"{Guid.NewGuid():N}@example.org";
        Guid userId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var user = new ApplicationUser
            {
                OrganisationId = organisation.OrganisationId,
                DisplayName = "Sam Taylor",
                Role = UserRole.Worker,
                Status = status,
                InvitedAtUtc = TimeUtc,
                InvitedByUserId = organisation.ManagerId,
                ActivatedAtUtc = TimeUtc,
                UserName = email,
                Email = email,
                EmailConfirmed = true,
            };
            var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                .CreateAsync(user);
            Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
            userId = user.Id;
        }

        await using var signInScope = factory.Services.CreateAsyncScope();
        var signInManager = Assert.IsType<Grow2NotesSignInManager>(
            signInScope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>());
        var loaded = await signInManager.UserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(loaded);
        Assert.Equal(status, loaded.Status);
        return await signInManager.CanSignInAsync(loaded);
    }
}
