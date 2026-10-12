using System.Reflection;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Auth;

/// <summary>
/// Whom the app's sign-in manager lets sign in (design.md §8.1, §8.2): Active users only, and none who is locked out,
/// whichever sign-in brings them. Each test adds its own user to seeded organisation A, as tests/README.md's
/// seeded-rows rule asks.
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

    [Fact]
    public Task The_check_before_signing_in_refuses_an_Invited_user() =>
        AssertTheCheckRefusesTheStatusAsync(UserStatus.Invited);

    [Fact]
    public Task The_check_before_signing_in_refuses_a_Deactivated_user() =>
        AssertTheCheckRefusesTheStatusAsync(UserStatus.Deactivated);

    // Locked out as A25's lockout locks someone out. Identity's UserManager compares the lockout's end with
    // DateTimeOffset.UtcNow, not the app's TimeProvider, so the end is set by the system clock.
    [Fact]
    public async Task The_check_before_signing_in_refuses_a_locked_out_user()
    {
        var userId = await AddUserAsync(UserStatus.Active);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(userId.ToString());
            Assert.NotNull(user);
            var lockedOut = await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(15));
            Assert.True(lockedOut.Succeeded, string.Join(" ", lockedOut.Errors.Select(error => error.Code)));
        }

        var result = await SignInOrTwoFactorAsync(userId);

        Assert.True(result.IsLockedOut);
    }

    // Asks the app's sign-in manager about the user as loaded afresh, as a sign-in loads the user it signs in. The
    // users differ in status alone, so nothing else can refuse them: an Invited user with a confirmed email and an
    // activation time is one whose sign-in a manager has reset (§8.6).
    private async Task<bool> CanSignInAsync(UserStatus status)
    {
        var userId = await AddUserAsync(status);

        await using var scope = factory.Services.CreateAsyncScope();
        var signInManager = Assert.IsType<Grow2NotesSignInManager>(
            scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>());
        var loaded = await signInManager.UserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(loaded);
        Assert.Equal(status, loaded.Status);
        return await signInManager.CanSignInAsync(loaded);
    }

    // The user differs from an Active one in status alone and is not locked out, so the status is all that can refuse
    // them.
    private async Task AssertTheCheckRefusesTheStatusAsync(UserStatus status)
    {
        var userId = await AddUserAsync(status);

        var result = await SignInOrTwoFactorAsync(userId);

        Assert.True(result.IsNotAllowed);
    }

    // Calls SignInOrTwoFactorAsync, where Identity's sign-ins decide to sign someone in, for the user as loaded afresh.
    // Identity's password sign-in checks the user before it calls it, and its passkey sign-in does too from ASP.NET
    // Core 10.0.4, which CI runs, so there no sign-in reaches it unchecked, and only a direct call shows that the app's
    // own check in it, for 10.0.0 to 10.0.3, still works (S00.04.04's Notes). It is protected, so the call is by
    // reflection, which runs the app's override. The scope has no request, so a sign-in that went on would throw for
    // want of one.
    private async Task<SignInResult> SignInOrTwoFactorAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var signInManager = Assert.IsType<Grow2NotesSignInManager>(
            scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>());
        var loaded = await signInManager.UserManager.FindByIdAsync(userId.ToString());
        Assert.NotNull(loaded);
        var method = typeof(SignInManager<ApplicationUser>).GetMethod(
            "SignInOrTwoFactorAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object?[] arguments = [loaded, false, null, false];
        return await (Task<SignInResult>)method.Invoke(signInManager, arguments)!;
    }

    // A user as setup completion leaves one (design.md §8.1), except for the status, with an address of its own, which
    // no other test or run of this one uses.
    private async Task<Guid> AddUserAsync(UserStatus status)
    {
        var organisation = sqlServer.Seeded.A;
        var email = $"{Guid.NewGuid():N}@example.org";
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
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(error => error.Code)));
        return user.Id;
    }
}
