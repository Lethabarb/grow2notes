using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-items/{id}</c> (design.md §6.6, S01.03.02, S01.03.08), called with the test-only sign-in
/// (<see cref="TestSignIn"/>) on a fake clock: a manager rewords an item of their own organisation with the item's
/// <c>ETag</c> as <c>If-Match</c>, its text cleaned and checked by the add's rules, in its own group when the body
/// names no other, or moves it to the end of another active group (A42), and gets <c>200</c> with no body and no
/// <c>ETag</c>; the text already stored, in the item's own group, answers the same and writes nothing (D84); an item
/// that is unknown, or another organisation's, gets <c>404</c>, then, with no <c>If-Match</c>, <c>428</c>, with a stale
/// one, or an item changed after the handler loaded it, <c>412</c>, then an item whose group is archived
/// <c>409 config.group_archived</c>, then a chosen group that is unknown, or another organisation's, <c>404</c>, and an
/// archived one <c>409 config.group_archived</c>, then a text that the add refuses <c>422 validation.failed</c> in the
/// screen's words; each refusal changing nothing and writing no event. Each test changes an item that it adds to a
/// group of its own in A, moving it to another group of its own, or, into Every note, in an organisation of its own,
/// and changes no seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the <c>common_item.updated</c> that a reword and a move write.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UpdateCommonItemEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string StoredText = "Travelled by bus or train";

    private const string EnterTheCommonItem = "Enter the common item";

    private const string TooLong = "Common item must be 200 characters or less";

    // The group that a move chooses, beside the item's own, Community outing.
    private const string ChosenGroupName = "Personal care";

    private static readonly DateTime ArchivedAtUtc = TestCommonItems.AddedAtUtc;

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public UpdateCommonItemEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankTexts { get; } = new("", "   ", "\t \r\n \n", null);

    // Each groupId that keeps the item in its own group: none, a null one, or the item's own (T07's Notes).
    public static TheoryData<string> OwnGroupIds { get; } = new("none", "null", "own");

    [Fact]
    public async Task A_reword_answers_200_with_no_body_and_no_ETag_and_stores_the_text_alone()
    {
        var (groupId, item) = await AddItemAsync(new(StoredText, 2));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text = "Travelled by tram" }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            ("Travelled by tram", 2, (DateTime?)null, TestCommonItems.AddedAtUtc, groupId),
            (stored.Text, stored.SortOrder, stored.ArchivedAtUtc, stored.CreatedAtUtc, stored.GroupId));
        Assert.NotEqual(item.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([AuditEventTypes.CommonItem.Updated], await EventTypesAsync(item.Id));
    }

    // As the add cleans it (list-editor.md *Implementation notes*): a line break, with the spaces on either side of
    // it, becomes one space, and the ends are trimmed; 200 characters remain, so the text is stored.
    [Fact]
    public async Task Line_breaks_are_stored_as_one_space_and_200_characters_once_cleaned_are_stored()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(
            Caller.ManagerOfA,
            item.Id,
            new { text = $"  {new string('a', 100)} \r\n\n {new string('b', 99)}\t" },
            item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new string('a', 100) + " " + new string('b', 99), (await StoredAsync(item.Id)).Text);
    }

    // D84: the save finds the text unchanged, so it saves nothing, and the version stays the one the If-Match matched.
    [Theory]
    [InlineData(StoredText)]
    [InlineData(" Travelled by\n  bus or train ")]
    public async Task A_reword_to_the_stored_text_answers_200_and_writes_nothing(string text)
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        await AssertUnchangedAsync(item);
    }

    [Fact]
    public async Task With_no_If_Match_it_answers_428_and_changes_nothing()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text = "Travelled by tram" }, null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(item);
    }

    // As when another manager saved first: the second reword, sent with the version both loaded, changes nothing.
    [Fact]
    public async Task With_a_stale_If_Match_it_answers_412_and_writes_nothing()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));
        using (var first = await PutAsync(Caller.ManagerOfA, item.Id, new { text = "Travelled by tram" }, item.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var afterFirst = await StoredAsync(item.Id);

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text = "Travelled by ferry" }, item.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            ("Travelled by tram", RowVersionToken.ETag(afterFirst)), (stored.Text, RowVersionToken.ETag(stored)));
        Assert.Equal([AuditEventTypes.CommonItem.Updated], await EventTypesAsync(item.Id));
    }

    // The race that If-Match cannot catch: two managers reword at once, both with the version that their pages loaded.
    // The first's handler has loaded the item and checked it, and the second's reword commits before the first's save,
    // whose concurrency check then matches no row. That is an item changed since it was loaded, as a stale If-Match is,
    // so it gets 412, never 500, and the second's wording stands.
    [Fact]
    public async Task A_reword_whose_save_finds_the_item_changed_since_it_was_loaded_gets_412_and_writes_nothing()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));
        var gate = new HoldsTheFirstSaveOfTheItem(item.Id);
        await using var gated = app.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(gate))));

        var first = PutAsync(gated, Caller.ManagerOfA, item.Id, new { text = "Travelled by ferry" }, item.ETag);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await PutAsync(
                gated, Caller.ManagerOfA, item.Id, new { text = "Travelled by tram" }, item.ETag);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await first;
        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        Assert.Equal("Travelled by tram", (await StoredAsync(item.Id)).Text);
        Assert.Equal([AuditEventTypes.CommonItem.Updated], await EventTypesAsync(item.Id));
    }

    [Fact]
    public async Task An_item_of_201_characters_once_cleaned_is_refused_and_nothing_is_changed()
    {
        await AssertRefusedAsync(new { text = new string('a', Limits.ItemText + 1) }, TooLong);
        await AssertRefusedAsync(new { text = $"{new string('a', 100)}\n{new string('b', 100)}" }, TooLong);
    }

    [Theory]
    [MemberData(nameof(BlankTexts))]
    public async Task A_blank_or_null_text_is_refused_and_nothing_is_changed(string? text)
    {
        await AssertRefusedAsync(new { text }, EnterTheCommonItem);
    }

    [Fact]
    public async Task A_body_with_no_text_is_refused_and_nothing_is_changed()
    {
        await AssertRefusedAsync(new { item = "Travelled by tram" }, EnterTheCommonItem);
    }

    // The group is looked at after If-Match and before the text, so an archived one's item is a 409 even when the
    // text would be refused too; the group, restored first, is the screen's way out (design.md §6.6).
    [Theory]
    [InlineData("Packed a day bag")]
    [InlineData("")]
    public async Task An_archived_group_s_item_is_a_409_group_archived_problem_and_not_changed(string text)
    {
        var groupId = Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Overnight respite", 0, ArchivedAtUtc: ArchivedAtUtc)));
        var item = await app.Services.AddItemAsync(seeded.A.OrganisationId, groupId, new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text }, item.ETag);

        await AssertProblemAsync(response, ErrorCode.GroupArchived);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        await AssertUnchangedAsync(item);
    }

    // If-Match is checked before the group, so a stale or missing one is refused as such, whatever the group holds.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_archived_group_s_item_with_no_or_a_stale_If_Match_is_refused_for_its_If_Match(bool stale)
    {
        var groupId = Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Overnight respite", 0, ArchivedAtUtc: ArchivedAtUtc)));
        var item = await app.Services.AddItemAsync(seeded.A.OrganisationId, groupId, new(StoredText, 0));

        using var response = await PutAsync(
            Caller.ManagerOfA, item.Id, new { text = "Packed a day bag" }, stale ? IfMatch.ETag("AAAAAAAAB9E=") : null);

        await AssertProblemAsync(response, stale ? ErrorCode.PreconditionFailed : ErrorCode.PreconditionRequired);
        await AssertUnchangedAsync(item);
    }

    // A42: one more than the highest active SortOrder of the chosen group, past the gap that an archive left and
    // whatever SortOrder its archived item holds, as only a reorder closes gaps; the old group's other items keep their
    // places and versions. The event records the group and the place, before and after, and not the text, sent as it
    // was (S01.03.08's Note).
    [Fact]
    public async Task A_move_puts_the_item_after_the_chosen_group_s_active_items_and_leaves_the_old_group_as_it_was()
    {
        var (groupId, item) = await AddItemAsync(new(StoredText, 1));
        await app.Services.AddItemsAsync(
            seeded.A.OrganisationId, groupId, new("Paid for a ticket", 0), new("Bought lunch", 2));
        var chosenId = await AddGroupAsync(new(ChosenGroupName, 1));
        await app.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            chosenId,
            new("Showered", 0),
            new("Dressed", 3),
            new("Packed an overnight bag", 7, ArchivedAtUtc: ArchivedAtUtc));
        var oldGroupBefore = await StoredItemsAsync(groupId);

        using var response = await PutAsync(
            Caller.ManagerOfA, item.Id, new { text = StoredText, groupId = chosenId }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            (chosenId, StoredText, 4, (DateTime?)null),
            (stored.GroupId, stored.Text, stored.SortOrder, stored.ArchivedAtUtc));
        Assert.NotEqual(item.ETag, RowVersionToken.ETag(stored));
        Assert.Equal([.. oldGroupBefore.Where(listed => listed.Id != item.Id)], await StoredItemsAsync(groupId));
        AssertDetails(
            new JsonObject
            {
                ["old"] = new JsonObject { ["groupId"] = groupId, ["sortOrder"] = 1 },
                ["new"] = new JsonObject { ["groupId"] = chosenId, ["sortOrder"] = 4 },
            },
            Assert.Single(await EventDetailsAsync(item.Id)));
    }

    // One PUT, one event, which records the three values that changed, the text as stored once cleaned.
    [Fact]
    public async Task A_move_with_new_text_stores_both_and_records_the_group_the_text_and_the_place()
    {
        var (groupId, item) = await AddItemAsync(new(StoredText, 2));
        var chosenId = await AddGroupAsync(new(ChosenGroupName, 1));

        using var response = await PutAsync(
            Caller.ManagerOfA, item.Id, new { text = " Travelled by\n tram ", groupId = chosenId }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(item.Id);
        Assert.Equal((chosenId, "Travelled by tram", 0), (stored.GroupId, stored.Text, stored.SortOrder));
        AssertDetails(
            new JsonObject
            {
                ["old"] = new JsonObject { ["groupId"] = groupId, ["text"] = StoredText, ["sortOrder"] = 2 },
                ["new"] = new JsonObject { ["groupId"] = chosenId, ["text"] = "Travelled by tram", ["sortOrder"] = 0 },
            },
            Assert.Single(await EventDetailsAsync(item.Id)));
    }

    // T06's callers send {text} alone, and the screen sends the item's own group when no other is chosen: each is a
    // reword in place, whose event records the text alone.
    [Theory]
    [MemberData(nameof(OwnGroupIds))]
    public async Task A_body_with_no_other_group_rewords_the_item_in_its_place(string groupIdSent)
    {
        var (groupId, item) = await AddItemAsync(new(StoredText, 2));
        object body = groupIdSent switch
        {
            "none" => new { text = "Travelled by tram" },
            "null" => new { text = "Travelled by tram", groupId = (Guid?)null },
            _ => new { text = "Travelled by tram", groupId },
        };

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, body, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(item.Id);
        Assert.Equal((groupId, "Travelled by tram", 2), (stored.GroupId, stored.Text, stored.SortOrder));
        AssertDetails(
            new JsonObject
            {
                ["old"] = new JsonObject { ["text"] = StoredText },
                ["new"] = new JsonObject { ["text"] = "Travelled by tram" },
            },
            Assert.Single(await EventDetailsAsync(item.Id)));
    }

    // D84: the screen sends the item's own group with the text that it closes on when nothing changed; a body that
    // changes neither saves nothing, and the version stays the one the If-Match matched.
    [Fact]
    public async Task The_stored_text_with_the_item_s_own_group_answers_200_and_writes_nothing()
    {
        var (groupId, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text = StoredText, groupId }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.ETag);
        await AssertUnchangedAsync(item);
    }

    // The chosen group is looked at before the text, so an archived one is a 409 even when the text would be refused
    // too; the group, restored first, is the screen's way out (design.md §6.6).
    [Theory]
    [InlineData("Travelled by tram")]
    [InlineData("")]
    public async Task An_archived_chosen_group_is_a_409_group_archived_problem_and_nothing_is_changed(string text)
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));
        var chosenId = await AddGroupAsync(new(ChosenGroupName, 1, ArchivedAtUtc: ArchivedAtUtc));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text, groupId = chosenId }, item.ETag);

        await AssertProblemAsync(response, ErrorCode.GroupArchived);
        await AssertUnchangedAsync(item);
        Assert.Empty(await StoredItemsAsync(chosenId));
    }

    // The item's own group is looked at before the chosen one, so an item of an archived group is not moved out of it
    // either: its group, restored first, is the way out (design.md §6.6).
    [Fact]
    public async Task An_archived_group_s_item_sent_with_an_active_group_is_a_409_group_archived_problem_and_not_moved()
    {
        var groupId = await AddGroupAsync(new("Overnight respite", 0, ArchivedAtUtc: ArchivedAtUtc));
        var item = await app.Services.AddItemAsync(seeded.A.OrganisationId, groupId, new(StoredText, 0));
        var chosenId = await AddGroupAsync(new(ChosenGroupName, 1));

        using var response = await PutAsync(
            Caller.ManagerOfA, item.Id, new { text = StoredText, groupId = chosenId }, item.ETag);

        await AssertProblemAsync(response, ErrorCode.GroupArchived);
        await AssertUnchangedAsync(item);
        Assert.Empty(await StoredItemsAsync(chosenId));
    }

    // The chosen group is found through the tenant filter, as the add finds its group, so B's is not found (design.md
    // §9.2), and it is looked for before the text, so a text that would be refused does not hide it.
    [Theory]
    [InlineData(true, "Travelled by tram")]
    [InlineData(false, "")]
    public async Task A_chosen_group_of_another_organisation_or_of_none_is_a_404_problem_and_nothing_is_changed(
        bool anotherOrganisations, string text)
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));
        var chosenId = anotherOrganisations
            ? Assert.Single(await app.Services.AddGroupsAsync(
                seeded.B.OrganisationId, new TestCommonItemGroup(ChosenGroupName, 0)))
            : Guid.NewGuid();

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text, groupId = chosenId }, item.ETag);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        await AssertUnchangedAsync(item);
    }

    // Every note takes items as any active group does: only a rename, a move or an archive of Every note itself is
    // refused (A43). In an organisation of the test's own, as no test changes a seeded Every note (tests/README.md).
    [Fact]
    public async Task An_item_moved_into_Every_note_goes_after_its_highest_active_item()
    {
        var (organisationId, managerId) = await AddOrganisationWithAManagerAsync();
        var everyNoteId = await AddEveryNoteAsync(organisationId);
        await app.Services.AddItemsAsync(
            organisationId, everyNoteId, new("Medication prompted", 0), new("Meal prepared", 1));
        var groupId = Assert.Single(
            await app.Services.AddGroupsAsync(organisationId, new TestCommonItemGroup("Community outing", 0)));
        var item = await app.Services.AddItemAsync(organisationId, groupId, new(StoredText, 0));

        using var response = await PutAsync(
            managerId, item.Id, new { text = StoredText, groupId = everyNoteId }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(item.Id, organisationId);
        Assert.Equal((everyNoteId, StoredText, 2), (stored.GroupId, stored.Text, stored.SortOrder));
        Assert.Equal([AuditEventTypes.CommonItem.Updated], await EventTypesAsync(item.Id));
    }

    // As the reword of an archived item is taken (T06's Notes), so is its move, though the screen moves active items
    // alone: it keeps the time it was archived at, and goes after the chosen group's active items, as a restore would
    // put it.
    [Fact]
    public async Task An_archived_item_moved_to_another_group_stays_archived_after_that_group_s_active_items()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 4, ArchivedAtUtc: ArchivedAtUtc));
        var chosenId = await AddGroupAsync(new(ChosenGroupName, 1));
        await app.Services.AddItemsAsync(seeded.A.OrganisationId, chosenId, new TestCommonItem("Showered", 0));

        using var response = await PutAsync(
            Caller.ManagerOfA, item.Id, new { text = StoredText, groupId = chosenId }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            (chosenId, 1, (DateTime?)ArchivedAtUtc), (stored.GroupId, stored.SortOrder, stored.ArchivedAtUtc));
    }

    // The screen offers Edit on active items alone, but the server refuses nothing on an archived item of an active
    // group (T06's Notes).
    [Fact]
    public async Task An_archived_item_of_an_active_group_is_reworded_and_stays_archived_in_its_place()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 4, ArchivedAtUtc: ArchivedAtUtc));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, new { text = "Travelled by tram" }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(item.Id);
        Assert.Equal(
            ("Travelled by tram", 4, (DateTime?)ArchivedAtUtc), (stored.Text, stored.SortOrder, stored.ArchivedAtUtc));
    }

    // B's manager does not find A's items (design.md §9.2), so the version is never checked, and one who sends the
    // item's current ETag, as one who had it would, gets the same 404 as one who sends none.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Another_organisation_s_item_is_a_404_problem_and_not_changed(bool withItsETag)
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(
            Caller.ManagerOfB, item.Id, new { text = "Travelled by tram" }, withItsETag ? item.ETag : null);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        await AssertUnchangedAsync(item);
    }

    // The item is looked for first, so an unknown one is a 404 even when its If-Match or text would be refused too.
    [Theory]
    [InlineData("Travelled by tram", true)]
    [InlineData("", false)]
    public async Task An_item_of_no_organisation_is_a_404_problem(string text, bool withIfMatch)
    {
        var lastEventId = await LastEventIdAsync();

        using var response = await PutAsync(
            Caller.ManagerOfA, Guid.NewGuid(), new { text }, withIfMatch ? IfMatch.ETag("AAAAAAAAB9E=") : null);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // The route takes a GUID alone, so another ID matches no endpoint, and gets the unknown API path's 404.
    [Fact]
    public async Task An_ID_that_is_not_a_GUID_is_a_404()
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PutWithTokenAsync(
            "/api/admin/common-items/travelled-by-bus",
            await client.FetchTokenAsync(),
            new { text = "Travelled by tram" },
            IfMatch.ETag("AAAAAAAAB9E="));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The /api/admin group's policy refuses the worker before the handler, which would otherwise reword the item.
    [Fact]
    public async Task A_worker_gets_403_and_nothing_is_changed()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.WorkerOfA, item.Id, new { text = "Travelled by tram" }, item.ETag);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        await AssertUnchangedAsync(item);
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static void AssertDetails(JsonObject expected, string? written) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected.ToJsonString()), JsonNode.Parse(written ?? "null")),
            $"Details: expected {expected.ToJsonString()}, written {written}.");

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Null(response.Headers.ETag);
    }

    /// <summary>
    /// Sends <paramref name="body"/> as A's manager, with the ETag, to an item added to A for the call, and asserts
    /// that it gets <c>422 validation.failed</c> with <paramref name="message"/> alone, under <c>text</c>, and that the
    /// item was not changed and no event written.
    /// </summary>
    private async Task AssertRefusedAsync(object body, string message)
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(Caller.ManagerOfA, item.Id, body, item.ETag);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal(
            ErrorCode.ValidationFailed.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.Equal(
            [("text", message)],
            problem.RootElement.GetProperty("errors").EnumerateObject()
                .Select(field => (field.Name, Assert.Single(field.Value.EnumerateArray()).GetString() ?? "")));
        await AssertUnchangedAsync(item);
    }

    // The item as it was added, at the same version, with no event written about it.
    private async Task AssertUnchangedAsync(AddedCommonItem item)
    {
        var stored = await StoredAsync(item.Id);
        Assert.Equal((StoredText, item.ETag), (stored.Text, RowVersionToken.ETag(stored)));
        Assert.Empty(await EventTypesAsync(item.Id));
    }

    // An item, as given, of an active group added to A for the test.
    private async Task<(Guid GroupId, AddedCommonItem Item)> AddItemAsync(TestCommonItem item)
    {
        var groupId = await AddGroupAsync(new("Community outing", 0));
        return (groupId, await app.Services.AddItemAsync(seeded.A.OrganisationId, groupId, item));
    }

    // A group, as given, added to A for the test.
    private async Task<Guid> AddGroupAsync(TestCommonItemGroup group) =>
        Assert.Single(await app.Services.AddGroupsAsync(seeded.A.OrganisationId, group));

    private Task<HttpResponseMessage> PutAsync(Caller caller, Guid itemId, object body, string? ifMatch) =>
        PutAsync(app, caller, itemId, body, ifMatch);

    private async Task<HttpResponseMessage> PutAsync(
        WebApplicationFactory<Program> host, Caller caller, Guid itemId, object body, string? ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(caller, seeded);
        return await PutAsync(client, itemId, body, ifMatch);
    }

    private async Task<HttpResponseMessage> PutAsync(Guid userId, Guid itemId, object body, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(userId);
        return await PutAsync(client, itemId, body, ifMatch);
    }

    // As the SPA rewords or moves one: over HTTPS, with a token fetched as the caller, and, unless null, the ETag as
    // If-Match.
    private static async Task<HttpResponseMessage> PutAsync(
        HttpClient client, Guid itemId, object body, string? ifMatch) =>
        await client.PutWithTokenAsync(
            $"/api/admin/common-items/{itemId}", await client.FetchTokenAsync(), body, ifMatch);

    // The item as its row holds it, through the app's context under its organisation's tenant, A's unless given.
    private async Task<CommonItem> StoredAsync(Guid itemId, Guid? organisationId = null)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (tenant.Use(organisationId ?? seeded.A.OrganisationId))
        {
            return await db.CommonItems.AsNoTracking()
                .SingleAsync(item => item.Id == itemId, TestContext.Current.CancellationToken);
        }
    }

    // The items of a group of A, each as its row holds it, its version included, in SortOrder.
    private async Task<List<(Guid Id, string Text, int SortOrder, DateTime? ArchivedAtUtc, string ETag)>>
        StoredItemsAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            var items = await db.CommonItems.AsNoTracking()
                .Where(item => item.GroupId == groupId)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(TestContext.Current.CancellationToken);
            return [.. items.Select(item =>
                (item.Id, item.Text, item.SortOrder, item.ArchivedAtUtc, RowVersionToken.ETag(item)))];
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

    // The Details of the events written about the item, oldest first, read in SQL, with no tenant needed.
    private async Task<List<string?>> EventDetailsAsync(Guid itemId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string?>(
                $"SELECT [Details] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {itemId} ORDER BY [Id]")
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

    // An organisation of the test's own, with no group yet, and an Active manager, who needs no passkey or password to
    // sign in with the test-only sign-in. An organisation is the tenant, not tenant-owned, and AspNetUsers is not
    // filtered, so neither save needs a tenant.
    private async Task<(Guid OrganisationId, Guid ManagerId)> AddOrganisationWithAManagerAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Move items test", CreatedAtUtc = TestCommonItems.AddedAtUtc };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = $"{Guid.NewGuid():N}@example.org";
        var manager = new ApplicationUser
        {
            OrganisationId = organisation.Id,
            DisplayName = "Jamie Lee",
            Role = UserRole.Manager,
            Status = UserStatus.Active,
            InvitedAtUtc = TestCommonItems.AddedAtUtc,
            ActivatedAtUtc = TestCommonItems.AddedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(manager);
        Assert.True(result.Succeeded);
        return (organisation.Id, manager.Id);
    }

    // The organisation's Every note group, made as the app makes it.
    private async Task<Guid> AddEveryNoteAsync(Guid organisationId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var everyNote = CommonItemGroup.EveryNote(organisationId, TestCommonItems.AddedAtUtc);
        db.CommonItemGroups.Add(everyNote);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return everyNote.Id;
    }

    /// <summary>
    /// Holds back the first save that changes the item whose ID it is given, until the test lets it go: in a reword,
    /// the one save of the text and its event, made once the handler has loaded the item, checked its ETag and found
    /// its group active. Every other save passes.
    /// </summary>
    private sealed class HoldsTheFirstSaveOfTheItem(Guid itemId) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int held;

        public Task Reached => reached.Task;

        public void Release() => released.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { } context
                && context.ChangeTracker.Entries<CommonItem>().Any(
                    entry => entry.State == EntityState.Modified && entry.Entity.Id == itemId)
                && Interlocked.Exchange(ref held, 1) == 0)
            {
                reached.SetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
