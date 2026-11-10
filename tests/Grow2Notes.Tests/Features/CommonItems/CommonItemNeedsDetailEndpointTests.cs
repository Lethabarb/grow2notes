using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.CommonItems;

/// <summary>
/// Whether ticking a common item asks for details, on <c>POST /api/admin/common-item-groups/{groupId}/items</c> and
/// <c>PUT /api/admin/common-items/{id}</c> (design.md §6.6, D104, A151), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): an item is added not asking unless the body says <c>needsDetail</c>; a reword may set
/// it, the events <c>common_item.created</c> and <c>common_item.updated</c> record it, with its old and new value on an
/// update that changes it alone; a request that leaves it out keeps the stored value; one that sends the stored text
/// and value writes nothing and answers the same <c>ETag</c> (D84); a missing <c>If-Match</c> gets <c>428</c> and a
/// stale one <c>412</c>; and the admin list gives it. Each test adds a group of its own in A (tests/README.md).
/// <see cref="AddCommonItemEndpointTests"/> and <see cref="UpdateCommonItemEndpointTests"/> cover the text and the
/// group.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class CommonItemNeedsDetailEndpointTests : IClassFixture<Grow2NotesFactory>, IAsyncDisposable
{
    private const string StoredText = "Other (staff to complete if different from above)";

    private readonly WebApplicationFactory<Program> app;

    private readonly SeededOrganisations seeded;

    public CommonItemNeedsDetailEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    {
        app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(TestClock.Start)));
        seeded = sqlServer.Seeded;
    }

    [Fact]
    public async Task An_item_added_with_no_needsDetail_does_not_ask_and_the_event_records_it()
    {
        var groupId = await AddGroupAsync();

        using var response = await PostAsync(groupId, new { text = StoredText });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = Assert.Single(await StoredForAsync(groupId));
        Assert.False(item.NeedsDetail);
        var details = Assert.Single(await DetailsAsync(item.Id));
        Assert.False(details["new"]!["needsDetail"]!.GetValue<bool>());
    }

    [Fact]
    public async Task An_item_added_as_asking_for_details_is_stored_so_and_the_event_records_it()
    {
        var groupId = await AddGroupAsync();

        using var response = await PostAsync(groupId, new { text = StoredText, needsDetail = true });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = Assert.Single(await StoredForAsync(groupId));
        Assert.True(item.NeedsDetail);
        var details = Assert.Single(await DetailsAsync(item.Id));
        Assert.True(details["new"]!["needsDetail"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_request_that_changes_needsDetail_alone_writes_common_item_updated_with_the_old_and_new_value()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(item.Id, new { text = StoredText, needsDetail = true }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(item.Id);
        Assert.True(stored.NeedsDetail);
        Assert.NotEqual(item.ETag, RowVersionToken.ETag(stored));
        var details = Assert.Single(await DetailsAsync(item.Id));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["needsDetail"] = false }, details["old"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["needsDetail"] = true }, details["new"]));
    }

    // A reword that sends no value keeps the stored one, as a stale page or a tool that knows nothing of it would.
    [Fact]
    public async Task A_request_that_leaves_needsDetail_out_keeps_the_stored_value()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0, NeedsDetail: true));

        using var response = await PutAsync(item.Id, new { text = "Other (please say)" }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await StoredAsync(item.Id);
        Assert.Equal(("Other (please say)", true), (stored.Text, stored.NeedsDetail));
        var details = Assert.Single(await DetailsAsync(item.Id));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["text"] = "Other (please say)" }, details["new"]));
    }

    // D84: the same text and value saves nothing and writes no event, and the ETag stays the one the If-Match matched.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_stored_text_and_value_sent_again_answer_200_with_the_same_ETag_and_write_nothing(
        bool needsDetail)
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0, NeedsDetail: needsDetail));

        using var response = await PutAsync(item.Id, new { text = StoredText, needsDetail }, item.ETag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(item.ETag, RowVersionToken.ETag(await StoredAsync(item.Id)));
        Assert.Empty(await DetailsAsync(item.Id));
    }

    [Fact]
    public async Task With_no_If_Match_a_change_answers_428_and_changes_nothing()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));

        using var response = await PutAsync(item.Id, new { text = StoredText, needsDetail = true }, null);

        await AssertProblemAsync(response, ErrorCode.PreconditionRequired);
        Assert.False((await StoredAsync(item.Id)).NeedsDetail);
        Assert.Empty(await DetailsAsync(item.Id));
    }

    [Fact]
    public async Task With_a_stale_If_Match_a_change_answers_412_and_changes_nothing()
    {
        var (_, item) = await AddItemAsync(new(StoredText, 0));
        using (var first = await PutAsync(item.Id, new { text = "Other (please say)" }, item.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using var response = await PutAsync(item.Id, new { text = StoredText, needsDetail = true }, item.ETag);

        await AssertProblemAsync(response, ErrorCode.PreconditionFailed);
        var stored = await StoredAsync(item.Id);
        Assert.Equal(("Other (please say)", false), (stored.Text, stored.NeedsDetail));
    }

    [Fact]
    public async Task The_admin_list_gives_each_item_s_needsDetail()
    {
        var groupId = await AddGroupAsync();
        await app.Services.AddItemsAsync(
            seeded.A.OrganisationId,
            groupId,
            new TestCommonItem("Plain", 0),
            new TestCommonItem(StoredText, 1, NeedsDetail: true));
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.GetAsync(
            "/api/admin/common-item-groups", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var group = groups.EnumerateArray().Single(entry => entry.GetProperty("id").GetGuid() == groupId);
        Assert.Equal(
            [("Plain", false), (StoredText, true)],
            group.GetProperty("items").EnumerateArray().Select(
                item => (item.GetProperty("text").GetString(), item.GetProperty("needsDetail").GetBoolean())));
    }

    public ValueTask DisposeAsync() => app.DisposeAsync();

    private static async Task AssertProblemAsync(HttpResponseMessage response, ErrorCode code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync((HttpStatusCode)code.Status));
        Assert.Equal(code.Code, problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // An active group with no items, added to A for the test.
    private async Task<Guid> AddGroupAsync() =>
        Assert.Single(await app.Services.AddGroupsAsync(
            seeded.A.OrganisationId, new TestCommonItemGroup("Community outing", 0)));

    // An item, as given, of a group added to A for the test.
    private async Task<(Guid GroupId, AddedCommonItem Item)> AddItemAsync(TestCommonItem item)
    {
        var groupId = await AddGroupAsync();
        return (groupId, await app.Services.AddItemAsync(seeded.A.OrganisationId, groupId, item));
    }

    private async Task<HttpResponseMessage> PostAsync(Guid groupId, object body)
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        return await client.PostWithTokenAsync(
            $"/api/admin/common-item-groups/{groupId}/items", await client.FetchTokenAsync(), body);
    }

    private async Task<HttpResponseMessage> PutAsync(Guid itemId, object body, string? ifMatch)
    {
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        return await client.PutWithTokenAsync(
            $"/api/admin/common-items/{itemId}", await client.FetchTokenAsync(), body, ifMatch);
    }

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

    private async Task<List<CommonItem>> StoredForAsync(Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.CommonItems.AsNoTracking()
                .Where(item => item.GroupId == groupId)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // The Details of the events written about the item, oldest first, read in SQL, with no tenant needed.
    private async Task<List<JsonNode>> DetailsAsync(Guid itemId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var details = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>(
                $"SELECT [Details] AS [Value] FROM [AuditEvent] WHERE [EntityId] = {itemId} ORDER BY [Id]")
            .ToListAsync(TestContext.Current.CancellationToken);
        return [.. details.Select(text => JsonNode.Parse(text)!)];
    }
}
