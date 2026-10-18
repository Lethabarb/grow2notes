using System.Net;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// <c>PUT /api/admin/common-items/{id}</c> (design.md §6.6, S01.03.02), called with the test-only sign-in
/// (<see cref="TestSignIn"/>) on a fake clock: a manager rewords an item of their own organisation in its own group
/// with the item's <c>ETag</c> as <c>If-Match</c>, its text cleaned and checked by the add's rules, and gets
/// <c>200</c> with no body and no <c>ETag</c>; the text already stored answers the same and writes nothing (D84); an
/// item that is unknown, or another organisation's, gets <c>404</c>, then, with no <c>If-Match</c>, <c>428</c>, with a
/// stale one, or an item changed after the handler loaded it, <c>412</c>, then an item whose group is archived
/// <c>409 config.group_archived</c>, then a text that the add refuses <c>422 validation.failed</c> in the screen's
/// words; each refusal changing nothing and writing no event. Each test rewords an item that it adds to a group of its
/// own in A, and changes no seeded row (tests/README.md). The endpoint matrix covers the statuses of every caller, and
/// <see cref="Platform.Audit.AuditEventsTests"/> the <c>common_item.updated</c> that a reword writes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class UpdateCommonItemEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string StoredText = "Travelled by bus or train";

    private const string EnterTheCommonItem = "Enter the common item";

    private const string TooLong = "Common item must be 200 characters or less";

    private static readonly DateTime ArchivedAtUtc = TestCommonItems.AddedAtUtc;

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public UpdateCommonItemEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    public static TheoryData<string?> BlankTexts { get; } = new("", "   ", "\t \r\n \n", null);

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
        var groupId = Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));
        return (groupId, await app.Services.AddItemAsync(seeded.A.OrganisationId, groupId, item));
    }

    private Task<HttpResponseMessage> PutAsync(Caller caller, Guid itemId, object body, string? ifMatch) =>
        PutAsync(app, caller, itemId, body, ifMatch);

    // As the SPA rewords one: over HTTPS, with a token fetched as the caller, and, unless null, the ETag as If-Match.
    private async Task<HttpResponseMessage> PutAsync(
        WebApplicationFactory<Program> host, Caller caller, Guid itemId, object body, string? ifMatch)
    {
        using var client = host.CreateHttpsClient().SignInAs(caller, seeded);
        return await client.PutWithTokenAsync(
            $"/api/admin/common-items/{itemId}", await client.FetchTokenAsync(), body, ifMatch);
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

    // The tests of the collection run one at a time, so none writes an event while a test compares this before and
    // after its request.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
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
