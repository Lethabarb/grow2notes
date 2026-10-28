using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

internal static class TestUsers
{
    // Made up (A35).
    private static readonly DateTime AddedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Adds a user to <paramref name="organisation"/>, invited by its seeded manager, through the app's
    /// <c>UserManager</c>, as setup leaves one but with no way to sign in, so that a test calls as them with the
    /// test-only sign-in (tests/README.md); and gives their ID. By default the user is Active, with an address of their
    /// own on every call; an Invited user has not activated, and a Deactivated one has. A test that looks a user up by
    /// address gives it as <paramref name="email"/>.
    /// </summary>
    public static async Task<Guid> AddUserAsync(
        this IServiceProvider services,
        SeededOrganisation organisation,
        UserRole role,
        string displayName,
        UserStatus status = UserStatus.Active,
        string? email = null)
    {
        email ??= $"user.{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
        {
            OrganisationId = organisation.OrganisationId,
            DisplayName = displayName,
            Role = role,
            Status = status,
            InvitedAtUtc = AddedAtUtc,
            InvitedByUserId = organisation.ManagerId,
            ActivatedAtUtc = status == UserStatus.Invited ? null : AddedAtUtc,
            DeactivatedAtUtc = status == UserStatus.Deactivated ? AddedAtUtc : null,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        await using var scope = services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user.Id;
    }
}
