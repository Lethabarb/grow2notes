using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-item-groups/{groupId}/archive</c> and <c>/restore</c> (design.md §3.7, §6.6, S01.03.07),
/// called with the test-only sign-in (<see cref="TestSignIn"/>) on a fake clock: a manager archives a group of their
/// own organisation, which keeps its <c>SortOrder</c> and leaves its items as they were, or restores one, which goes to
/// the end of the active groups other than Every note with its active items, with the group's <c>ETag</c> as
/// <c>If-Match</c>, and gets <c>204</c>; a group that is unknown, or another organisation's, gets <c>404</c>, then,
/// with no <c>If-Match</c>, <c>428</c>, with a stale one <c>412</c>, then Every note
/// <c>422 config.every_note_fixed</c> (A43); each of those changing nothing and writing no event. Archiving an
/// archived group, or restoring an active one, answers <c>204</c> too, and saves nothing (D84). Once archived, the
/// group takes no item add and no reword of its items (<c>409 config.group_archived</c>). Each test acts on a group
/// that it adds to A, or, where the group's place depends on every group of an organisation, to an organisation of its
/// own, but for Every note, which a refusal leaves as it was, and changes no seeded row (tests/README.md). The endpoint
/// matrix covers the statuses of every caller, and <see cref="Platform.Audit.AuditEventsTests"/> the events that they
/// write.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ArchiveCommonItemGroupEndpointsTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Archive = "archive";
    private const string Restore = "restore";

    private const string StoredName = "Community outing";

    private static readonly DateTime AddedAtUtc = TestCommonItems.AddedAtUtc;

    private static readonly DateTime ArchivedAtUtc = AddedAtUtc.AddDays(1);

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public ArchiveCommonItemGroupEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    // Each action, on a group it changes: an active one to archive, an archived one to restore.
    public static TheoryData<string> Actions { get; } = new(Archive, Restore);

    [Fact]
    public async Task Archive_answers_204_and_archives_it_at_the_clocks_time_in_its_place_with_a_new_version()
    {
        var group = await AddGroupAsync(new(StoredName, 2));

        using var response = await PostAsync(Caller.ManagerOfA, group.Id, Archive, group.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(seeded.A.OrganisationId, group.Id);
        Assert.Equal(
            (StoredName, 2, (DateTime?)TestClock.Start.UtcDateTime, false),
            (stored.Name, stored.SortOrder, stored.ArchivedAtUtc, stored.IsEveryNote));
        Assert.NotEqual(group.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.CommonItemGroup.Archived], await EventTypesAsync(group.Id));
    }

    // design.md §5.3: an item's own state is its own, so archiving its group touches no item row, and no If-Match of an
    // item could catch the group's archive (§6.9), which is why an item write checks the group itself.
    [Fact]
    public async Task Archiving_a_group_leaves_each_of_its_items_archived_state_and_version_as_they_were()
    {
        var group = await AddGroupAsync(new(StoredName, 0));
        var active = await app.Services.AddItemAsync(
            seeded.A.OrganisationId, group.Id, new TestCommonItem("Travelled by bus or train", 0));
        var archived = await app.Services.AddItemAsync(
            seeded.A.OrganisationId, group.Id, new TestCommonItem("Packed a day bag", 1, ArchivedAtUtc: ArchivedAtUtc));

        using var response = await PostAsync(Caller.ManagerOfA, group.Id, Archive, group.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            [(active.Id, (DateTime?)null, active.ETag), (archived.Id, ArchivedAtUtc, archived.ETag)],
            await StoredItemsAsync(group.Id));
        Assert.Empty(await EventTypesAsync(active.Id));
        Assert.Empty(await EventTypesAsync(archived.Id));
    }

    // A2: one more than the highest active SortOrder of the groups other than Every note, whose own SortOrder counts
    // for nothing, past the gap that an archive left, and whatever SortOrder the archived groups hold, its own
    // included, as only a reorder closes gaps. The list then gives it last, without includeArchived, with its active
    // items alone, each as it was.
    [Fact]
    public async Task Restore_answers_204_and_puts_it_last_after_the_highest_active_group_but_Every_note()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        var everyNoteId = await AddEveryNoteAsync(organisationId, sortOrder: 9);
        var others = await app.Services.AddGroupsAsync(
            organisationId,
            new("Personal care", 0),
            new("Shopping", 3),
            new("Overnight respite", 7, ArchivedAtUtc: ArchivedAtUtc));
        var (care, shopping) = (others[0], others[1]);
        var group = await app.Services.AddGroupAsync(organisationId, new(StoredName, 1, ArchivedAtUtc: ArchivedAtUtc));
        var items = await app.Services.AddItemsAsync(
            organisationId,
            group.Id,
            new("Travelled by bus or train", 0),
            new("Packed a day bag", 1, ArchivedAtUtc: ArchivedAtUtc),
            new("Bought lunch", 2));
        var itemsBefore = await StoredItemsAsync(group.Id, organisationId);

        using var response = await PostAsync(managerId, group.Id, Restore, group.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(organisationId, group.Id);
        Assert.Equal((StoredName, 4, (DateTime?)null), (stored.Name, stored.SortOrder, stored.ArchivedAtUtc));
        Assert.NotEqual(group.ETag, RowVersionToken.ETag(stored));
        AssertDetails(
            new JsonObject
            {
                ["old"] = new JsonObject { ["sortOrder"] = 1, ["archivedAtUtc"] = ArchivedAtUtc },
                ["new"] = new JsonObject { ["sortOrder"] = 4, ["archivedAtUtc"] = null },
            },
            Assert.Single(await EventsAsync(group.Id)));
        Assert.Equal(itemsBefore, await StoredItemsAsync(group.Id, organisationId));
        var listed = await ListedAsync(managerId);
        Assert.Equal([everyNoteId, care, shopping, group.Id], listed.Select(Id));
        Assert.Equal(
            [(items[0], "Travelled by bus or train"), (items[2], "Bought lunch")],
            listed[^1].GetProperty("items").EnumerateArray()
                .Select(item => (Id(item), item.GetProperty("text").GetString())));
    }

    // With no other active group, the end is 0, whatever Every note's own SortOrder, as bootstrap leaves it at 0, and
    // the list still gives Every note first (design.md §5.3).
    [Fact]
    public async Task A_group_restored_with_no_other_active_group_is_at_0_and_listed_after_Every_note()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        var everyNoteId = await AddEveryNoteAsync(organisationId, sortOrder: 0);
        var group = await app.Services.AddGroupAsync(organisationId, new(StoredName, 5, ArchivedAtUtc: ArchivedAtUtc));

        using var response = await PostAsync(managerId, group.Id, Restore, group.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await StoredAsync(organisationId, group.Id)).SortOrder);
        Assert.Equal([everyNoteId, group.Id], (await ListedAsync(managerId)).Select(Id));
    }

    // T07's Notes: a restore records its sortOrder only when the group's old SortOrder was not already the end.
    [Fact]
    public async Task A_restore_to_the_place_it_held_records_the_time_alone()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        await AddEveryNoteAsync(organisationId, sortOrder: 0);
        await app.Services.AddGroupsAsync(organisationId, new TestCommonItemGroup("Personal care", 0));
        var group = await app.Services.AddGroupAsync(organisationId, new(StoredName, 1, ArchivedAtUtc: ArchivedAtUtc));

        using var response = await PostAsync(managerId, group.Id, Restore, group.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, (await StoredAsync(organisationId, group.Id)).SortOrder);
        AssertDetails(
            new JsonObject
            {
                ["old"] = new JsonObject { ["archivedAtUtc"] = ArchivedAtUtc },
                ["new"] = new JsonObject { ["archivedAtUtc"] = null },
            },
            Assert.Single(await EventsAsync(group.Id)));
    }

    // D84: a repeat finds nothing to change, so an archived group keeps the time it was archived at, and an active one
    // its place.
    [Theory]
    [InlineData(Archive, true)]
    [InlineData(Restore, false)]
    public async Task A_group_already_so_answers_204_and_nothing_is_saved(string action, bool archived)
    {
        var group = await AddGroupAsync(ToChange(archived));

        using var response = await PostAsync(Caller.ManagerOfA, group.Id, action, group.ETag);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertUnchangedAsync(group, archived);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing(string action)
    {
        var group = await AddGroupAsync(ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.ManagerOfA, group.Id, action, ifMatch: null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(group, archived: action == Restore);
    }

    // As when another manager renamed the group first: the action, sent with the version both loaded, changes nothing.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing(string action)
    {
        var archived = action == Restore;
        var group = await AddGroupAsync(ToChange(archived));
        using (var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded))
        using (var rename = await client.PutWithTokenAsync(
                   $"/api/admin/common-item-groups/{group.Id}",
                   await client.FetchTokenAsync(),
                   new { name = "Outings" },
                   group.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        }

        var renamed = RowVersionToken.ETag(await StoredAsync(seeded.A.OrganisationId, group.Id));

        using var response = await PostAsync(Caller.ManagerOfA, group.Id, action, group.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(seeded.A.OrganisationId, group.Id);
        Assert.Equal(
            ("Outings", 2, archived ? ArchivedAtUtc : (DateTime?)null, renamed),
            (stored.Name, stored.SortOrder, stored.ArchivedAtUtc, RowVersionToken.ETag(stored)));
        Assert.Equal([AuditEventTypes.CommonItemGroup.Updated], await EventTypesAsync(group.Id));
    }

    // B's manager, sending the group's current ETag, as one who had it would: the tenant filter finds no such group in
    // B, so the version is never checked (design.md §9.2).
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Another_organisation_s_group_is_not_found_and_not_changed(string action)
    {
        var group = await AddGroupAsync(ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.ManagerOfB, group.Id, action, group.ETag);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        await AssertUnchangedAsync(group, archived: action == Restore);
    }

    // B's own Every note is not found by A's manager either, so it is a 404, never the 422 that would say it exists.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Another_organisation_s_Every_note_is_not_found(string action)
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(
            Caller.ManagerOfA, seeded.B.EveryNoteGroupId, action, IfMatch.ETag("AAAAAAAAB9E="));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_no_group_has_is_not_found(string action)
    {
        using var response = await PostAsync(Caller.ManagerOfA, Guid.NewGuid(), action, IfMatch.ETag("AAAAAAAAB9E="));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // The routes take a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task An_ID_that_is_not_a_GUID_is_not_found(string action)
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/community-outing/{action}",
            await client.FetchTokenAsync(),
            ifMatch: IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Every note is looked at after If-Match, and refused for each action, the restore too, though it is never archived
    // (A43, T07's Notes). The screen never offers either, so the screen takes this as something gone wrong.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Every_note_is_a_422_every_note_fixed_problem_and_stays_active(string action)
    {
        var everyNote = await StoredAsync(seeded.A.OrganisationId, seeded.A.EveryNoteGroupId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, everyNote.Id, action, RowVersionToken.ETag(everyNote));

        await AssertProblemAsync(response, ErrorCode.EveryNoteFixed);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var stored = await StoredAsync(seeded.A.OrganisationId, everyNote.Id);
        Assert.Equal(
            ("Every note", true, (DateTime?)null, everyNote.SortOrder, RowVersionToken.ETag(everyNote)),
            (stored.Name, stored.IsEveryNote, stored.ArchivedAtUtc, stored.SortOrder, RowVersionToken.ETag(stored)));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // If-Match is checked before Every note is, so a missing one is refused as such, as for any other group.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task Every_note_with_no_If_Match_is_refused_for_its_If_Match(string action)
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, seeded.A.EveryNoteGroupId, action, ifMatch: null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        Assert.Null((await StoredAsync(seeded.A.OrganisationId, seeded.A.EveryNoteGroupId)).ArchivedAtUtc);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // S01.03.07: the group archived through its endpoint, as a manager archives it, is then refused an item, which the
    // manager adds once the group is restored.
    [Fact]
    public async Task After_an_archive_an_item_added_to_the_group_is_a_409_group_archived_and_nothing_is_written()
    {
        var group = await AddGroupAsync(new(StoredName, 0));
        var item = await app.Services.AddItemAsync(
            seeded.A.OrganisationId, group.Id, new TestCommonItem("Travelled by bus or train", 0));
        await ArchiveThroughTheEndpointAsync(group);
        var itemsBefore = await StoredItemsAsync(group.Id);
        var lastEventId = await LastEventIdAsync();
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/{group.Id}/items",
            await client.FetchTokenAsync(),
            new { text = "Bought lunch" });

        await AssertProblemAsync(response, ErrorCode.GroupArchived);
        Assert.Equal(itemsBefore, await StoredItemsAsync(group.Id));
        Assert.Equal([(item.Id, (DateTime?)null, item.ETag)], itemsBefore);
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The item's version is as it was before the archive, so the reword's If-Match passes, and the group's own state
    // refuses it (design.md §6.9).
    [Fact]
    public async Task After_an_archive_a_reword_of_its_item_is_a_409_group_archived_and_nothing_is_written()
    {
        var group = await AddGroupAsync(new(StoredName, 0));
        var item = await app.Services.AddItemAsync(
            seeded.A.OrganisationId, group.Id, new TestCommonItem("Travelled by bus or train", 0));
        await ArchiveThroughTheEndpointAsync(group);
        var lastEventId = await LastEventIdAsync();
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PutWithTokenAsync(
            $"/api/admin/common-items/{item.Id}",
            await client.FetchTokenAsync(),
            new { text = "Travelled by tram" },
            item.ETag);

        await AssertProblemAsync(response, ErrorCode.GroupArchived);
        var stored = await StoredItemAsync(item.Id);
        Assert.Equal(("Travelled by bus or train", item.ETag), (stored.Text, RowVersionToken.ETag(stored)));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise change the group.
    [Theory]
    [MemberData(nameof(Actions))]
    public async Task A_worker_gets_403_and_nothing_is_changed(string action)
    {
        var group = await AddGroupAsync(ToChange(archived: action == Restore));

        using var response = await PostAsync(Caller.WorkerOfA, group.Id, action, group.ETag);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        await AssertUnchangedAsync(group, archived: action == Restore);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // The Details of an event as the JSON written, against those expected, each value compared as the text written.
    private static void AssertDetails(JsonObject expected, string? written) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected.ToJsonString()), JsonNode.Parse(written ?? "null")),
            $"Details: expected {expected.ToJsonString()}, written {written}.");

    private static Guid Id(JsonElement listed) => listed.GetProperty("id").GetGuid();

    // A group that the action changes: an active one to archive, an archived one to restore, each in a place of its
    // own.
    private static TestCommonItemGroup ToChange(bool archived) =>
        archived ? new(StoredName, 2, ArchivedAtUtc: ArchivedAtUtc) : new(StoredName, 2);

    // The group as AddGroupAsync left it, in its place and at the same version, with no event written about it.
    private async Task AssertUnchangedAsync(AddedGroup group, bool archived)
    {
        var stored = await StoredAsync(seeded.A.OrganisationId, group.Id);
        Assert.Equal(
            (StoredName, 2, archived ? ArchivedAtUtc : (DateTime?)null, group.ETag),
            (stored.Name, stored.SortOrder, stored.ArchivedAtUtc, RowVersionToken.ETag(stored)));
        Assert.Empty(await EventTypesAsync(group.Id));
    }

    // The group archived as A's manager archives it, with its ETag, which answers 204.
    private async Task ArchiveThroughTheEndpointAsync(AddedGroup group)
    {
        using var response = await PostAsync(Caller.ManagerOfA, group.Id, Archive, group.ETag);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull((await StoredAsync(seeded.A.OrganisationId, group.Id)).ArchivedAtUtc);
    }

    // A group, as given, added to A for the test.
    private Task<AddedGroup> AddGroupAsync(TestCommonItemGroup group) =>
        app.Services.AddGroupAsync(seeded.A.OrganisationId, group);

    private async Task<HttpResponseMessage> PostAsync(Caller caller, Guid groupId, string action, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await PostAsync(client, groupId, action, ifMatch);
    }

    private async Task<HttpResponseMessage> PostAsync(Guid userId, Guid groupId, string action, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(userId);
        return await PostAsync(client, groupId, action, ifMatch);
    }

    // As the SPA archives or restores one: over HTTPS, with a token fetched as the caller, and no body.
    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client, Guid groupId, string action, string? ifMatch) =>
        await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}/{action}", await client.FetchTokenAsync(), ifMatch: ifMatch);

    // The groups that the list gives the manager, without includeArchived, as notes and their pickers read them, in
    // its order, each with its items.
    private async Task<List<JsonElement>> ListedAsync(Guid managerId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient().SignInAs(managerId);
        using var response = await client.GetAsync("/api/admin/common-item-groups", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).EnumerateArray()];
    }

    // An organisation of the test's own, with no group yet, and an Active manager, who needs no passkey or password to
    // sign in with the test-only sign-in. An organisation is the tenant, not tenant-owned, and AspNetUsers is not
    // filtered, so neither save needs a tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationWithAManagerAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Archive groups test", CreatedAtUtc = AddedAtUtc };
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
    private async Task<Guid> AddEveryNoteAsync(Guid organisationId, int sortOrder)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var everyNote = CommonItemGroup.EveryNote(organisationId, AddedAtUtc);
        everyNote.SortOrder = sortOrder;
        db.CommonItemGroups.Add(everyNote);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return everyNote.Id;
    }

    // The group as its row holds it, through the app's context under its organisation's tenant.
    private async Task<CommonItemGroup> StoredAsync(Guid organisationId, Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            return await db.CommonItemGroups.AsNoTracking()
                .SingleAsync(group => group.Id == groupId, TestContext.Current.CancellationToken);
        }
    }

    // The item as its row holds it in A, through the app's context under A's tenant.
    private async Task<CommonItem> StoredItemAsync(Guid itemId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.CommonItems.AsNoTracking()
                .SingleAsync(item => item.Id == itemId, TestContext.Current.CancellationToken);
        }
    }

    // Each item of the group, by its SortOrder, as its row holds it: its ID, the time it was archived at and its ETag;
    // in A unless an organisation is given.
    private async Task<List<(Guid Id, DateTime? ArchivedAtUtc, string ETag)>> StoredItemsAsync(
        Guid groupId, Guid? organisationId = null)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (tenant.Use(organisationId ?? seeded.A.OrganisationId))
        {
            var items = await db.CommonItems.AsNoTracking()
                .Where(item => item.GroupId == groupId)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(TestContext.Current.CancellationToken);
            return [.. items.Select(item => (item.Id, item.ArchivedAtUtc, RowVersionToken.ETag(item)))];
        }
    }

    // The types of the events written about the group or item, oldest first, read in SQL, with no tenant needed.
    private async Task<List<string>> EventTypesAsync(Guid entityId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>(
                $"SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {entityId} ORDER BY [Id]")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The Details of the events written about the group, oldest first, read in SQL, with no tenant needed.
    private async Task<List<string?>> EventsAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string?>(
                $"SELECT [Details] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {groupId} ORDER BY [Id]")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The tests of the collection run one at a time, so none writes an event while a test compares this before and
    // after its request.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
