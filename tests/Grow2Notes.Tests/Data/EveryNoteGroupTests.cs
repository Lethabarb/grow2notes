using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The built-in Every note group on SQL Server (design.md §5.3 <i>Constraints</i>, A43): the database itself allows an
/// organisation one Every note group, which is never archived, whatever the app's code does, and leaves its other
/// groups to the app; and the migration that added the table gave each organisation already there its one (D82). The
/// rows are read in SQL, so that neither the tenant filter nor a tracked row decides what is seen.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class EveryNoteGroupTests(SqlServerFixture sqlServer, Grow2NotesFactory factory)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime AddedAtUtc = new(2026, 10, 18, 1, 2, 3, 456, DateTimeKind.Utc);

    [Fact]
    public async Task A_second_Every_note_group_is_refused_by_the_filtered_unique_index_and_writes_nothing()
    {
        var (organisationId, _) = await AddOrganisationAsync();
        var before = await StoredGroupsAsync(organisationId);

        var refusal = await RefusedSaveAsync(organisationId, db =>
        {
            db.CommonItemGroups.Add(CommonItemGroup.EveryNote(organisationId, AddedAtUtc));
            return Task.CompletedTask;
        });

        // 2601: a duplicate key in a unique index.
        Assert.Equal(2601, refusal.Number);
        Assert.Contains("'IX_CommonItemGroup_OrganisationId'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, await StoredGroupsAsync(organisationId));
    }

    [Fact]
    public async Task Archiving_Every_note_is_refused_by_the_check_constraint_and_writes_nothing()
    {
        var (organisationId, everyNoteGroupId) = await AddOrganisationAsync();
        var before = await StoredGroupsAsync(organisationId);

        var refusal = await RefusedSaveAsync(organisationId, async db =>
        {
            var everyNote = await db.CommonItemGroups.SingleAsync(
                group => group.Id == everyNoteGroupId, TestContext.Current.CancellationToken);
            everyNote.ArchivedAtUtc = AddedAtUtc;
        });

        // 547: a statement conflicted with a constraint.
        Assert.Equal(547, refusal.Number);
        Assert.Contains("\"CK_CommonItemGroup_EveryNoteActive\"", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, await StoredGroupsAsync(organisationId));
    }

    [Fact]
    public async Task An_organisation_s_other_groups_can_be_many_and_archived()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (organisationId, _) = await AddOrganisationAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var outing = OtherGroup("Community outing", sortOrder: 0);
        var care = OtherGroup("Personal care", sortOrder: 1);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            db.CommonItemGroups.AddRange(outing, care);
            await db.SaveChangesAsync(cancellationToken);
            outing.ArchivedAtUtc = AddedAtUtc;
            await db.SaveChangesAsync(cancellationToken);
        }

        Assert.Equal(
            [
                ("Community outing", false, AddedAtUtc),
                ("Every note", true, null),
                ("Personal care", false, null),
            ],
            (await db.Database.SqlQuery<StoredGroup>($"""
                    SELECT [Name], [IsEveryNote], [ArchivedAtUtc]
                    FROM [CommonItemGroup]
                    WHERE [OrganisationId] = {organisationId}
                    """).ToListAsync(cancellationToken))
                .Select(group => (group.Name, group.IsEveryNote, group.ArchivedAtUtc))
                .OrderBy(group => group.Name, StringComparer.Ordinal));

        CommonItemGroup OtherGroup(string name, int sortOrder) => new()
        {
            OrganisationId = organisationId,
            Name = name,
            SortOrder = sortOrder,
            CreatedAtUtc = AddedAtUtc,
        };
    }

    // On a database of its own, migrated as far as the migration before AddCommonItems, which is found from the
    // context's list of migrations, so that a migration that another change puts before it needs no change here.
    [Fact]
    public async Task AddCommonItems_gives_each_organisation_already_there_exactly_one_active_Every_note_group()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var services = SqlServerFixture.AppDataServices(
            sqlServer.ConnectionStringFor("AddCommonItemsOnThreeOrganisations"));
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToList();
        var addCommonItems = migrations.FindIndex(
            migration => migration.EndsWith("_AddCommonItems", StringComparison.Ordinal));
        await migrator.MigrateAsync(migrations[addCommonItems - 1], cancellationToken);

        // In SQL, as the organisations were there before the migration that this model came with.
        Guid[] organisationIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        foreach (var organisationId in organisationIds)
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO [Organisation] ([Id], [Name], [CreatedAtUtc])
                VALUES ({organisationId}, N'Migrated organisation', {AddedAtUtc})
                """, cancellationToken);
        }

        var startedAtUtc = await ServerTimeAsync(db);
        await migrator.MigrateAsync(migrations[addCommonItems], cancellationToken);
        var finishedAtUtc = await ServerTimeAsync(db);

        var groups = await db.Database.SqlQuery<MigratedGroup>($"""
            SELECT [Id], [OrganisationId], [Name], [SortOrder], [IsEveryNote], [ArchivedAtUtc], [CreatedAtUtc]
            FROM [CommonItemGroup]
            """).ToListAsync(cancellationToken);
        Assert.Equal(organisationIds.Order(), groups.Select(group => group.OrganisationId).Order());
        Assert.Distinct(groups.Select(group => group.Id));
        Assert.All(groups, group =>
        {
            Assert.Equal(
                ("Every note", 0, true, (DateTime?)null),
                (group.Name, group.SortOrder, group.IsEveryNote, group.ArchivedAtUtc));
            Assert.InRange(group.CreatedAtUtc, startedAtUtc, finishedAtUtc);
        });
    }

    // An organisation of the test's own with its Every note group, as admin bootstrap adds them (design.md §7.4), so
    // that a change that the database should refuse but lets through stays where no other test looks, and never
    // reaches a seeded organisation's Every note group, which other tests read as bootstrap leaves it.
    private async Task<(Guid OrganisationId, Guid EveryNoteGroupId)> AddOrganisationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Every note test", CreatedAtUtc = AddedAtUtc };
        db.Organisations.Add(organisation);
        var everyNote = CommonItemGroup.EveryNote(organisation.Id, AddedAtUtc);
        db.CommonItemGroups.Add(everyNote);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.Id))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return (organisation.Id, everyNote.Id);
    }

    // Makes the change through the app's context under the organisation as tenant, as the app's code would, and gives
    // the error with which SQL Server refused the save.
    private async Task<SqlException> RefusedSaveAsync(Guid organisationId, Func<Grow2NotesDbContext, Task> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId);

        await change(db);
        var error = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        return Assert.IsType<SqlException>(error.InnerException);
    }

    // Every column of the organisation's groups, its row versions included, as SQL Server writes them in JSON, read in
    // a scope of its own after the save.
    private async Task<string?> StoredGroupsAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return Assert.Single(await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string?>($"""
                SELECT (SELECT * FROM [CommonItemGroup]
                        WHERE [OrganisationId] = {organisationId}
                        ORDER BY [Id]
                        FOR JSON PATH) AS [Value]
                """).ToListAsync(TestContext.Current.CancellationToken));
    }

    // To the millisecond, as the datetime2(3) of CreatedAtUtc rounds it, so that a time the migration wrote between two
    // of these lies between them.
    private static async Task<DateTime> ServerTimeAsync(Grow2NotesDbContext db) =>
        Assert.Single(await db.Database
            .SqlQuery<DateTime>($"SELECT CAST(SYSUTCDATETIME() AS datetime2(3)) AS [Value]")
            .ToListAsync(TestContext.Current.CancellationToken));

    private sealed record StoredGroup(string Name, bool IsEveryNote, DateTime? ArchivedAtUtc);

    private sealed record MigratedGroup(
        Guid Id,
        Guid OrganisationId,
        string Name,
        int SortOrder,
        bool IsEveryNote,
        DateTime? ArchivedAtUtc,
        DateTime CreatedAtUtc);
}
