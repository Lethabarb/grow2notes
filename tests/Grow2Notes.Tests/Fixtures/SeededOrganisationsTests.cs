using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
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
    public void The_two_organisations_their_six_users_two_participants_and_two_Every_note_groups_are_distinct()
    {
        var (a, b) = sqlServer.Seeded;

        Assert.NotEqual(a.OrganisationId, b.OrganisationId);
        Assert.Distinct([a.ManagerId, a.WorkerId, a.SupportId, b.ManagerId, b.WorkerId, b.SupportId]);
        Assert.NotEqual(a.ParticipantId, b.ParticipantId);
        Assert.NotEqual(a.EveryNoteGroupId, b.EveryNoteGroupId);
    }

    [Fact]
    public async Task Each_organisation_is_there_with_its_Active_manager_Active_worker_and_Active_Support_person()
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
                    && (u.Id == seeded.ManagerId || u.Id == seeded.WorkerId || u.Id == seeded.SupportId))
                .ToListAsync(cancellationToken);

            Assert.Equal(
                [
                    (seeded.SupportId, UserRole.Support, UserStatus.Active),
                    (seeded.ManagerId, UserRole.Manager, UserStatus.Active),
                    (seeded.WorkerId, UserRole.Worker, UserStatus.Active),
                ],
                // Support (3) sorts before Manager (2), which sorts before Worker (1).
                users.OrderByDescending(u => u.Role).Select(u => (u.Id, u.Role, u.Status)));
        }
    }

    [Fact]
    public async Task Each_organisation_has_its_active_participant_and_sees_none_of_the_other_s()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var (a, b) = sqlServer.Seeded;

        foreach (var (seeded, other) in new[] { (a, b), (b, a) })
        {
            // Other tests may add participants to a seeded organisation, so it reads the seeded two by their IDs.
            using (tenant.Use(seeded.OrganisationId))
            {
                var participants = await db.Participants
                    .Where(p => p.Id == seeded.ParticipantId || p.Id == other.ParticipantId)
                    .Select(p => new { p.Id, p.OrganisationId, p.Status, p.ArchivedAtUtc })
                    .ToListAsync(cancellationToken);

                var participant = Assert.Single(participants);
                Assert.Equal(seeded.ParticipantId, participant.Id);
                Assert.Equal(seeded.OrganisationId, participant.OrganisationId);
                Assert.Equal(ParticipantStatus.Active, participant.Status);
                Assert.Null(participant.ArchivedAtUtc);
            }
        }
    }

    // As admin bootstrap gives an organisation its group (design.md §7.4).
    [Fact]
    public async Task Each_organisation_has_its_one_active_Every_note_group_and_sees_none_of_the_other_s()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var (a, b) = sqlServer.Seeded;

        foreach (var seeded in new[] { a, b })
        {
            // Other tests may add groups to a seeded organisation, but none can add a second Every note group.
            using (tenant.Use(seeded.OrganisationId))
            {
                var everyNote = Assert.Single(await db.CommonItemGroups
                    .Where(group => group.IsEveryNote)
                    .ToListAsync(cancellationToken));

                Assert.Equal(
                    (seeded.EveryNoteGroupId, seeded.OrganisationId, "Every note", 0, (DateTime?)null),
                    (everyNote.Id, everyNote.OrganisationId, everyNote.Name, everyNote.SortOrder,
                        everyNote.ArchivedAtUtc));
            }
        }
    }

    [Fact]
    public async Task Identity_finds_each_seeded_user_by_a_confirmed_example_org_address_and_has_a_security_stamp()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var (a, b) = sqlServer.Seeded;

        foreach (var userId in new[] { a.ManagerId, a.WorkerId, a.SupportId, b.ManagerId, b.WorkerId, b.SupportId })
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
