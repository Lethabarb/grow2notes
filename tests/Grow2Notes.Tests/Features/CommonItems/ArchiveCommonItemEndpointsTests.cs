using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-items/{id}/archive</c> and <c>/restore</c> (design.md §3.7, §6.6, S01.03.04), called with
/// the test-only sign-in (<see cref="TestSignIn"/>) on a fake clock: a manager archives an item of their own
/// organisation, which keeps its <c>SortOrder</c>, or restores one, which goes to the end of its own group's active
/// items, with the item's <c>ETag</c> as <c>If-Match</c>, and gets <c>204</c>; with no <c>If-Match</c>, <c>428</c>;
/// with a stale one, <c>412</c>; and for an item that is unknown, or another organisation's, <c>404</c>; each of those
/// changing nothing and writing no event. Archiving an archived item, or restoring an active one, answers <c>204</c>
/// too, and saves nothing (D84). Each test acts on an item that it adds to a group of its own in A, and changes no
/// seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the events that they write.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ArchiveCommonItemEndpointsTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Archive = "archive";
    private const string Restore = "restore";

    private const string StoredText = "Travelled by bus or train";

    private static readonly DateTime ArchivedAtUtc = TestCommonItems.AddedAtUtc;

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public ArchiveCommonItemEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    // Each action, on an item it changes: an active one to archive, an archived one to restore.
    public static TheoryData<string> Actions { get; } = new(Archive, Restore);

    [Fact]
    public async Task Archive_answers_204_and_archives_it_at_the_clocks_time_in_its_place_with_a_new_version()
    {
        var (groupId, item) = await AddItemAsync(new(StoredText, 2));

        using var response = await PostAsync(Caller.ManagerOfA, item.Id, Archive, item.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            (StoredText, 2, (DateTime?)TestClock.Start.UtcDateTime, groupId),
            (stored.Text, stored.SortOrder, stored.ArchivedAtUtc, stored.GroupId));
        Assert.NotEqual(item.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.CommonItem.Archived], await EventTypesAsync(item.Id));
    }

    // A2: one more than the highest active SortOrder of the item's own group, past the gap that an archive left, and
    // whatever SortOrder the group's archived items hold, its own included, as only a reorder closes gaps; another
    // group's items, whose highest SortOrder is higher, count for nothing, and stay as they were.
    [Fact]
    public async Task Restore_answers_204_and_puts_it_one_after_the_highest_active_SortOrder_of_its_own_group()
    {
        var groupId = await AddGroupAsync();
        await app.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            groupId,
            new("Paid for a ticket at the counter", 0),
            new("Bought lunch", 3),
            new("Packed a day bag", 7, ArchivedAtUtc: ArchivedAtUtc));
        var item = await app.Services.AddItemAsync(
            seeded.A.OrganisationId, groupId, new(StoredText, 1, ArchivedAtUtc: ArchivedAtUtc));
        var otherGroupId = await AddGroupAsync();
        var otherItem = await app.Services.AddItemAsync(seeded.A.OrganisationId, otherGroupId, new("Showered", 9));

        using var response = await PostAsync(Caller.ManagerOfA, item.Id, Restore, item.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            (StoredText, 4, (DateTime?)null, groupId),
            (stored.Text, stored.SortOrder, stored.ArchivedAtUtc, stored.GroupId));
        Assert.NotEqual(item.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.CommonItem.Restored], await EventTypesAsync(item.Id));
        var other = await StoredAsync(otherItem.Id);
        Assert.Equal((9, otherItem.ETag), (other.SortOrder, RowVersionToken.ETag(other)));
    }

    // Another group's active items do not make the restored item's group any less empty.
    [Fact]
    public async Task An_item_restored_to_a_group_with_no_active_item_is_first_whatever_another_group_holds()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 5, ArchivedAtUtc: ArchivedAtUtc));
        var otherGroupId = await AddGroupAsync();
        await app.Services.AddItemsAsync(seeded.A.OrganisationId, otherGroupId, new("Showered", 0), new("Dressed", 1));

        using var response = await PostAsync(Caller.ManagerOfA, item.Id, Restore, item.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await StoredAsync(item.Id)).SortOrder);
    }

    // D84: a repeat finds nothing to change, so an archived item keeps the time it was archived at, and an active one
    // its place.
    [Theory]
    [InlineData(Archive, true)]
    [InlineData(Restore, false)]
    public async Task An_item_already_so_answers_204_and_nothing_is_saved(string action, bool archived)
    {
        var (_, item) = await AddItemAsync(ToChange(archived));

        using var response = await PostAsync(Caller.ManagerOfA, item.Id, action, item.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertUnchangedAsync(item, archived);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing(string action)
    {
        var (_, item) = await AddItemAsync(ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.ManagerOfA, item.Id, action, ifMatch: null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(item, archived: action == Restore);
    }

    // As when another manager reworded the item first: the action, sent with the version both loaded, changes nothing.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing(string action)
    {
        var archived = action == Restore;
        var (_, item) = await AddItemAsync(ToChange(archived));
        using (var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded))
        using (var reword = await client.PutWithTokenAsync(
                   $"/api/admin/common-items/{item.Id}",
                   await client.FetchTokenAsync(),
                   new { text = "Travelled by tram" },
                   item.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, reword.StatusCode);
        }

        var reworded = RowVersionToken.ETag(await StoredAsync(item.Id));

        using var response = await PostAsync(Caller.ManagerOfA, item.Id, action, item.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            (2, archived ? ArchivedAtUtc : (DateTime?)null, reworded),
            (stored.SortOrder, stored.ArchivedAtUtc, RowVersionToken.ETag(stored)));
        Assert.Equal([AuditEventTypes.CommonItem.Updated], await EventTypesAsync(item.Id));
    }

    // B's manager, sending the item's current ETag, as one who had it would: the tenant filter finds no such item in B,
    // so the version is never checked (design.md §9.2).
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Another_organisation_s_item_is_not_found_and_not_changed(string action)
    {
        var (_, item) = await AddItemAsync(ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.ManagerOfB, item.Id, action, item.ETag);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        await AssertUnchangedAsync(item, archived: action == Restore);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_no_item_has_is_not_found(string action)
    {
        using var response = await PostAsync(Caller.ManagerOfA, Guid.NewGuid(), action, IfMatch.ETag("AAAAAAAAB9E="));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // The routes take a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_is_not_a_GUID_is_not_found(string action)
    {
        using var response = await PostAsync(
            Caller.ManagerOfA, $"/api/admin/common-items/travelled-by-bus/{action}", IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // design.md §6.6 gives neither a config.group_archived, so an archived group's item is archived and restored as an
    // active group's is (T06's Notes); the screen shows no archived group's items (T07).
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_archived_group_s_item_is_changed_as_an_active_group_s_is(string action)
    {
        var groupId = Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Overnight respite", 0, ArchivedAtUtc: ArchivedAtUtc)));
        var item = await app.Services.AddItemAsync(
            seeded.A.OrganisationId, groupId, ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.ManagerOfA, item.Id, action, item.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await StoredAsync(item.Id);
        Assert.Equal(action == Archive ? TestClock.Start.UtcDateTime : (DateTime?)null, stored.ArchivedAtUtc);
        Assert.Equal(
            [action == Archive ? AuditEventTypes.CommonItem.Archived : AuditEventTypes.CommonItem.Restored],
            await EventTypesAsync(item.Id));
    }

    // The screen reads the groups with the archived items, in the list's order, and sends the version that the list
    // gives each. An archived item keeps its place in that order, so the screen shows the archived ones apart.
    [Fact]
    public async Task After_Archive_it_is_listed_as_archived_in_its_place_and_after_Restore_last()
    {
        var groupId = await AddGroupAsync();
        var ids = await app.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new(StoredText, 0), new("Bought lunch", 1));
        var (bus, lunch) = (ids[0], ids[1]);

        var listed = await ListedItemsAsync(groupId);
        using (var archived = await PostAsync(Caller.ManagerOfA, bus, Archive, VersionOf(listed, bus)))
        {
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        }

        listed = await ListedItemsAsync(groupId);
        Assert.Equal([(bus, "2026-10-09T13:30:00Z"), (lunch, null)], Places(listed));

        using (var restored = await PostAsync(Caller.ManagerOfA, bus, Restore, VersionOf(listed, bus)))
        {
            Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        }

        Assert.Equal([(lunch, null), (bus, null)], Places(await ListedItemsAsync(groupId)));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // Each listed item's ID and the time it was archived at, as the list gives it, in the list's order.
    private static List<(Guid Id, string? ArchivedAtUtc)> Places(List<JsonElement> listed) =>
        [.. listed.Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("archivedAtUtc").GetString()))];

    // The If-Match of the listed item of ID `itemId`, from the version that the list gives it.
    private static string VersionOf(List<JsonElement> listed, Guid itemId) =>
        IfMatch.ETag(listed.Single(item => item.GetProperty("id").GetGuid() == itemId)
            .GetProperty("rowVersion").GetString()!);

    // An item that the action changes: an active one to archive, an archived one to restore, each in a place of its
    // own.
    private static TestCommonItem ToChange(bool archived) =>
        archived ? new(StoredText, 2, ArchivedAtUtc: ArchivedAtUtc) : new(StoredText, 2);

    // The item as AddItemAsync left it, in its place and at the same version, with no event written about it.
    private async Task AssertUnchangedAsync(AddedCommonItem item, bool archived)
    {
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            (2, archived ? ArchivedAtUtc : (DateTime?)null, item.ETag),
            (stored.SortOrder, stored.ArchivedAtUtc, RowVersionToken.ETag(stored)));
        Assert.Empty(await EventTypesAsync(item.Id));
    }

    // An active group added to A for the test.
    private async Task<Guid> AddGroupAsync() =>
        Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));

    // An item, as given, of an active group added to A for the test.
    private async Task<(Guid GroupId, AddedCommonItem Item)> AddItemAsync(TestCommonItem item)
    {
        var groupId = await AddGroupAsync();
        return (groupId, await app.Services.AddItemAsync(seeded.A.OrganisationId, groupId, item));
    }

    private Task<HttpResponseMessage> PostAsync(Caller caller, Guid itemId, string action, string? ifMatch) =>
        PostAsync(caller, $"/api/admin/common-items/{itemId}/{action}", ifMatch);

    // As the SPA archives or restores one: over HTTPS, with a token fetched as the caller, and no body.
    private async Task<HttpResponseMessage> PostAsync(Caller caller, string path, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PostWithTokenAsync(path, await client.FetchTokenAsync(), ifMatch: ifMatch);
    }

    // The items of the group of ID `groupId`, archived ones included, as A's manager's list gives them to the screen.
    private async Task<List<JsonElement>> ListedItemsAsync(Guid groupId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var response = await client.GetAsync(
            "/api/admin/common-item-groups?includeArchived=true", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return
        [
            .. groups.EnumerateArray()
                .Single(group => group.GetProperty("id").GetGuid() == groupId)
                .GetProperty("items")
                .EnumerateArray(),
        ];
    }

    // The item as its row holds it in A, through the app's context under A's tenant.
    private async Task<CommonItem> StoredAsync(Guid itemId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.CommonItems.AsNoTracking()
                .SingleAsync(item => item.Id == itemId, TestContext.Current.CancellationToken);
        }
    }

    // The types of the events written about the item, oldest first, read in SQL, with no tenant needed.
    private async Task<List<string>> EventTypesAsync(Guid itemId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>(
                $"SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {itemId} ORDER BY [Id]")
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
