using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-item-groups</c> (design.md §6.6, S01.03.05), called with the test-only sign-in
/// (<see cref="TestSignIn"/>) on a fake clock: a manager adds a group, with no items, at the end of their own
/// organisation's active groups other than Every note (A2), its name on one line with its ends trimmed, and gets
/// <c>201</c> with the new group's ID and no <c>Location</c>; a name that is missing, or empty or too long once
/// cleaned, gets <c>422 validation.failed</c> in the screen's words, adding nothing and writing no event; and an
/// <c>isEveryNote</c> in the body is never read, so no add makes a second Every note (S01.03.01, design.md §14 M1). The
/// tests add their groups to seeded organisation A, or to an organisation of their own where a test must know every
/// group, and change no seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the <c>common_item_group.created</c> that an add writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AddCommonItemGroupEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string Path = "/api/admin/common-item-groups";

    private const string EnterTheGroupName = "Enter the group name";

    private const string TooLong = "Group name must be 200 characters or less";

    private static readonly DateTime AddedAtUtc = TestCommonItems.AddedAtUtc;

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public AddCommonItemGroupEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankNames { get; } = new("", "   ", "\t \r\n \n", null);

    [Fact]
    public async Task A_manager_s_add_answers_201_with_the_new_group_s_ID_alone_and_no_Location_and_stores_it()
    {
        using var response = await PostAsync(Caller.ManagerOfA, new { name = "Community outing" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["id"], body.EnumerateObject().Select(property => property.Name));
        var groupId = body.GetProperty("id").GetGuid();
        var stored = await StoredAsync(seeded.A.OrganisationId, groupId);
        Assert.Equal(
            (seeded.A.OrganisationId, "Community outing", false),
            (stored.OrganisationId, stored.Name, stored.IsEveryNote));
        Assert.Equal(((DateTime?)null, TestClock.Start.UtcDateTime), (stored.ArchivedAtUtc, stored.CreatedAtUtc));
        Assert.Equal(0, await ItemCountAsync(groupId));
        Assert.Equal([AuditEventTypes.CommonItemGroup.Created], await EventTypesAsync(groupId));
    }

    // In an organisation of the test's own, so that its groups are known. Archiving leaves gaps, and keeps an archived
    // group's SortOrder, which may be above every active one's (A2); Every note's, which always comes first, is not
    // counted, though it is the highest. Every other group stays as it was, versions and all.
    [Fact]
    public async Task A_group_added_after_archived_ones_goes_one_after_the_highest_active_SortOrder_but_Every_note_s()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        await AddEveryNoteAsync(organisationId, sortOrder: 9);
        await app.Services.AddGroupsAsync(
            organisationId,
            new("Community outing", 0),
            new("Overnight respite", 1, ArchivedAtUtc: AddedAtUtc),
            new("Personal care", 2),
            new("School holidays", 7, ArchivedAtUtc: AddedAtUtc));
        var before = Snapshot(await StoredGroupsAsync(organisationId));

        using var response = await PostAsync(managerId, new { name = "Shopping" });

        var groupId = await AddedIdAsync(response);
        Assert.Equal(3, (await StoredAsync(organisationId, groupId)).SortOrder);
        Assert.Equal(
            before,
            Snapshot((await StoredGroupsAsync(organisationId)).Where(group => group.Id != groupId)));
    }

    // Every note has SortOrder 0 too, as admin bootstrap leaves it, and still comes first, by IsEveryNote alone.
    [Fact]
    public async Task An_organisation_s_first_group_goes_at_0_and_is_listed_after_Every_note()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        var everyNoteId = await AddEveryNoteAsync(organisationId, sortOrder: 0);

        using var response = await PostAsync(managerId, new { name = "Community outing" });

        var groupId = await AddedIdAsync(response);
        Assert.Equal(0, (await StoredAsync(organisationId, groupId)).SortOrder);
        using var client = app.CreateHttpsClient().SignInAs(managerId);
        var listed = await client.GetFromJsonAsync<JsonElement>(Path, TestContext.Current.CancellationToken);
        Assert.Equal(
            [everyNoteId, groupId],
            listed.EnumerateArray().Select(group => group.GetProperty("id").GetGuid()));
    }

    // Three, as a manager sets up groups before go-live, each after the last, in the order added.
    [Fact]
    public async Task Groups_added_one_after_another_each_go_to_the_end()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        await AddEveryNoteAsync(organisationId, sortOrder: 0);
        string[] names = ["Community outing", "Personal care", "Shopping"];

        foreach (var name in names)
        {
            using var response = await PostAsync(managerId, new { name });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        Assert.Equal(
            names.Select((name, index) => (name, index)),
            (await StoredGroupsAsync(organisationId))
                .Where(group => !group.IsEveryNote)
                .OrderBy(group => group.SortOrder)
                .Select(group => (group.Name, group.SortOrder)));
    }

    // As the screen cleans it before it sends it (common-items.md *Validation*): a line break, CR, LF or both, with the
    // spaces and tabs on either side of it, becomes one space, and the ends are trimmed. Spaces elsewhere stay.
    [Theory]
    [InlineData("  Community outing\t", "Community outing")]
    [InlineData("Community\nouting", "Community outing")]
    [InlineData("Community \r\n\r\n  outing", "Community outing")]
    [InlineData("\nTrips\rto the\t\ncafé\n", "Trips to the café")]
    [InlineData("Phở  with Nguyễn", "Phở  with Nguyễn")]
    public async Task Line_breaks_are_stored_as_one_space_and_the_ends_trimmed(string name, string storedName)
    {
        using var response = await PostAsync(Caller.ManagerOfA, new { name });

        Assert.Equal(storedName, (await StoredAsync(seeded.A.OrganisationId, await AddedIdAsync(response))).Name);
    }

    // The limit counts the name as it is stored: 200 characters, the spaces at the ends left out and a line break
    // counted as one.
    [Fact]
    public async Task A_name_of_200_characters_once_cleaned_is_stored()
    {
        using var response = await PostAsync(
            Caller.ManagerOfA, new { name = $"  {new string('a', 100)} \n\n {new string('b', 99)}  " });

        Assert.Equal(
            new string('a', 100) + " " + new string('b', 99),
            (await StoredAsync(seeded.A.OrganisationId, await AddedIdAsync(response))).Name);
    }

    [Fact]
    public async Task A_name_of_201_characters_once_cleaned_is_refused_and_nothing_is_added()
    {
        await AssertRefusedAsync(new { name = new string('a', Limits.GroupName + 1) }, TooLong);
        await AssertRefusedAsync(new { name = $"{new string('a', 100)}\n{new string('b', 100)}" }, TooLong);
    }

    [Theory]
    [MemberData(nameof(BlankNames))]
    public async Task A_blank_or_null_name_is_refused_and_nothing_is_added(string? name)
    {
        await AssertRefusedAsync(new { name }, EnterTheGroupName);
    }

    [Fact]
    public async Task A_body_with_no_name_is_refused_and_nothing_is_added()
    {
        await AssertRefusedAsync(new { title = "Community outing" }, EnterTheGroupName);
    }

    // Only CommonItemGroup.EveryNote makes an Every note, so the organisation keeps its one, which the filtered unique
    // index would hold to all the same (S01.03.01's database tests).
    [Fact]
    public async Task An_isEveryNote_in_the_body_is_not_read_so_the_group_is_an_ordinary_one()
    {
        using var response = await PostAsync(Caller.ManagerOfA, new { name = "Every note", isEveryNote = true });

        var stored = await StoredAsync(seeded.A.OrganisationId, await AddedIdAsync(response));
        Assert.Equal(("Every note", false), (stored.Name, stored.IsEveryNote));
        var everyNotes = (await StoredGroupsAsync(seeded.A.OrganisationId)).Where(group => group.IsEveryNote);
        Assert.Equal([seeded.A.EveryNoteGroupId], everyNotes.Select(group => group.Id));
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise add the group.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_added()
    {
        var groupsBefore = await GroupCountAsync(seeded.A.OrganisationId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.WorkerOfA, new { name = "Community outing" });

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(groupsBefore, await GroupCountAsync(seeded.A.OrganisationId));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // Each stored value of the groups, their versions among them, in a form that compares by value, in SortOrder.
    private static List<(Guid, string, int, DateTime?, string)> Snapshot(IEnumerable<CommonItemGroup> groups) =>
        [.. groups.OrderBy(group => group.SortOrder).Select(group =>
            (group.Id, group.Name, group.SortOrder, group.ArchivedAtUtc, Convert.ToBase64String(group.RowVersion)))];

    // The ID in the body of an add's 201, which a test asserts it got.
    private static async Task<Guid> AddedIdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Sends <paramref name="body"/> as A's manager, and asserts that it gets <c>422 validation.failed</c> with
    /// <paramref name="message"/> alone, under <c>name</c>, and that no group was added and no event written.
    /// </summary>
    private async Task AssertRefusedAsync(object body, string message)
    {
        var groupsBefore = await GroupCountAsync(seeded.A.OrganisationId);
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, body);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            [("name", message)],
            problem.RootElement.GetProperty("errors").EnumerateObject()
                .Select(field => (field.Name, Assert.Single(field.Value.EnumerateArray()).GetString() ?? "")));
        Assert.Equal(groupsBefore, await GroupCountAsync(seeded.A.OrganisationId));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // As the SPA adds one: over HTTPS, with a token fetched as the caller, and the body as JSON.
    private Task<HttpResponseMessage> PostAsync(Caller caller, object body) =>
        PostAsync(app.CreateHttpsClient().SignInAs(caller, seeded), body);

    private Task<HttpResponseMessage> PostAsync(Guid userId, object body) =>
        PostAsync(app.CreateHttpsClient().SignInAs(userId), body);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, object body)
    {
        using (client)
        {
            return await client.PostWithTokenAsync(Path, await client.FetchTokenAsync(), body);
        }
    }

    // An organisation of the test's own, with no group yet, and an Active manager, who needs no passkey or password to
    // sign in with the test-only sign-in. An organisation is the tenant, not tenant-owned, and AspNetUsers is not
    // filtered, so neither save needs a tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationWithAManagerAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Add group test", CreatedAtUtc = AddedAtUtc };
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
    private async Task<CommonItemGroup> StoredAsync(Guid organisationId, Guid groupId) =>
        Assert.Single(await StoredGroupsAsync(organisationId), group => group.Id == groupId);

    // The organisation's groups as their rows hold them, through the app's context under its tenant.
    private async Task<List<CommonItemGroup>> StoredGroupsAsync(Guid organisationId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            return await db.CommonItemGroups.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // The types of the events written about the group, oldest first, read in SQL, with no tenant needed.
    private async Task<List<string>> EventTypesAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>(
                $"SELECT [EventType] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {groupId} ORDER BY [Id]")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // Read in SQL, in whichever organisation, with no tenant needed. The tests of the collection run one at a time, so
    // none adds a group or writes an event while a test compares these before and after its request.
    private async Task<int> GroupCountAsync(Guid organisationId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM [CommonItemGroup] WHERE [OrganisationId] = {organisationId}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ItemCountAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [CommonItem] WHERE [GroupId] = {groupId}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
