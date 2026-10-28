using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.CommonItems;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-item-groups/{groupId}/items/order</c> (design.md §6.6, S01.03.03), called with the
/// test-only sign-in (<see cref="TestSignIn"/>): a manager sends the IDs of a group's active items in a new order, and
/// gets <c>204</c> with the items rewritten as 0 to n-1 and one <c>common_item.reordered</c>; the order already stored
/// answers <c>204</c> and writes nothing (D84); IDs that are not the group's active items, each once, and an item
/// changed after the list was read, <c>422 config.order_mismatch</c>; a body with no IDs, <c>422 validation.failed</c>;
/// and a group that is unknown, or another organisation's, <c>404</c>; each refusal changing nothing and writing no
/// event. Each test reorders the items of a group of its own in A, and changes no seeded row (tests/README.md). The
/// endpoint matrix covers the statuses of every caller, and <see cref="Platform.Audit.AuditEventsTests"/> the fields
/// of the <c>common_item.reordered</c> that it writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ReorderCommonItemsEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateTime ArchivedAtUtc = TestCommonItems.AddedAtUtc.AddDays(1);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_new_order_answers_204_and_rewrites_the_active_items_as_0_to_n_1_with_one_event()
    {
        // With a gap and a tie, as archiving and two adds at once leave them, which the list reads in CreatedAtUtc
        // order, so that the old order the event records is the list's; and an archived item, which keeps its place.
        // The tied items' IDs are the other way round in SQL Server's order, which compares a uniqueidentifier by its
        // last six bytes first, so only the CreatedAtUtc step reads the bus first.
        var groupId = await AddGroupAsync();
        var bus = Guid.Parse("3e7a1c5d-9b2f-4d6e-8a4c-ffffffffffff");
        var lunch = Guid.Parse("7c2e9a4b-5d1f-4b8a-9e3c-000000000001");
        var items = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            groupId,
            new("Paid for a ticket at the counter", 0),
            new("Packed a day bag", 1, ArchivedAtUtc: ArchivedAtUtc),
            new("Travelled by bus or train", 3, Id: bus),
            new("Bought lunch", 3, TestCommonItems.AddedAtUtc.AddSeconds(1), lunch));
        var (paid, bag) = (items[0], items[1]);
        var bagBefore = Assert.Single(await StoredItemsAsync(item => item.Id == bag));
        var otherGroupId = await AddGroupAsync();
        await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, otherGroupId, new("Showered", 0), new("Dressed", 1));
        var otherBefore = await StoredItemsAsync(item => item.GroupId == otherGroupId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, groupId, new { commonItemIds = new[] { lunch, paid, bus } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal([1, 1, 2, 0], await SortOrdersAsync(items));
        Assert.Equal(bagBefore, Assert.Single(await StoredItemsAsync(item => item.Id == bag)));
        Assert.Equal(otherBefore, await StoredItemsAsync(item => item.GroupId == otherGroupId));
        var written = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(
            (AuditEventTypes.CommonItem.Reordered, AuditEntityTypes.CommonItem, (Guid?)null, (Guid?)null),
            (written.EventType, written.EntityType, written.EntityId, written.ParticipantId));
        var details = Details(old: [paid, bus, lunch], @new: [lunch, paid, bus], groupId);
        Assert.True(
            JsonNode.DeepEquals(details, JsonNode.Parse(written.Details ?? "null")),
            $"Details: expected {details?.ToJsonString()}, written {written.Details}.");
    }

    // D84: the list finds nothing to change, even with the gap and the tie, so the save writes no row and no event. The
    // tied items' IDs are the other way round in SQL Server's order, as in the test above, so only the CreatedAtUtc
    // step reads the order sent as the one stored.
    [Fact]
    public async Task The_order_already_stored_answers_204_and_writes_nothing()
    {
        var groupId = await AddGroupAsync();
        var bus = Guid.Parse("9a4c2e6b-1d3f-4e5a-8b7c-ffffffffffff");
        var lunch = Guid.Parse("b5d7f9a1-3c2e-4a6b-9d8e-000000000001");
        var items = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            groupId,
            new("Paid for a ticket at the counter", 0),
            new("Travelled by bus or train", 2, Id: bus),
            new("Bought lunch", 2, TestCommonItems.AddedAtUtc.AddSeconds(1), lunch));
        var before = await StoredItemsAsync(item => item.GroupId == groupId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(Caller.ManagerOfA, groupId, new { commonItemIds = items });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before, await StoredItemsAsync(item => item.GroupId == groupId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // ListOrder.Reorder is given the group's active items alone, so an archived one is refused as one it was not
    // given, and another group's item as an unknown one (S01.03.03's Notes).
    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("archived")]
    [InlineData("repeated")]
    public async Task An_order_that_is_not_the_active_items_each_once_answers_422_and_changes_nothing(string mismatch)
    {
        var groupId = await AddGroupAsync();
        var items = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            groupId,
            new("Paid for a ticket at the counter", 0),
            new("Travelled by bus or train", 1),
            new("Packed a day bag", 2, ArchivedAtUtc: ArchivedAtUtc));
        var (paid, bus, bag) = (items[0], items[1], items[2]);
        var otherGroupId = await AddGroupAsync();
        var othersItem = Assert.Single(await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, otherGroupId, new TestCommonItem("Showered", 0)));
        Guid[] sent = mismatch switch
        {
            "missing" => [bus],
            "extra" => [bus, paid, othersItem],
            "archived" => [bus, paid, bag],
            "repeated" => [bus, paid, bus],
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null),
        };
        var before = await StoredItemsAsync(item => item.GroupId == groupId);
        var othersBefore = await StoredItemsAsync(item => item.GroupId == otherGroupId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(Caller.ManagerOfA, groupId, new { commonItemIds = sent });

        await AssertProblemAsync(response, ErrorCode.OrderMismatch);
        Assert.Equal(before, await StoredItemsAsync(item => item.GroupId == groupId));
        Assert.Equal(othersBefore, await StoredItemsAsync(item => item.GroupId == otherGroupId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // The race that the set of IDs cannot catch, as ConfigurationChangesOnSqlServerTests has it: the first scope's
    // context already holds the items as they were before the second scope's reword, so the handler's read there gives
    // those rows, which EF Core does not overwrite with the stored ones, and the set of IDs passes as if read before
    // that reword. The save of the moved item that was reworded then matches no row, so nothing of the reorder is
    // written: the screen reads the list again, as for any mismatch, so it is the same 422, never 412 or 500.
    [Fact]
    public async Task An_item_another_scope_changed_after_this_one_read_the_list_gives_OrderMismatch_writing_nothing()
    {
        var groupId = await AddGroupAsync();
        var paid = await factory.Services.AddItemAsync(
            seeded.A.OrganisationId, groupId, new("Paid for a ticket at the counter", 0));
        var others = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new("Travelled by bus or train", 1), new("Bought lunch", 2));
        var (bus, lunch) = (others[0], others[1]);
        var lastEventId = await LastEventIdAsync();
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        using (first.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            await first.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().CommonItems
                .Where(item => item.GroupId == groupId)
                .ToListAsync(TestContext.Current.CancellationToken);
        }

        using (second.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            var reworded = await second.ServiceProvider.GetRequiredService<CommonItemChanges>().UpdateAsync(
                Manager,
                paid.Id,
                paid.ETag,
                new("Paid at the machine", groupId),
                TestContext.Current.CancellationToken);
            Assert.Null(reworded.Error);
            Assert.True(reworded.Changed);
        }

        var afterSecond = await StoredItemsAsync(item => item.GroupId == groupId);

        CommonItemChangeResult result;
        using (first.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            result = await first.ServiceProvider.GetRequiredService<CommonItemChanges>().ReorderAsync(
                Manager, groupId, [bus, paid.Id, lunch], TestContext.Current.CancellationToken);
        }

        Assert.Same(ErrorCode.OrderMismatch, result.Error);
        Assert.Equal(afterSecond, await StoredItemsAsync(item => item.GroupId == groupId));
        Assert.Equal(
            [AuditEventTypes.CommonItem.Updated],
            (await EventsAfterAsync(lastEventId)).Select(written => written.EventType));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"commonItemIds":null}""")]
    public async Task A_body_with_no_commonItemIds_or_a_null_one_answers_422_on_commonItemIds_and_changes_nothing(
        string body)
    {
        var groupId = await AddGroupAsync();
        await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new("Paid for a ticket at the counter", 0), new("Bought lunch", 1));
        var before = await StoredItemsAsync(item => item.GroupId == groupId);
        var lastEventId = await LastEventIdAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var request = new HttpRequestMessage(HttpMethod.Put, OrderPath(groupId))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            "commonItemIds", Assert.Single(problem.RootElement.GetProperty("errors").EnumerateObject()).Name);
        Assert.Equal(before, await StoredItemsAsync(item => item.GroupId == groupId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // design.md §6.6 gives the reorder no config.group_archived, and the screen shows no archived group's items (T06's
    // Notes).
    [Fact]
    public async Task An_archived_group_s_items_are_reordered()
    {
        var groupId = Assert.Single(await factory.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Overnight respite", 0, ArchivedAtUtc: ArchivedAtUtc)));
        var items = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new("Packed an overnight bag", 0), new("Took medication", 1));

        using var response = await PutAsync(
            Caller.ManagerOfA, groupId, new { commonItemIds = new[] { items[1], items[0] } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([1, 0], await SortOrdersAsync(items));
    }

    // B's manager, sending the items' own IDs, as one who had them would: the tenant filter finds no such group in B,
    // so no item is read (design.md §9.2).
    [Fact]
    public async Task Another_organisation_s_group_is_a_404_problem_and_nothing_is_changed()
    {
        var groupId = await AddGroupAsync();
        var items = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new("Paid for a ticket at the counter", 0), new("Bought lunch", 1));
        var before = await StoredItemsAsync(item => item.GroupId == groupId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfB, groupId, new { commonItemIds = new[] { items[1], items[0] } });

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredItemsAsync(item => item.GroupId == groupId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    [Fact]
    public async Task A_group_of_no_organisation_is_a_404_problem()
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, Guid.NewGuid(), new { commonItemIds = Array.Empty<Guid>() });

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PutWithTokenAsync(
            "/api/admin/common-item-groups/community-outing/items/order",
            await client.FetchTokenAsync(),
            new { commonItemIds = Array.Empty<Guid>() },
            ifMatch: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise reorder the items.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_changed()
    {
        var groupId = await AddGroupAsync();
        var items = await factory.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new("Paid for a ticket at the counter", 0), new("Bought lunch", 1));
        var before = await StoredItemsAsync(item => item.GroupId == groupId);

        using var response = await PutAsync(
            Caller.WorkerOfA, groupId, new { commonItemIds = new[] { items[1], items[0] } });

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(before, await StoredItemsAsync(item => item.GroupId == groupId));
    }

    /// <summary>A's manager, as the handler is given them in the app.</summary>
    private Actor Manager => new(seeded.A.ManagerId, Channel.App);

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // The Details of a common_item.reordered, the old and new order of the group's active items' IDs (design.md §6.6),
    // with the group's ID beside them, as the JSON stored has them, so that each ID is compared as the text written.
    private static JsonNode? Details(Guid[] old, Guid[] @new, Guid groupId) => JsonNode.Parse(new JsonObject
    {
        ["old"] = new JsonObject { ["order"] = new JsonArray([.. old.Select(id => JsonValue.Create(id))]) },
        ["new"] = new JsonObject { ["order"] = new JsonArray([.. @new.Select(id => JsonValue.Create(id))]) },
        ["groupId"] = groupId,
    }.ToJsonString());

    private static string OrderPath(Guid groupId) => $"/api/admin/common-item-groups/{groupId}/items/order";

    // An active group added to A for the test, so that its items are the test's alone.
    private async Task<Guid> AddGroupAsync() =>
        Assert.Single(await factory.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));

    // As the SPA reorders: over HTTPS, with a token fetched as the caller, and no If-Match.
    private async Task<HttpResponseMessage> PutAsync(Caller caller, Guid groupId, object body)
    {
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PutWithTokenAsync(OrderPath(groupId), await client.FetchTokenAsync(), body, ifMatch: null);
    }

    // Each matching item, by ID, so that a change to any of its values shows.
    private async Task<List<StoredItem>> StoredItemsAsync(Expression<Func<CommonItem, bool>> predicate) =>
    [
        .. (await ItemsAsync(predicate))
            .OrderBy(item => item.Id)
            .Select(item => new StoredItem(
                item.Id, item.GroupId, item.SortOrder, item.Text, item.ArchivedAtUtc, RowVersionToken.ETag(item))),
    ];

    // The SortOrder of each of the items, in the order given.
    private async Task<List<int>> SortOrdersAsync(IReadOnlyList<Guid> itemIds)
    {
        var stored = (await ItemsAsync(item => itemIds.Contains(item.Id))).ToDictionary(item => item.Id);
        return [.. itemIds.Select(id => stored[id].SortOrder)];
    }

    // The items as their rows hold them in A, through the app's context under A's tenant.
    private async Task<List<CommonItem>> ItemsAsync(Expression<Func<CommonItem, bool>> predicate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.CommonItems.AsNoTracking().Where(predicate)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // The ID of the last event written, by any test. The tests of the collection run one at a time, so the events after
    // it are the test's alone; a reorder names no item as its entity and no participant, so its events are found so.
    private async Task<long> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT COALESCE(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // The events written after the one of ID eventId, oldest first, read in SQL, with no tenant needed.
    private async Task<List<StoredEvent>> EventsAfterAsync(long eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.SqlQuery<StoredEvent>($"""
            SELECT [EventType], [EntityType], [EntityId], [ParticipantId], [Details]
            FROM [AuditEvent]
            WHERE [Id] > {eventId}
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An item as its row holds it: its group, place, text, time archived and version.</summary>
    private sealed record StoredItem(
        Guid Id, Guid GroupId, int SortOrder, string Text, DateTime? ArchivedAtUtc, string ETag);

    private sealed record StoredEvent(
        string EventType, string? EntityType, Guid? EntityId, Guid? ParticipantId, string? Details);
}
