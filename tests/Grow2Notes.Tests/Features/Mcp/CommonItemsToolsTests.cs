using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>list_common_items</c> (mcp-server.md §5.2, S07.02.05) through the real <c>/mcp</c>: the groups and items it
/// lists and in what order, the versions it gives, and the one audit row each call writes. The guards that every tool
/// shares are in <see cref="ToolMatrixTests"/>. The cases share the app of <see cref="ToolWorld"/>, so each looks at
/// the rows of the scene, which no case changes, and at the rows it adds itself.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class CommonItemsToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Tool = "list_common_items";

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task Every_note_comes_first_and_archived_groups_and_items_are_left_out()
    {
        var (result, _) = await world.CallAsync(Tool, new());

        Assert.NotEqual(true, result.IsError);
        var groups = GroupsOf(result);
        Assert.Equal(Scene.EveryNoteGroupId, groups[0].GetProperty("groupId").GetGuid());
        Assert.True(groups[0].GetProperty("isEveryNote").GetBoolean());
        Assert.Single(groups, group => group.GetProperty("isEveryNote").GetBoolean());
        Assert.All(groups, group => Assert.False(group.GetProperty("archived").GetBoolean()));
        Assert.DoesNotContain(Scene.ArchivedGroup.Id, GroupIds(groups));
        Assert.Equal([Scene.Items[0].Id, Scene.Items[1].Id], ItemIds(GroupOf(groups, Scene.Group.Id)));
        Assert.All(
            groups,
            group => Assert.All(ItemsOf(group), item => Assert.False(item.GetProperty("archived").GetBoolean())));
    }

    [Fact]
    public async Task Archived_groups_and_items_are_listed_with_include_archived_after_the_active_ones()
    {
        var archivedGroup = await Scene.AddGroupAsync(archived: true);
        var activeGroup = await Scene.AddGroupAsync();

        var (result, _) = await world.CallAsync(Tool, new() { ["includeArchived"] = true });

        var groups = GroupsOf(result);
        Assert.Equal(Scene.EveryNoteGroupId, groups[0].GetProperty("groupId").GetGuid());
        var archivedFlags = groups.Select(group => group.GetProperty("archived").GetBoolean()).ToList();
        Assert.Equal(archivedFlags.Order(), archivedFlags);
        Assert.Contains(Scene.ArchivedGroup.Id, GroupIds(groups));
        Assert.Contains(archivedGroup.Id, GroupIds(groups));
        Assert.Contains(activeGroup.Id, GroupIds(groups));

        // The app's order puts the archived group and the archived item first; the tool puts them last.
        Assert.True(GroupIds(groups).IndexOf(Scene.Group.Id) < GroupIds(groups).IndexOf(Scene.ArchivedGroup.Id));
        Assert.Equal(
            [Scene.Items[0].Id, Scene.Items[1].Id, Scene.Items[2].Id], ItemIds(GroupOf(groups, Scene.Group.Id)));
        Assert.Equal(
            [false, false, true],
            ItemsOf(GroupOf(groups, Scene.Group.Id)).Select(item => item.GetProperty("archived").GetBoolean()));
        Assert.Equal([Scene.ArchivedGroupItem.Id], ItemIds(GroupOf(groups, Scene.ArchivedGroup.Id)));
    }

    [Fact]
    public async Task Each_group_and_item_has_its_name_or_text_and_its_row_version_as_the_version()
    {
        var (result, _) = await world.CallAsync(Tool, new() { ["includeArchived"] = true });

        var groups = GroupsOf(result);
        foreach (var scene in new[] { Scene.Group, Scene.ArchivedGroup })
        {
            var group = GroupOf(groups, scene.Id);
            Assert.Equal(scene.Name, group.GetProperty("name").GetString());
            Assert.Equal(scene.Archived, group.GetProperty("archived").GetBoolean());
            Assert.False(group.GetProperty("isEveryNote").GetBoolean());
            Assert.Equal(ToolScene.ExpectedVersion(scene.ETag), group.GetProperty("expectedVersion").GetString());
            Assert.Equal(
                ["archived", "expectedVersion", "groupId", "isEveryNote", "items", "name"],
                group.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        }

        foreach (var scene in Scene.Items.Append(Scene.ArchivedGroupItem))
        {
            var item = ItemsOf(groups.Single(group => ItemIds(group).Contains(scene.Id)))
                .Single(candidate => candidate.GetProperty("itemId").GetGuid() == scene.Id);
            Assert.Equal(scene.Text, item.GetProperty("text").GetString());
            Assert.Equal(scene.Archived, item.GetProperty("archived").GetBoolean());
            Assert.Equal(ToolScene.ExpectedVersion(scene.ETag), item.GetProperty("expectedVersion").GetString());
            Assert.Equal(
                ["archived", "expectedVersion", "itemId", "text"],
                item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_audit_row_holds_exactly_the_group_and_item_IDs_returned(bool includeArchived)
    {
        var (result, row) = await world.CallAsync(Tool, new() { ["includeArchived"] = includeArchived });

        var groups = GroupsOf(result);
        Assert.Null(row.ParticipantId);
        AssertRead(
            row,
            AssistantFlow.ClientId,
            Tool,
            ("groupIds", [.. GroupIds(groups)]),
            ("commonItemIds", [.. groups.SelectMany(ItemIds)]));
    }

    [Fact]
    public async Task A_manager_of_another_organisation_lists_only_that_organisations_groups()
    {
        var (result, row) = await world.CallAsync(
            Tool, new() { ["includeArchived"] = true }, ToolCaller.ManagerOfAnotherOrganisation);

        var group = Assert.Single(GroupsOf(result));
        Assert.Equal(Scene.OtherOrganisation.EveryNoteGroupId, group.GetProperty("groupId").GetGuid());
        Assert.Empty(ItemsOf(group));
        Assert.DoesNotContain(Scene.Token, Body(result));
        AssertRead(
            row,
            AssistantFlow.ClientId,
            Tool,
            ("groupIds", [Scene.OtherOrganisation.EveryNoteGroupId]),
            ("commonItemIds", []));
    }

    private static List<JsonElement> GroupsOf(CallToolResult result) =>
        [.. Content(result).GetProperty("groups").EnumerateArray()];

    private static IEnumerable<JsonElement> ItemsOf(JsonElement group) => group.GetProperty("items").EnumerateArray();

    private static List<Guid> GroupIds(IEnumerable<JsonElement> groups) =>
        [.. groups.Select(group => group.GetProperty("groupId").GetGuid())];

    private static List<Guid> ItemIds(JsonElement group) =>
        [.. ItemsOf(group).Select(item => item.GetProperty("itemId").GetGuid())];

    private static JsonElement GroupOf(IEnumerable<JsonElement> groups, Guid id) =>
        groups.Single(group => group.GetProperty("groupId").GetGuid() == id);
}
