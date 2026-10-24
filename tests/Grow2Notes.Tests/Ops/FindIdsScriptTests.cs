using System.Data;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// <c>ops/find-ids.sql</c>, which the operator runs to find the IDs that the other scripts ask for. Each test runs it
/// on a migrated database of its own, so it knows every row that the script returns.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class FindIdsScriptTests(SqlServerFixture sqlServer)
{
    private static readonly DateTime AddedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task It_lists_every_organisation_participant_and_user_with_the_ID_and_words_for_status_and_role()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "FindIdsListsEverything", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        await RenameAsync(db, a, "Find ids A");
        await RenameAsync(db, b, "Find ids B");
        var sam = await db.Services.AddParticipantAsync(a, archived: true, givenName: "Sam", familyName: "Adams");
        var alex = await db.Services.AddParticipantAsync(a, givenName: "Alex", familyName: "Adams");
        var kim = await db.Services.AddParticipantAsync(b, givenName: "Kim", familyName: "Brown");
        var invited = await AddUserAsync(db, a, UserRole.Worker, UserStatus.Invited, "Casey Quinn");
        var deactivated = await AddUserAsync(db, a, UserRole.Manager, UserStatus.Deactivated, "Drew Moss");

        var results = await OpsScripts.RunAsync(db.ConnectionString, "ops/find-ids.sql", cancellationToken: ct);

        Assert.Equal(3, results.Count);
        Assert.Equal(["Organisation", "Organisation ID"], ColumnNames(results[0]));
        Assert.Equal(
            ["Organisation", "Family name", "Given name", "Status", "Participant ID"], ColumnNames(results[1]));
        Assert.Equal(["Organisation", "Name", "Role", "Status", "User ID"], ColumnNames(results[2]));
        Assert.Equal(
            [
                $"Find ids A|{a.OrganisationId}",
                $"Find ids B|{b.OrganisationId}",
            ],
            Rows(results[0]));
        Assert.Equal(
            [
                $"Find ids A|Adams|Alex|Active|{alex.Id}",
                $"Find ids A|Adams|Sam|Archived|{sam.Id}",
                $"Find ids A|Citizen|Jane|Active|{a.ParticipantId}",
                $"Find ids B|Brown|Kim|Active|{kim.Id}",
                $"Find ids B|Citizen|Jane|Active|{b.ParticipantId}",
            ],
            Rows(results[1]));
        Assert.Equal(
            [
                $"Find ids A|Casey Quinn|Worker|Invited|{invited}",
                $"Find ids A|Drew Moss|Manager|Deactivated|{deactivated}",
                $"Find ids A|Manager|Manager|Active|{a.ManagerId}",
                $"Find ids A|Worker|Worker|Active|{a.WorkerId}",
                $"Find ids B|Manager|Manager|Active|{b.ManagerId}",
                $"Find ids B|Worker|Worker|Active|{b.WorkerId}",
            ],
            Rows(results[2]));
    }

    private static string[] ColumnNames(DataTable table) =>
        [.. table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)];

    private static string[] Rows(DataTable table) =>
        [.. table.Rows.Cast<DataRow>().Select(row => string.Join('|', row.ItemArray))];

    // The organisations' names are random, so a name is set to make their order known.
    private static async Task RenameAsync(OpsDatabase db, SeededOrganisation organisation, string name)
    {
        await using var scope = db.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        await context.Organisations.Where(o => o.Id == organisation.OrganisationId).ExecuteUpdateAsync(
            update => update.SetProperty(o => o.Name, name), TestContext.Current.CancellationToken);
    }

    // TestUsers adds Active users only, and the script must show the other two statuses.
    private static async Task<Guid> AddUserAsync(
        OpsDatabase db, SeededOrganisation organisation, UserRole role, UserStatus status, string displayName)
    {
        var email = $"user.{Guid.NewGuid():N}@example.org";
        var user = new ApplicationUser
        {
            OrganisationId = organisation.OrganisationId,
            DisplayName = displayName,
            Role = role,
            Status = status,
            InvitedAtUtc = AddedAtUtc,
            InvitedByUserId = organisation.ManagerId,
            DeactivatedAtUtc = status == UserStatus.Deactivated ? AddedAtUtc : null,
            UserName = email,
            Email = email,
        };
        await using var scope = db.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return user.Id;
    }
}
