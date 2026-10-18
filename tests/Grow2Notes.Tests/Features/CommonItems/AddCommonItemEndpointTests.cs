using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>POST /api/admin/common-item-groups/{groupId}/items</c> (design.md §6.6, S01.03.02), called with the test-only
/// sign-in (<see cref="TestSignIn"/>) on a fake clock: a manager adds an item at the end of an active group of their
/// own organisation (A2), its text on one line with its ends trimmed, and gets <c>201</c> with no body; a group that is
/// unknown, or another organisation's, gets <c>404</c>, then an archived one <c>409 config.group_archived</c>, then a
/// text that is empty or too long once cleaned <c>422 validation.failed</c> in the screen's words; each refusal adding
/// nothing and writing no event. The tests add their own groups and items to seeded organisation A and change no
/// seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the <c>common_item.created</c> that an add writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AddCommonItemEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string EnterTheCommonItem = "Enter the common item";

    private const string TooLong = "Common item must be 200 characters or less";

    private static readonly DateTime ArchivedAtUtc = TestCommonItems.AddedAtUtc;

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public AddCommonItemEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankTexts { get; } = new("", "   ", "\t \r\n \n", null);

    [Fact]
    public async Task A_manager_s_add_answers_201_with_no_body_and_stores_the_item_first_in_an_empty_group()
    {
        var groupId = await AddGroupAsync();

        using var response = await PostAsync(Caller.ManagerOfA, groupId, new { text = "Travelled by bus or train" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.Location);
        Assert.Equal("", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = Assert.Single(await StoredAsync(groupId));
        Assert.Equal(
            (seeded.A.OrganisationId, groupId, "Travelled by bus or train", 0),
            (stored.OrganisationId, stored.GroupId, stored.Text, stored.SortOrder));
        Assert.Equal(((DateTime?)null, TestClock.Start.UtcDateTime), (stored.ArchivedAtUtc, stored.CreatedAtUtc));
        Assert.Equal([AuditEventTypes.CommonItem.Created], await EventTypesAsync(stored.Id));
    }

    // Five, as a manager adds them on a phone (design.md §14 M1), each after the last, in the order added.
    [Fact]
    public async Task Items_added_one_after_another_each_go_to_the_end()
    {
        var groupId = await AddGroupAsync();
        string[] texts =
        [
            "Travelled by bus or train", "Paid for a ticket at the counter", "Bought lunch", "Visited the library",
            "Walked home",
        ];

        foreach (var text in texts)
        {
            using var response = await PostAsync(Caller.ManagerOfA, groupId, new { text });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var stored = await StoredAsync(groupId);
        Assert.Equal(texts.Select((text, index) => (text, index)), stored.Select(item => (item.Text, item.SortOrder)));
    }

    // Archiving leaves gaps, and keeps an archived item's SortOrder, which may be above every active one's (A2). Only
    // the group's own items count, and another group's, with higher SortOrders, stay as they were, versions and all.
    [Fact]
    public async Task An_item_added_after_archived_ones_goes_one_after_the_highest_active_SortOrder_of_its_group()
    {
        var groupIds = await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new("Community outing", 0), new("Personal care", 1));
        var (groupId, otherGroupId) = (groupIds[0], groupIds[1]);
        await app.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            groupId,
            new("Travelled by bus or train", 0),
            new("Paid for a ticket at the counter", 1, ArchivedAtUtc: ArchivedAtUtc),
            new("Bought lunch", 2),
            new("Visited the library", 7, ArchivedAtUtc: ArchivedAtUtc));
        await app.Services.AddItemsAsync(
            seeded.A.OrganisationId, otherGroupId, new("Showered", 5), new("Brushed teeth", 9));
        var otherGroupBefore = await StoredAsync(otherGroupId);

        using var response = await PostAsync(Caller.ManagerOfA, groupId, new { text = "Walked home" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(3, (await StoredAsync(groupId)).Single(item => item.Text == "Walked home").SortOrder);
        Assert.Equal(Snapshot(otherGroupBefore), Snapshot(await StoredAsync(otherGroupId)));
    }

    // As the screen cleans it before it sends it (list-editor.md *Implementation notes*): a line break, CR, LF or
    // both, with the spaces and tabs on either side of it, becomes one space, and the ends are trimmed. Spaces
    // elsewhere stay.
    [Theory]
    [InlineData("  Travelled by bus or train\t", "Travelled by bus or train")]
    [InlineData("Travelled by bus\nor train", "Travelled by bus or train")]
    [InlineData("Travelled by bus \r\n\r\n  or train", "Travelled by bus or train")]
    [InlineData("\nBought\rlunch\t\nat the café\n", "Bought lunch at the café")]
    [InlineData("Cooked phở  with Nguyễn", "Cooked phở  with Nguyễn")]
    public async Task Line_breaks_are_stored_as_one_space_and_the_ends_trimmed(string text, string storedText)
    {
        var groupId = await AddGroupAsync();

        using var response = await PostAsync(Caller.ManagerOfA, groupId, new { text });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(storedText, Assert.Single(await StoredAsync(groupId)).Text);
    }

    // The limit counts the text as it is stored: 200 characters, the spaces at the ends left out and a line break
    // counted as one.
    [Fact]
    public async Task An_item_of_200_characters_once_cleaned_is_stored()
    {
        var groupId = await AddGroupAsync();
        var longest = new string('a', 100) + " " + new string('b', 99);

        using var response = await PostAsync(
            Caller.ManagerOfA, groupId, new { text = $"  {new string('a', 100)} \n\n {new string('b', 99)}  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(longest, Assert.Single(await StoredAsync(groupId)).Text);
    }

    [Fact]
    public async Task An_item_of_201_characters_once_cleaned_is_refused_and_nothing_is_added()
    {
        await AssertRefusedAsync(new { text = new string('a', Limits.ItemText + 1) }, TooLong);
        await AssertRefusedAsync(new { text = $"{new string('a', 100)}\n{new string('b', 100)}" }, TooLong);
    }

    [Theory]
    [MemberData(nameof(BlankTexts))]
    public async Task A_blank_or_null_text_is_refused_and_nothing_is_added(string? text)
    {
        await AssertRefusedAsync(new { text }, EnterTheCommonItem);
    }

    [Fact]
    public async Task A_body_with_no_text_is_refused_and_nothing_is_added()
    {
        await AssertRefusedAsync(new { item = "Travelled by bus or train" }, EnterTheCommonItem);
    }

    // The group is looked at before the text, so an archived one is a 409 even when the text would be refused too.
    [Theory]
    [InlineData("Travelled by bus or train")]
    [InlineData("")]
    public async Task An_archived_group_is_a_409_group_archived_problem_and_nothing_is_added(string text)
    {
        var groupId = Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Overnight respite", 0, ArchivedAtUtc: ArchivedAtUtc)));
        await app.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new TestCommonItem("Packed an overnight bag", 0));
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, groupId, new { text });

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        Assert.Equal(
            ErrorCode.GroupArchived.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(["Packed an overnight bag"], (await StoredAsync(groupId)).Select(item => item.Text));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // B's manager does not find A's groups, nor A's B's (§9.2), so neither adds an item to the other's, the seeded
    // Every note groups included.
    [Fact]
    public async Task Another_organisation_s_group_is_a_404_problem_and_nothing_is_added()
    {
        var groupId = await AddGroupAsync();
        var lastEventId = await LastEventIdAsync();

        foreach (var (caller, otherGroupId) in new[]
        {
            (Caller.ManagerOfB, groupId),
            (Caller.ManagerOfB, seeded.A.EveryNoteGroupId),
            (Caller.ManagerOfA, seeded.B.EveryNoteGroupId),
        })
        {
            var itemsBefore = await ItemCountAsync(otherGroupId);

            using var response = await PostAsync(caller, otherGroupId, new { text = "Travelled by bus or train" });

            await response.ReadProblemAsync(HttpStatusCode.NotFound);
            Assert.Equal(itemsBefore, await ItemCountAsync(otherGroupId));
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The group is looked for first, so an unknown one is a 404 even when the text would be refused too.
    [Theory]
    [InlineData("Travelled by bus or train")]
    [InlineData("")]
    public async Task A_group_of_no_organisation_is_a_404_problem(string text)
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, Guid.NewGuid(), new { text });

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PostWithTokenAsync(
            "/api/admin/common-item-groups/every-note/items",
            await client.FetchTokenAsync(),
            new { text = "Travelled by bus or train" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise add the item.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_added()
    {
        var groupId = await AddGroupAsync();
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.WorkerOfA, groupId, new { text = "Travelled by bus or train" });

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Equal(0, await ItemCountAsync(groupId));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    // Each stored value of the items, their versions among them, in a form that compares by value.
    private static List<(Guid, string, int, DateTime?, string)> Snapshot(IEnumerable<CommonItem> items) =>
        [.. items.Select(item =>
            (item.Id, item.Text, item.SortOrder, item.ArchivedAtUtc, Convert.ToBase64String(item.RowVersion)))];

    /// <summary>
    /// Sends <paramref name="body"/> as A's manager to a group added to A for the call, and asserts that it gets
    /// <c>422 validation.failed</c> with <paramref name="message"/> alone, under <c>text</c>, and that no item was
    /// added and no event written.
    /// </summary>
    private async Task AssertRefusedAsync(object body, string message)
    {
        var groupId = await AddGroupAsync();
        var lastEventId = await LastEventIdAsync();

        using var response = await PostAsync(Caller.ManagerOfA, groupId, body);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            [("text", message)],
            problem.RootElement.GetProperty("errors").EnumerateObject()
                .Select(field => (field.Name, Assert.Single(field.Value.EnumerateArray()).GetString() ?? "")));
        Assert.Equal(0, await ItemCountAsync(groupId));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // An active group with no items, added to A for the test.
    private async Task<Guid> AddGroupAsync() =>
        Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));

    // As the SPA adds one: over HTTPS, with a token fetched as the caller, and the body as JSON.
    private async Task<HttpResponseMessage> PostAsync(Caller caller, Guid groupId, object body)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}/items", await client.FetchTokenAsync(), body);
    }

    // The group's items as their rows hold them, in A, through the app's context under A's tenant, in SortOrder.
    private async Task<List<CommonItem>> StoredAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.CommonItems.AsNoTracking()
                .Where(item => item.GroupId == groupId)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(TestContext.Current.CancellationToken);
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

    // Read in SQL, in whichever organisation, with no tenant needed. The tests of the collection run one at a time, so
    // none adds an item or writes an event while a test compares these before and after its request.
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
