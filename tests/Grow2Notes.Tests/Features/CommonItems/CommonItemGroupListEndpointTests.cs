using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>GET /api/admin/common-item-groups?includeArchived=</c> (design.md §6.6), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): a manager gets their organisation's active groups, Every note first, each with its
/// active items, or with <c>includeArchived=true</c> the archived groups and items too, in the list's orders, each in
/// §6.6's shape with its stored <c>RowVersion</c>, as JSON that is never stored (§6.1); another organisation's groups
/// and items are never in it (§5.9 item 7); and no call writes an audit event (A28). The tests add their own groups
/// and items to seeded organisation A, or to an organisation of their own where a test must know every group, and
/// change no seeded row (tests/README.md). The endpoint matrix covers who may call it, and
/// <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class CommonItemGroupListEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/admin/common-item-groups";

    // Made up (A35), with milliseconds, which datetime2(3) keeps.
    private static readonly DateTime AddedAtUtc = TestCommonItems.AddedAtUtc;

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_manager_gets_the_groups_as_JSON_that_is_never_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = ClientFor(Caller.ManagerOfA);

        using var response = await client.GetAsync(Path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        var groups = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(seeded.A.EveryNoteGroupId, groups.EnumerateArray().Select(Id));
    }

    // In an organisation of the test's own, so that the list holds no group but these: Every note comes first by
    // IsEveryNote alone, though it was added last, with the highest SortOrder.
    [Fact]
    public async Task Every_note_comes_first_though_added_last_with_the_highest_SortOrder()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        var added = await factory.Services.AddGroupsAsync(
            organisationId,
            new("Community outing", 0),
            new("Personal care", 1, AddedAtUtc.AddMinutes(1)));
        var everyNote = await AddEveryNoteAsync(organisationId, sortOrder: 2, AddedAtUtc.AddMinutes(2));

        var groups = await ListAsync(ClientFor(managerId), Path);

        Assert.Equal([everyNote, added[0], added[1]], groups.Select(Id));
        Assert.Equal([true, false, false], groups.Select(group => group.GetProperty("isEveryNote").GetBoolean()));
    }

    // SortOrder first, whatever the order they were added in; then, for two of one SortOrder, as two adds at once can
    // leave them, CreatedAtUtc; then Id, in SQL Server's order, which compares a uniqueidentifier by its last six bytes
    // first, so ...0001 comes before ...0002 there, where .NET's Guid order has them the other way round. The earliest
    // row's ID comes after both of theirs there, so only the CreatedAtUtc step puts it first. Rows cannot tell the Id
    // step from the order SQL Server keeps them in, so CommonItemGroupListOrderTests checks the ORDER BY itself. A's
    // list holds other tests' groups too, so the groups are looked at alone, in the list's order.
    [Fact]
    public async Task Groups_and_each_group_s_items_are_in_SortOrder_then_CreatedAtUtc_then_Id_order()
    {
        var a = seeded.A.OrganisationId;
        var earliestGroup = Guid.Parse("7e3a9b20-1c2d-4e3f-8a4b-ffffffffffff");
        var tiedSecondGroup = Guid.Parse("2b3c4d5e-6f70-4a81-9b2c-d4e5f6000002");
        var tiedFirstGroup = Guid.Parse("a0b1c2d3-e4f5-4a06-8b17-d4e5f6000001");
        var addedGroups = await factory.Services.AddGroupsAsync(
            a,
            new("Overnight respite", 2),
            new("Personal care", 1, AddedAtUtc.AddSeconds(1), tiedSecondGroup),
            new("Household", 1, AddedAtUtc.AddSeconds(1), tiedFirstGroup),
            new("Community outing", 1, AddedAtUtc, earliestGroup),
            new("Appointments", 0, AddedAtUtc.AddMinutes(5)));
        var earliestItem = Guid.Parse("8f4b0c31-2d3e-4f40-9b5c-ffffffffffff");
        var tiedSecondItem = Guid.Parse("3c4d5e6f-7081-4b92-8c3d-e5f607000002");
        var tiedFirstItem = Guid.Parse("b1c2d3e4-f5a6-4b17-9c28-e5f607000001");
        var addedItems = await factory.Services.AddItemsAsync(
            a,
            earliestGroup,
            new("Walked to the shop", 2),
            new("Paid at the counter", 1, AddedAtUtc.AddSeconds(1), tiedSecondItem),
            new("Caught the bus", 1, AddedAtUtc.AddSeconds(1), tiedFirstItem),
            new("Planned the trip", 1, AddedAtUtc, earliestItem),
            new("Checked the weather", 0, AddedAtUtc.AddMinutes(5)));

        var groups = (await ListAsync(ClientFor(Caller.ManagerOfA), Path))
            .Where(group => addedGroups.Contains(Id(group)))
            .ToList();

        Assert.Equal(
            [addedGroups[4], earliestGroup, tiedFirstGroup, tiedSecondGroup, addedGroups[0]], groups.Select(Id));
        Assert.Equal(
            [addedItems[4], earliestItem, tiedFirstItem, tiedSecondItem, addedItems[0]],
            ItemsOf(groups.Single(group => Id(group) == earliestGroup)).Select(Id));
    }

    // An archived group's items, active or not, go with it, and an active group keeps its active items alone.
    [Theory]
    [InlineData("")]
    [InlineData("?includeArchived=false")]
    public async Task Without_includeArchived_true_the_list_leaves_out_archived_groups_and_items(string query)
    {
        var a = seeded.A.OrganisationId;
        var groups = await factory.Services.AddGroupsAsync(
            a, new("Community outing", 0), new("Overnight respite", 1, ArchivedAtUtc: AddedAtUtc));
        var activeGroupItems = await factory.Services.AddItemsAsync(
            a, groups[0], new("Caught the bus", 0), new("Paid at the counter", 1, ArchivedAtUtc: AddedAtUtc));
        await factory.Services.AddItemsAsync(a, groups[1], new TestCommonItem("Packed a bag", 0));

        var listed = await ListAsync(ClientFor(Caller.ManagerOfA), Path + query);

        Assert.DoesNotContain(groups[1], listed.Select(Id));
        Assert.Equal([activeGroupItems[0]], ItemsOf(listed.Single(group => Id(group) == groups[0])).Select(Id));
    }

    // An archived item keeps its SortOrder (Archiving.Archive), so it takes its place among the active ones here, as an
    // archived group does among the groups.
    [Fact]
    public async Task With_includeArchived_true_the_list_holds_an_archived_group_with_its_items_and_archived_items()
    {
        var a = seeded.A.OrganisationId;
        var groups = await factory.Services.AddGroupsAsync(
            a,
            new("Community outing", 0),
            new("Overnight respite", 1, ArchivedAtUtc: AddedAtUtc),
            new("Personal care", 2));
        var activeGroupItems = await factory.Services.AddItemsAsync(
            a,
            groups[0],
            new("Caught the bus", 0),
            new("Paid at the counter", 1, ArchivedAtUtc: AddedAtUtc),
            new("Walked to the shop", 2));
        var archivedGroupItems = await factory.Services.AddItemsAsync(
            a, groups[1], new("Packed a bag", 0), new("Made the bed", 1, ArchivedAtUtc: AddedAtUtc));

        var listed = (await ListAsync(ClientFor(Caller.ManagerOfA), $"{Path}?includeArchived=true"))
            .Where(group => groups.Contains(Id(group)))
            .ToList();

        Assert.Equal(groups, listed.Select(Id));
        Assert.Equal(activeGroupItems, ItemsOf(listed[0]).Select(Id));
        Assert.Equal(archivedGroupItems, ItemsOf(listed[1]).Select(Id));
        Assert.Empty(ItemsOf(listed[2]));
    }

    // The name and text as stored, accents and all; the times they were archived at in UTC, to the millisecond, or
    // null; and the stored RowVersions as base64, which If-Match sends back.
    [Fact]
    public async Task A_group_and_an_item_have_section_6_6_s_shape_with_their_stored_values_and_RowVersions()
    {
        var a = seeded.A.OrganisationId;
        var groupId = Assert.Single(await factory.Services.AddGroupsAsync(
            a, new TestCommonItemGroup("Đi chợ với Nguyễn", 3, ArchivedAtUtc: AddedAtUtc)));
        var itemIds = await factory.Services.AddItemsAsync(
            a, groupId, new("Cooked phở, then tidied up", 4), new("Paid at the counter", 5, ArchivedAtUtc: AddedAtUtc));

        var group = (await ListAsync(ClientFor(Caller.ManagerOfA), $"{Path}?includeArchived=true"))
            .Single(listed => Id(listed) == groupId);

        Assert.Equal(
            ["id", "name", "isEveryNote", "sortOrder", "archivedAtUtc", "rowVersion", "items"],
            group.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Đi chợ với Nguyễn", group.GetProperty("name").GetString());
        Assert.False(group.GetProperty("isEveryNote").GetBoolean());
        Assert.Equal(3, group.GetProperty("sortOrder").GetInt32());
        Assert.Equal("2026-10-03T04:05:06.789Z", group.GetProperty("archivedAtUtc").GetString());
        Assert.Equal(
            Convert.ToBase64String(await StoredRowVersionAsync(
                db => db.CommonItemGroups.Where(row => row.Id == groupId).Select(row => row.RowVersion))),
            group.GetProperty("rowVersion").GetString());
        var items = ItemsOf(group);
        Assert.Equal(
            ["id", "text", "sortOrder", "archivedAtUtc", "rowVersion"],
            items[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal("Cooked phở, then tidied up", items[0].GetProperty("text").GetString());
        Assert.Equal(4, items[0].GetProperty("sortOrder").GetInt32());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("archivedAtUtc").ValueKind);
        Assert.Equal("2026-10-03T04:05:06.789Z", items[1].GetProperty("archivedAtUtc").GetString());
        foreach (var (item, id) in items.Zip(itemIds))
        {
            Assert.Equal(
                Convert.ToBase64String(await StoredRowVersionAsync(
                    db => db.CommonItems.Where(row => row.Id == id).Select(row => row.RowVersion))),
                item.GetProperty("rowVersion").GetString());
        }
    }

    // As bootstrap and the migration leave it: named "Every note", built in, with SortOrder 0, and active.
    [Fact]
    public async Task The_seeded_Every_note_is_listed_as_built_in()
    {
        var group = (await ListAsync(ClientFor(Caller.ManagerOfA), $"{Path}?includeArchived=true"))
            .Single(listed => Id(listed) == seeded.A.EveryNoteGroupId);

        Assert.Equal("Every note", group.GetProperty("name").GetString());
        Assert.True(group.GetProperty("isEveryNote").GetBoolean());
        Assert.Equal(0, group.GetProperty("sortOrder").GetInt32());
        Assert.Equal(JsonValueKind.Null, group.GetProperty("archivedAtUtc").ValueKind);
    }

    // B's manager's list holds B's seeded Every note, and no group or item of A's, archived or not, wherever it is, as
    // the tenant is the caller's (§5.9 item 7).
    [Fact]
    public async Task B_s_manager_gets_B_s_seeded_Every_note_and_nothing_of_A_s()
    {
        var a = seeded.A.OrganisationId;
        var groupsOfA = await factory.Services.AddGroupsAsync(
            a, new("Community outing", 0), new("Overnight respite", 1, ArchivedAtUtc: AddedAtUtc));
        List<Guid> itemsOfA =
        [
            .. await factory.Services.AddItemsAsync(
                a, groupsOfA[0], new("Caught the bus", 0), new("Paid at the counter", 1, ArchivedAtUtc: AddedAtUtc)),
            .. await factory.Services.AddItemsAsync(a, groupsOfA[1], new TestCommonItem("Packed a bag", 0)),
        ];

        var listed = await ListAsync(ClientFor(Caller.ManagerOfB), $"{Path}?includeArchived=true");

        var groupIds = listed.Select(Id).ToList();
        Assert.Equal((await StoredGroupIdsAsync(seeded.B.OrganisationId)).Order(), groupIds.Order());
        Assert.Contains(seeded.B.EveryNoteGroupId, groupIds);
        Assert.DoesNotContain(seeded.A.EveryNoteGroupId, groupIds);
        Assert.DoesNotContain(groupIds, groupsOfA.Contains);
        Assert.DoesNotContain(listed.SelectMany(ItemsOf).Select(Id), itemsOfA.Contains);
    }

    // design.md §5.4 lists no event for reading, whoever asks (A28).
    [Fact]
    public async Task The_calls_write_no_audit_event()
    {
        var a = seeded.A.OrganisationId;
        var groupId = Assert.Single(await factory.Services.AddGroupsAsync(a, new TestCommonItemGroup("Household", 0)));
        await factory.Services.AddItemsAsync(a, groupId, new TestCommonItem("Did the dishes", 0));
        var lastEventId = await LastEventIdAsync();

        foreach (var (caller, path, status) in new[]
        {
            (Caller.ManagerOfA, Path, HttpStatusCode.OK),
            (Caller.ManagerOfA, $"{Path}?includeArchived=true", HttpStatusCode.OK),
            (Caller.ManagerOfB, Path, HttpStatusCode.OK),
            (Caller.WorkerOfA, Path, HttpStatusCode.Forbidden),
        })
        {
            using var client = ClientFor(caller);
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(status, response.StatusCode);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    private static Guid Id(JsonElement row) => row.GetProperty("id").GetGuid();

    private static List<JsonElement> ItemsOf(JsonElement group) => [.. group.GetProperty("items").EnumerateArray()];

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    private HttpClient ClientFor(Guid userId) => factory.CreateHttpsClient().SignInAs(userId);

    // The answer's groups, in its order. The client is disposed with the call.
    private static async Task<List<JsonElement>> ListAsync(HttpClient client, string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using (client)
        {
            using var response = await client.GetAsync(path, cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var groups = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return [.. groups.EnumerateArray()];
        }
    }

    // An organisation of the test's own, with no group yet, and an Active manager, who needs no passkey or password to
    // sign in with the test-only sign-in. An organisation is the tenant, not tenant-owned, and AspNetUsers is not
    // filtered, so neither save needs a tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationWithAManagerAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Common items list test", CreatedAtUtc = AddedAtUtc };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = $"{Guid.NewGuid():N}@example.org";
        var manager = new ApplicationUser
        {
            OrganisationId = organisation.Id,
            DisplayName = "Jamie Lee",
            Role = UserRole.Manager,
            Status = UserStatus.Active,
            InvitedAtUtc = AddedAtUtc,
            ActivatedAtUtc = AddedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(manager);
        Assert.True(result.Succeeded);
        return (organisation.Id, manager.Id);
    }

    // The organisation's Every note group, made as the app makes it, with the SortOrder given.
    private async Task<Guid> AddEveryNoteAsync(Guid organisationId, int sortOrder, DateTime createdAtUtc)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var everyNote = CommonItemGroup.EveryNote(organisationId, createdAtUtc);
        everyNote.SortOrder = sortOrder;
        db.CommonItemGroups.Add(everyNote);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return everyNote.Id;
    }

    // A row of A's as it holds its version, read in a scope of its own, as a request reads it.
    private async Task<byte[]> StoredRowVersionAsync(Func<Grow2NotesDbContext, IQueryable<byte[]>> rowVersion)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await rowVersion(db).SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // Read with the tenant filter off, so that it is the table, not the filter under test, that says what B holds.
    private async Task<List<Guid>> StoredGroupIdsAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [CommonItemGroup] WHERE [OrganisationId] = {organisationId}")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after its requests.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
