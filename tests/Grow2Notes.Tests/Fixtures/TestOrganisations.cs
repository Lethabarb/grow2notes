using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Fixtures;

internal static class TestOrganisations
{
    // Made up (A35).
    private static readonly DateTime AddedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Adds an organisation of its own to the shared database, through the app's context and <c>UserManager</c>, as
    /// <see cref="SqlServerFixture"/> adds the seeded ones: with its Every note group, an Active manager, an Active
    /// worker and an Active Support person, who cannot sign in but are called as with the test-only sign-in, and an
    /// active participant. It is for a test that must know every row of a list that the seeded organisations' lists
    /// share with other tests, such as the Reviewed list's pages or a count of notes in To review (tests/README.md).
    /// </summary>
    public static async Task<SeededOrganisation> AddOrganisationAsync(this IServiceProvider services)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var organisation = new Organisation
        {
            Name = $"Test organisation {Guid.NewGuid():N}",
            CreatedAtUtc = AddedAtUtc,
        };
        db.Organisations.Add(organisation);
        var everyNote = CommonItemGroup.EveryNote(organisation.Id, AddedAtUtc);
        var participant = new Participant
        {
            OrganisationId = organisation.Id,
            GivenName = "Jane",
            FamilyName = "Citizen",
            DateOfBirth = new DateOnly(1987, 3, 7),
            CreatedAtUtc = AddedAtUtc,
        };
        db.CommonItemGroups.Add(everyNote);
        db.Participants.Add(participant);
        using (tenant.Use(organisation.Id))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        var managerId = await AddUserAsync(users, organisation.Id, UserRole.Manager, invitedByUserId: null);
        var workerId = await AddUserAsync(users, organisation.Id, UserRole.Worker, invitedByUserId: managerId);
        var supportId = await AddUserAsync(users, organisation.Id, UserRole.Support, invitedByUserId: managerId);
        return new(organisation.Id, managerId, workerId, supportId, participant.Id, everyNote.Id);
    }

    private static async Task<Guid> AddUserAsync(
        UserManager<ApplicationUser> users, Guid organisationId, UserRole role, Guid? invitedByUserId)
    {
        var email = $"user.{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
        {
            OrganisationId = organisationId,
            DisplayName = role.ToString(),
            Role = role,
            Status = UserStatus.Active,
            InvitedAtUtc = AddedAtUtc,
            InvitedByUserId = invitedByUserId,
            ActivatedAtUtc = AddedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user.Id;
    }
}
