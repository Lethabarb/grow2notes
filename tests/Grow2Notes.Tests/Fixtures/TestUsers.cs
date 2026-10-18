using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

internal static class TestUsers
{
    // Made up (A35).
    private static readonly DateTime AddedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Adds an Active user to <paramref name="organisation"/>, invited by its seeded manager, through the app's
    /// <c>UserManager</c>, as setup leaves one but with no way to sign in, so that a test calls as them with the
    /// test-only sign-in (tests/README.md); and gives their ID. Each call adds one more, with an address of its own.
    /// </summary>
    public static async Task<Guid> AddUserAsync(
        this IServiceProvider services, SeededOrganisation organisation, UserRole role, string displayName)
    {
        var email = $"user.{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
        {
            OrganisationId = organisation.OrganisationId,
            DisplayName = displayName,
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = AddedAtUtc,
            InvitedByUserId = organisation.ManagerId,
            ActivatedAtUtc = AddedAtUtc,
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
