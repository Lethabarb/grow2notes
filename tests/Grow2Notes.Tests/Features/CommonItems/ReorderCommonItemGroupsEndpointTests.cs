using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.CommonItems;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-item-groups/order</c> (design.md §6.6, S01.03.06), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): a manager sends the IDs of their organisation's active groups other than Every note in a
/// new order, and gets <c>204</c> with those groups rewritten as 0 to n-1 and one <c>common_item_group.reordered</c>,
/// Every note still first and its <c>SortOrder</c> as it was (A43); the order already stored answers <c>204</c> and
/// writes nothing (D84); IDs that are not those groups, each once, Every note's among them, and a group changed after
/// the list was read, <c>422 config.order_mismatch</c>; and a body with no IDs, <c>422 validation.failed</c>; each
/// refusal changing nothing and writing no event. The order is of every active group of an organisation, so each test
/// reorders the groups of an organisation of its own, which it adds with a manager and its Every note, and changes no
/// seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the fields of the <c>common_item_group.reordered</c> that it writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ReorderCommonItemGroupsEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/admin/common-item-groups/order";

    private static readonly DateTime AddedAtUtc = TestCommonItems.AddedAtUtc;

    private static readonly DateTime ArchivedAtUtc = AddedAtUtc.AddDays(1);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_new_order_answers_204_and_rewrites_the_active_groups_as_0_to_n_1_with_one_event()
    {
        // With a gap and a tie, as archiving and two adds at once leave them, which the list reads in CreatedAtUtc
        // order, so that the old order the event records is the list's; an archived group, which keeps its place; and
        // Every note at 0, as admin bootstrap leaves it, which no reorder moves. The tied groups' IDs are the other way
        // round in SQL Server's order, which compares a uniqueidentifier by its last six bytes first, so only the
        // CreatedAtUtc step reads the outing first.
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        var everyNoteId = await AddEveryNoteAsync(organisationId);
        var outing = Guid.Parse("4c8e2a6d-1f3b-4a5c-9d7e-ffffffffffff");
        var shopping = Guid.Parse("8d3f1b7e-2a4c-4e6d-8f9a-000000000001");
        var groups = await factory.Services.AddGroupsAsync(
            organisationId,
            new("Personal care", 0),
            new("Overnight respite", 1, ArchivedAtUtc: ArchivedAtUtc),
            new("Community outing", 3, Id: outing),
            new("Shopping", 3, AddedAtUtc.AddSeconds(1), shopping));
        var (care, respite) = (groups[0], groups[1]);
        var before = await StoredGroupsAsync(organisationId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(managerId, new { groupIds = new[] { shopping, care, outing } });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredGroupsAsync(organisationId);
        Assert.Equal([0, 1, 2], SortOrders(stored, shopping, care, outing));
        Assert.Equal(
            before.Where(group => group.Id == everyNoteId || group.Id == respite),
            stored.Where(group => group.Id == everyNoteId || group.Id == respite));
        Assert.Equal([everyNoteId, shopping, care, outing], await ListedIdsAsync(managerId));
        var written = Assert.Single(await EventsAfterAsync(lastEventId));
        Assert.Equal(
            (AuditEventTypes.CommonItemGroup.Reordered, organisationId, (Guid?)managerId),
            (written.EventType, written.OrganisationId, written.ActorUserId));
        Assert.Equal(
            (AuditEntityTypes.CommonItemGroup, (Guid?)null, (Guid?)null),
            (written.EntityType, written.EntityId, written.ParticipantId));
        var details = Details(old: [care, outing, shopping], @new: [shopping, care, outing]);
        Assert.True(
            JsonNode.DeepEquals(details, JsonNode.Parse(written.Details ?? "null")),
            $"Details: expected {details?.ToJsonString()}, written {written.Details}.");
    }

    // D84: the list finds nothing to change, even with the gap and the tie, so the save writes no row and no event. The
    // tied groups' IDs are the other way round in SQL Server's order, as in the test above, so only the CreatedAtUtc
    // step reads the order sent as the one stored.
    [Fact]
    public async Task The_order_already_stored_answers_204_and_writes_nothing()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        await AddEveryNoteAsync(organisationId);
        var outing = Guid.Parse("b2d4f6a8-3c5e-4f7a-9b1d-ffffffffffff");
        var shopping = Guid.Parse("c3e5a7b9-4d6f-4a8b-8c2e-000000000001");
        var groups = await factory.Services.AddGroupsAsync(
            organisationId,
            new("Personal care", 0),
            new("Community outing", 2, Id: outing),
            new("Shopping", 2, AddedAtUtc.AddSeconds(1), shopping));
        var before = await StoredGroupsAsync(organisationId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(managerId, new { groupIds = groups });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before, await StoredGroupsAsync(organisationId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // ListOrder.Reorder is given the active groups other than Every note alone, so Every note's ID and an archived
    // group's are refused as ones it was not given, and another organisation's group as an unknown one (S01.03.06's
    // Notes): no request can move a group above Every note.
    [Theory]
    [InlineData("Every note")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("archived")]
    [InlineData("repeated")]
    public async Task An_order_that_is_not_the_active_groups_but_Every_note_each_once_answers_422_and_changes_nothing(
        string mismatch)
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        var everyNoteId = await AddEveryNoteAsync(organisationId);
        var groups = await factory.Services.AddGroupsAsync(
            organisationId,
            new("Community outing", 0),
            new("Personal care", 1),
            new("Overnight respite", 2, ArchivedAtUtc: ArchivedAtUtc));
        var (outing, care, respite) = (groups[0], groups[1], groups[2]);
        var othersGroup = Assert.Single(await factory.Services.AddGroupsAsync(
            seeded.B.OrganisationId, new TestCommonItemGroup("Shopping", 0)));
        Guid[] sent = mismatch switch
        {
            "Every note" => [everyNoteId, care, outing],
            "missing" => [care],
            "extra" => [care, outing, othersGroup],
            "archived" => [care, outing, respite],
            "repeated" => [care, outing, care],
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null),
        };
        var before = await StoredGroupsAsync(organisationId);
        var othersBefore = await StoredGroupsAsync(seeded.B.OrganisationId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(managerId, new { groupIds = sent });

        await AssertProblemAsync(response, ErrorCode.OrderMismatch);
        Assert.Equal(before, await StoredGroupsAsync(organisationId));
        Assert.Equal(othersBefore, await StoredGroupsAsync(seeded.B.OrganisationId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // The race that the set of IDs cannot catch, as ConfigurationChangesOnSqlServerTests has it: the first scope's
    // context already holds the groups as they were before the second scope's rename, so the handler's read there gives
    // those rows, which EF Core does not overwrite with the stored ones, and the set of IDs passes as if read before
    // that rename. The save of the moved group that was renamed then matches no row, so nothing of the reorder is
    // written: the screen reads the list again, as for any mismatch, so it is the same 422, never 412 or 500.
    [Fact]
    public async Task A_group_another_scope_changed_after_this_one_read_the_list_gives_OrderMismatch_writing_nothing()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        await AddEveryNoteAsync(organisationId);
        var outing = await factory.Services.AddGroupAsync(organisationId, new("Community outing", 0));
        var others = await factory.Services.AddGroupsAsync(organisationId, new("Personal care", 1), new("Shopping", 2));
        var (care, shopping) = (others[0], others[1]);
        var manager = new Actor(managerId, Channel.App);
        var lastEventId = await LastEventIdAsync();
        await using var first = factory.Services.CreateAsyncScope();
        await using var second = factory.Services.CreateAsyncScope();
        using (first.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await first.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().CommonItemGroups
                .ToListAsync(TestContext.Current.CancellationToken);
        }

        using (second.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            var renamed = await second.ServiceProvider.GetRequiredService<CommonItemGroupChanges>().RenameAsync(
                manager, outing.Id, outing.ETag, new("Outings"), TestContext.Current.CancellationToken);
            Assert.Same(CommonItemChangeResult.Saved, renamed);
        }

        var afterSecond = await StoredGroupsAsync(organisationId);

        CommonItemChangeResult result;
        using (first.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            result = await first.ServiceProvider.GetRequiredService<CommonItemGroupChanges>().ReorderAsync(
                manager, [care, outing.Id, shopping], TestContext.Current.CancellationToken);
        }

        Assert.Same(ErrorCode.OrderMismatch, result.Error);
        Assert.Equal(afterSecond, await StoredGroupsAsync(organisationId));
        Assert.Equal(
            [AuditEventTypes.CommonItemGroup.Updated],
            (await EventsAfterAsync(lastEventId)).Select(written => written.EventType));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"groupIds":null}""")]
    public async Task A_body_with_no_groupIds_or_a_null_one_answers_422_on_groupIds_and_changes_nothing(string body)
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        await AddEveryNoteAsync(organisationId);
        await factory.Services.AddGroupsAsync(organisationId, new("Community outing", 0), new("Personal care", 1));
        var before = await StoredGroupsAsync(organisationId);
        var lastEventId = await LastEventIdAsync();
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        using var request = new HttpRequestMessage(HttpMethod.Put, Path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal("groupIds", Assert.Single(problem.RootElement.GetProperty("errors").EnumerateObject()).Name);
        Assert.Equal(before, await StoredGroupsAsync(organisationId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // B's manager, sending the groups' own IDs, as one who had them would: the order is checked against B's own active
    // groups, which those are not, so no group of the other organisation is read or written (design.md §9.2).
    [Fact]
    public async Task Another_organisation_s_groups_are_an_order_mismatch_and_nothing_is_changed()
    {
        var (organisationId, _) = await AddOrganisationWithAManagerAsync();
        await AddEveryNoteAsync(organisationId);
        var groups = await factory.Services.AddGroupsAsync(
            organisationId, new("Community outing", 0), new("Personal care", 1));
        var before = await StoredGroupsAsync(organisationId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(seeded.B.ManagerId, new { groupIds = new[] { groups[1], groups[0] } });

        await AssertProblemAsync(response, ErrorCode.OrderMismatch);
        Assert.Equal(before, await StoredGroupsAsync(organisationId));
        Assert.Empty(await EventsAfterAsync(lastEventId));
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise reorder A's groups, as
    // the worker sends them the other way round.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_changed()
    {
        await factory.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0), new("Personal care", 1));
        var groupIds = await factory.Services.ActiveGroupIdsAsync(seeded.A.OrganisationId);
        groupIds.Reverse();
        var before = await StoredGroupsAsync(seeded.A.OrganisationId);

        using var response = await PutAsync(seeded.A.WorkerId, new { groupIds });

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(before, await StoredGroupsAsync(seeded.A.OrganisationId));
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // The Details of a common_item_group.reordered, the old and new order of the active groups' IDs other than Every
    // note's (design.md §6.6), as the JSON stored has them, so that each ID is compared as the text written.
    private static JsonNode? Details(Guid[] old, Guid[] @new) => JsonNode.Parse(new JsonObject
    {
        ["old"] = new JsonObject { ["order"] = new JsonArray([.. old.Select(id => JsonValue.Create(id))]) },
        ["new"] = new JsonObject { ["order"] = new JsonArray([.. @new.Select(id => JsonValue.Create(id))]) },
    }.ToJsonString());

    // The SortOrder of each of the groups, in the order given.
    private static List<int> SortOrders(List<StoredGroup> stored, params Guid[] groupIds) =>
        [.. groupIds.Select(id => Assert.Single(stored, group => group.Id == id).SortOrder)];

    // As the SPA reorders: over HTTPS, with a token fetched as the caller, and no If-Match.
    private async Task<HttpResponseMessage> PutAsync(Guid userId, object body)
    {
        using var client = factory.CreateHttpsClient().SignInAs(userId);
        return await client.PutWithTokenAsync(Path, await client.FetchTokenAsync(), body, ifMatch: null);
    }

    // The IDs of the groups that the list gives the manager, in its order.
    private async Task<List<Guid>> ListedIdsAsync(Guid managerId)
    {
        using var client = factory.CreateHttpsClient().SignInAs(managerId);
        var listed = await client.GetFromJsonAsync<JsonElement>(
            "/api/admin/common-item-groups", TestContext.Current.CancellationToken);
        return [.. listed.EnumerateArray().Select(group => group.GetProperty("id").GetGuid())];
    }

    // An organisation of the test's own, with no group yet, and an Active manager, who needs no passkey or password to
    // sign in with the test-only sign-in. An organisation is the tenant, not tenant-owned, and AspNetUsers is not
    // filtered, so neither save needs a tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationWithAManagerAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Reorder groups test", CreatedAtUtc = AddedAtUtc };
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

    // The organisation's Every note group, made as admin bootstrap makes it, at SortOrder 0.
    private async Task<Guid> AddEveryNoteAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var everyNote = CommonItemGroup.EveryNote(organisationId, AddedAtUtc);
        db.CommonItemGroups.Add(everyNote);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return everyNote.Id;
    }

    // Each of the organisation's groups, Every note and the archived ones among them, as its row holds it, by ID, so
    // that a change to any of its values shows; through the app's context under the organisation's tenant.
    private async Task<List<StoredGroup>> StoredGroupsAsync(Guid organisationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            return
            [
                .. (await db.CommonItemGroups.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken))
                    .OrderBy(group => group.Id)
                    .Select(group => new StoredGroup(
                        group.Id, group.Name, group.SortOrder, group.ArchivedAtUtc, RowVersionToken.ETag(group))),
            ];
        }
    }

    // The ID of the last event written, by any test. The tests of the collection run one at a time, so the events after
    // it are the test's alone; a reorder names no group as its entity and no participant, so its events are found so.
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
            SELECT [EventType], [OrganisationId], [ActorUserId], [EntityType], [EntityId], [ParticipantId], [Details]
            FROM [AuditEvent]
            WHERE [Id] > {eventId}
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A group as its row holds it: its name, place, time archived and version.</summary>
    private sealed record StoredGroup(Guid Id, string Name, int SortOrder, DateTime? ArchivedAtUtc, string ETag);

    private sealed record StoredEvent(
        string EventType,
        Guid OrganisationId,
        Guid? ActorUserId,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);
}
