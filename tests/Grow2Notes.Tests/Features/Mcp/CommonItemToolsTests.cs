using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>add_common_item</c>, <c>update_common_item</c>, <c>reorder_common_items</c>, <c>archive_common_item</c> and
/// <c>restore_common_item</c> (mcp-server.md §5.2, S07.03.04, S07.03.05) through the real <c>/mcp</c>: what each
/// returns and stores, the event or the one refusal row that each call writes, and the order of the checks, which the
/// app's own endpoints give in the other order. The guards that every tool shares, including who may call each and
/// that another organisation's rows are not found, are in <see cref="ToolMatrixTests"/>. The cases share the app of
/// <see cref="ToolWorld"/> and run one after the other, so each adds the groups and items it changes and never changes
/// a row of the scene. The Every note group is shared by them all, so no case here depends on the items it holds.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class CommonItemToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Add = "add_common_item";
    private const string Update = "update_common_item";
    private const string Reorder = "reorder_common_items";
    private const string Archive = "archive_common_item";
    private const string Restore = "restore_common_item";
    private const string List = "list_common_items";

    private const string HelpText =
        "Changes apply to notes started from now on. Notes already started or submitted keep the wording they were " +
        "written with.";

    private const string GroupNotFound =
        "No common item group has that ID in this organisation. Find it with list_common_items.";

    private const string ItemNotFound =
        "No common item has that ID in this organisation. Find it with list_common_items.";

    private const string ItemOrGroupNotFound =
        "The common item or the group is not in this organisation. Find them with list_common_items.";

    private const string Changed =
        "This common item was changed since you looked it up. Look it up again with list_common_items, then retry.";

    private const string GroupArchived =
        "An archived group's items cannot be added, reworded or moved, and no item can be moved into one. " +
        "Restore the group with restore_common_item_group first.";

    private const string OrderMismatch =
        "itemIds must hold every active item of the group exactly once. " +
        "Look them up again with list_common_items, then retry.";

    // A mark in a refused value, which no refusal may repeat.
    private const string Mark = "Zq9x";

    // The text (with {BEL}, {LF} and {RLO} for characters that a test's text cannot hold) and the app's message.
    public static TheoryData<string, string> TextRefusals { get; } = new()
    {
        { "", "Enter the common item" },
        { "   ", "Enter the common item" },
        { Mark + new string('a', 197), "Common item must be 200 characters or less" },
        { Mark + "{BEL}", "Common item has a control or formatting character" },
        { Mark + "{LF}x", "Common item has a control or formatting character" },
        { Mark + "{RLO}", "Common item has a control or formatting character" },
    };

    public static TheoryData<string> WrongVersions { get; } = ["stale", "missing"];

    public static TheoryData<string> ItemTools { get; } = [Update, Archive, Restore];

    public static TheoryData<string> GroupTools { get; } = [Add, Reorder];

    public static TheoryData<string, string> ArchivedGroupItemUpdates { get; } = MatrixOf(
        ["current", "stale", "missing"], ["its own groupId", "no groupId"]);

    public static TheoryData<string> OrderMismatches { get; } =
        [
            "missing one", "an archived item", "a repeat", "an empty list", "not an ID", "a bidi character",
            "an item of another group",
        ];

    private static readonly string Bidi = char.ConvertFromUtf32(0x202E);

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task An_item_is_added_at_the_end_trimmed_with_its_version_and_the_help_text()
    {
        var group = await Scene.AddGroupAsync();
        var first = await Scene.AddItemAsync(group.Id);
        var text = $"Item {Unique()}";

        var (result, row) = await world.CallAsync(Add, AddCall(group.Id, $"  {text} "));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        var id = content.GetProperty("itemId").GetGuid();
        Assert.Equal(group.Id, content.GetProperty("groupId").GetGuid());
        Assert.Equal(text, Text(content, "text"));
        Assert.False(content.GetProperty("archived").GetBoolean());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, Text(content, "helpText"));
        Assert.Equal(
            ["archived", "changed", "expectedVersion", "groupId", "helpText", "itemId", "text"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal([first.Id, id], await Scene.ActiveItemIdsAsync(group.Id));
        Assert.Equal(Text(content, "expectedVersion"), Text((await StoredAsync(id)).Item, "expectedVersion"));
        AssertChanged(row, AuditEventTypes.CommonItem.Created, Add, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task The_version_that_an_add_returns_works_for_the_next_change()
    {
        var group = await Scene.AddGroupAsync();
        var (added, _) = await world.CallAsync(Add, AddCall(group.Id, $"Item {Unique()}"));
        var content = Content(added);
        var text = $"Reworded {Unique()}";

        var (result, row) = await world.CallAsync(
            Update, ItemCall(content.GetProperty("itemId").GetGuid(), Text(content, "expectedVersion"), text));

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal(text, Text(Content(result), "text"));
        Assert.Equal(HelpText, Text(Content(result), "helpText"));
        AssertChanged(row, AuditEventTypes.CommonItem.Updated, Update, Scene.Organisation.ManagerId);
    }

    [Theory]
    [MemberData(nameof(TextRefusals))]
    public async Task A_refused_text_in_an_add_gets_the_apps_message_without_the_text_and_no_item_is_added(
        string text, string message)
    {
        var group = await Scene.AddGroupAsync();
        await Scene.AddItemAsync(group.Id);
        var before = await Scene.ActiveItemIdsAsync(group.Id);

        var (result, row) = await world.CallAsync(Add, AddCall(group.Id, Expand(text)));

        AssertValidationFailed(result, "text", message);
        Assert.DoesNotContain(Mark, Body(result));
        AssertRefused(row, Add, "validation.failed");
        Assert.Equal(before, await Scene.ActiveItemIdsAsync(group.Id));
    }

    [Theory]
    [MemberData(nameof(TextRefusals))]
    public async Task A_refused_text_in_an_update_gets_the_same_message_and_the_item_is_not_changed(
        string text, string message)
    {
        var item = await Scene.AddItemAsync((await Scene.AddGroupAsync()).Id);

        var (result, row) = await world.CallAsync(
            Update, ItemCall(item.Id, ToolScene.ExpectedVersion(item.ETag), Expand(text)));

        AssertValidationFailed(result, "text", message);
        Assert.DoesNotContain(Mark, Body(result));
        AssertRefused(row, Update, "validation.failed");
        Assert.Equal(item.Text, Text((await StoredAsync(item.Id)).Item, "text"));
    }

    [Fact]
    public async Task An_archived_group_takes_no_item()
    {
        var group = await Scene.AddGroupAsync(archived: true);

        var (result, row) = await world.CallAsync(Add, AddCall(group.Id, $"Item {Unique()}"));

        AssertRefusedWith(result, "config.group_archived", GroupArchived);
        AssertRefused(row, Add, "config.group_archived");
        Assert.Empty((await GroupAsync(group.Id)).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task The_Every_note_group_takes_an_item()
    {
        var text = $"Item {Unique()}";

        var (result, row) = await world.CallAsync(Add, AddCall(Scene.EveryNoteGroupId, text));

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(Scene.EveryNoteGroupId, Content(result).GetProperty("groupId").GetGuid());
        Assert.Equal(text, Text(Content(result), "text"));
        AssertChanged(row, AuditEventTypes.CommonItem.Created, Add, Scene.Organisation.ManagerId);
    }

    [Theory]
    [MemberData(nameof(ItemTools))]
    public async Task An_unknown_or_malformed_item_ID_is_not_found_in_the_words_of_the_tool(string tool)
    {
        foreach (var id in new[] { Guid.NewGuid().ToString(), "not-a-guid" })
        {
            var arguments = ItemCall(Guid.NewGuid(), "AAAAAAAAAAA=", tool == Update ? "Text" : null);
            arguments["itemId"] = id;

            var (result, row) = await world.CallAsync(tool, arguments);

            AssertRefusedWith(result, "not_found", tool == Update ? ItemOrGroupNotFound : ItemNotFound);
            AssertRefused(row, tool, "not_found");
        }
    }

    [Theory]
    [MemberData(nameof(GroupTools))]
    public async Task An_unknown_or_malformed_group_ID_is_not_found_in_the_words_of_the_group(string tool)
    {
        foreach (var id in new[] { Guid.NewGuid().ToString(), "not-a-guid" })
        {
            var arguments = tool == Add ? AddCall(Guid.NewGuid(), "Item") : ReorderCall(Guid.NewGuid(), []);
            arguments["groupId"] = id;

            var (result, row) = await world.CallAsync(tool, arguments);

            AssertRefusedWith(result, "not_found", GroupNotFound);
            AssertRefused(row, tool, "not_found");
        }
    }

    [Fact]
    public async Task An_unknown_group_to_move_to_is_not_found_in_the_words_of_the_update()
    {
        var item = await Scene.AddItemAsync((await Scene.AddGroupAsync()).Id);

        foreach (var id in new[] { Guid.NewGuid().ToString(), "not-a-guid" })
        {
            var arguments = ItemCall(item.Id, ToolScene.ExpectedVersion(item.ETag));
            arguments["groupId"] = id;

            var (result, row) = await world.CallAsync(Update, arguments);

            AssertRefusedWith(result, "not_found", ItemOrGroupNotFound);
            AssertRefused(row, Update, "not_found");
        }
    }

    [Fact]
    public async Task A_moved_item_goes_to_the_end_of_the_new_group_and_the_event_holds_both_groups()
    {
        var from = await Scene.AddGroupAsync();
        var moved = await Scene.AddItemAsync(from.Id);
        var stays = await Scene.AddItemAsync(from.Id);
        var to = await Scene.AddGroupAsync();
        var there = await Scene.AddItemAsync(to.Id);
        var archivedThere = await Scene.AddItemAsync(to.Id, archived: true);

        var (result, row) = await world.CallAsync(
            Update, ItemCall(moved.Id, ToolScene.ExpectedVersion(moved.ETag), groupId: to.Id));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(to.Id, content.GetProperty("groupId").GetGuid());
        Assert.Equal(moved.Text, Text(content, "text"));
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.NotEqual(ToolScene.ExpectedVersion(moved.ETag), Text(content, "expectedVersion"));
        Assert.Equal([stays.Id], await Scene.ActiveItemIdsAsync(from.Id));
        Assert.Equal([there.Id, moved.Id], await Scene.ActiveItemIdsAsync(to.Id));
        Assert.True((await StoredAsync(archivedThere.Id)).Item.GetProperty("archived").GetBoolean());
        AssertChanged(row, AuditEventTypes.CommonItem.Updated, Update, Scene.Organisation.ManagerId);
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details))!;
        Assert.Equal(from.Id, details["old"]!["groupId"]!.GetValue<Guid>());
        Assert.Equal(to.Id, details["new"]!["groupId"]!.GetValue<Guid>());
        Assert.Null(details["old"]!["text"]);
    }

    [Fact]
    public async Task A_move_with_no_text_keeps_the_text_but_the_apps_update_with_no_text_is_still_refused()
    {
        var from = await Scene.AddGroupAsync();
        var item = await Scene.AddItemAsync(from.Id);
        var to = await Scene.AddGroupAsync();

        using var refused = await AppAsync(
            HttpMethod.Put, $"/api/admin/common-items/{item.Id}", new { groupId = to.Id }, item.ETag);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Contains(
            "Enter the common item", await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(from.Id, (await StoredAsync(item.Id)).GroupId);

        var (result, _) = await world.CallAsync(
            Update, ItemCall(item.Id, ToolScene.ExpectedVersion(item.ETag), groupId: to.Id));

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(item.Text, Text(Content(result), "text"));
        Assert.Equal(to.Id, (await StoredAsync(item.Id)).GroupId);
    }

    [Fact]
    public async Task The_items_own_group_is_no_move_and_keeps_its_place()
    {
        var group = await Scene.AddGroupAsync();
        var first = await Scene.AddItemAsync(group.Id);
        var second = await Scene.AddItemAsync(group.Id);
        var order = await Scene.ActiveItemIdsAsync(group.Id);
        var text = $"Reworded {Unique()}";

        var (reworded, rewordRow) = await world.CallAsync(
            Update, ItemCall(first.Id, ToolScene.ExpectedVersion(first.ETag), text, group.Id));

        Assert.NotEqual(true, reworded.IsError);
        Assert.True(Content(reworded).GetProperty("changed").GetBoolean());
        Assert.Equal(text, Text(Content(reworded), "text"));
        Assert.Equal(order, await Scene.ActiveItemIdsAsync(group.Id));
        var details = JsonNode.Parse(Assert.IsType<string>(rewordRow.Details))!;
        Assert.Equal([text], details["new"]!.AsObject().Select(property => property.Value!.ToString()));

        var (same, sameRow) = await world.CallAsync(
            Update, ItemCall(second.Id, ToolScene.ExpectedVersion(second.ETag), groupId: group.Id));

        AssertNothingChanged(
            same,
            sameRow,
            Update,
            second.Id,
            group.Id,
            second.Text,
            archived: false,
            ToolScene.ExpectedVersion(second.ETag));
        Assert.Equal(order, await Scene.ActiveItemIdsAsync(group.Id));
    }

    [Theory]
    [MemberData(nameof(ArchivedGroupItemUpdates))]
    public async Task An_item_of_an_archived_group_is_refused_whatever_the_version_and_is_not_changed(
        string version, string group)
    {
        var archived = await Scene.AddGroupAsync(archived: true);
        var item = await Scene.AddItemAsync(archived.Id);
        var stored = (await StoredAsync(item.Id)).Item.GetRawText();
        var arguments = ItemCall(
            item.Id,
            version switch
            {
                "current" => ToolScene.ExpectedVersion(item.ETag),
                "stale" => "AAAAAAAAAAA=",
                _ => null,
            },
            $"Reworded {Unique()}",
            group == "its own groupId" ? archived.Id : null);

        var (result, row) = await world.CallAsync(Update, arguments);

        AssertRefusedWith(result, "config.group_archived", GroupArchived);
        AssertRefused(row, Update, "config.group_archived");
        Assert.Equal(stored, (await StoredAsync(item.Id)).Item.GetRawText());
    }

    [Fact]
    public async Task A_move_of_an_active_groups_item_into_an_archived_group_is_refused()
    {
        var group = await Scene.AddGroupAsync();
        var item = await Scene.AddItemAsync(group.Id);
        var archived = await Scene.AddGroupAsync(archived: true);

        var (result, row) = await world.CallAsync(
            Update, ItemCall(item.Id, ToolScene.ExpectedVersion(item.ETag), groupId: archived.Id));

        AssertRefusedWith(result, "config.group_archived", GroupArchived);
        AssertRefused(row, Update, "config.group_archived");
        Assert.Equal(group.Id, (await StoredAsync(item.Id)).GroupId);
    }

    [Fact]
    public async Task An_update_that_sends_neither_text_nor_a_group_is_refused_with_no_field()
    {
        var item = await Scene.AddItemAsync((await Scene.AddGroupAsync()).Id);

        var (result, row) = await world.CallAsync(Update, ItemCall(item.Id, ToolScene.ExpectedVersion(item.ETag)));

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("validation.failed", error.GetProperty("code").GetString());
        Assert.Equal("Send at least one of text and groupId.", error.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, error.GetProperty("fields").ValueKind);
        AssertRefused(row, Update, "validation.failed");
    }

    [Theory]
    [InlineData(Update)]
    [InlineData(Archive)]
    [InlineData(Restore)]
    public async Task A_stale_or_missing_version_is_refused_when_a_value_would_change_and_the_item_is_not_changed(
        string tool)
    {
        foreach (var version in new string?[] { "AAAAAAAAAAA=", null })
        {
            var item = await Scene.AddItemAsync((await Scene.AddGroupAsync()).Id, archived: tool == Restore);
            var stored = (await StoredAsync(item.Id)).Item.GetRawText();

            var (result, row) = await world.CallAsync(
                tool, ItemCall(item.Id, version, tool == Update ? $"Reworded {Unique()}" : null));

            Assert.True(result.IsError);
            var error = Content(result).GetProperty("error");
            var (code, message) = version is not null
                ? ("precondition.failed", Changed)
                : ("precondition.required", "Send the expectedVersion that the look-up returned.");
            Assert.Equal(code, error.GetProperty("code").GetString());
            Assert.Equal(message, error.GetProperty("message").GetString());
            AssertRefused(row, tool, code);
            Assert.Equal(stored, (await StoredAsync(item.Id)).Item.GetRawText());
        }
    }

    // Both orders (R13): the tool answers a call that changes nothing before it looks at the version, and the app
    // refuses the same stale version (412) or a missing one (428) at once.
    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task An_update_to_the_stored_text_answers_changed_false_but_the_app_refuses(string version)
    {
        var group = await Scene.AddGroupAsync();
        var item = await Scene.AddItemAsync(group.Id);
        var text = $"Reworded {Unique()}";
        var (first, _) = await world.CallAsync(Update, ItemCall(item.Id, ToolScene.ExpectedVersion(item.ETag), text));
        var current = Text(Content(first), "expectedVersion");

        var (result, row) = await world.CallAsync(
            Update, ItemCall(item.Id, version == "stale" ? ToolScene.ExpectedVersion(item.ETag) : null, text));

        AssertNothingChanged(result, row, Update, item.Id, group.Id, text, archived: false, current);
        using var response = await AppAsync(
            HttpMethod.Put,
            $"/api/admin/common-items/{item.Id}",
            new { text, groupId = group.Id },
            version == "stale" ? item.ETag : null);
        Assert.Equal(Refused(version), response.StatusCode);
        Assert.Equal(current, Text((await StoredAsync(item.Id)).Item, "expectedVersion"));
    }

    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_repeated_archive_answers_changed_false_but_the_app_refuses(string version)
    {
        var group = await Scene.AddGroupAsync();
        var item = await Scene.AddItemAsync(group.Id);
        var (first, _) = await world.CallAsync(Archive, ItemCall(item));
        var current = Text(Content(first), "expectedVersion");

        var (result, row) = await world.CallAsync(
            Archive, ItemCall(item.Id, version == "stale" ? ToolScene.ExpectedVersion(item.ETag) : null));

        AssertNothingChanged(result, row, Archive, item.Id, group.Id, item.Text, archived: true, current);
        using var response = await AppAsync(
            HttpMethod.Post, $"/api/admin/common-items/{item.Id}/archive", null, version == "stale" ? item.ETag : null);
        Assert.Equal(Refused(version), response.StatusCode);
        Assert.Equal(current, Text((await StoredAsync(item.Id)).Item, "expectedVersion"));
    }

    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_repeated_restore_answers_changed_false_but_the_app_refuses(string version)
    {
        var group = await Scene.AddGroupAsync();
        var item = await Scene.AddItemAsync(group.Id, archived: true);
        var (first, _) = await world.CallAsync(Restore, ItemCall(item));
        var current = Text(Content(first), "expectedVersion");

        var (result, row) = await world.CallAsync(
            Restore, ItemCall(item.Id, version == "stale" ? ToolScene.ExpectedVersion(item.ETag) : null));

        AssertNothingChanged(result, row, Restore, item.Id, group.Id, item.Text, archived: false, current);
        using var response = await AppAsync(
            HttpMethod.Post,
            $"/api/admin/common-items/{item.Id}/restore",
            null,
            version == "stale" ? item.ETag : null);
        Assert.Equal(Refused(version), response.StatusCode);
        Assert.Equal(current, Text((await StoredAsync(item.Id)).Item, "expectedVersion"));
    }

    [Fact]
    public async Task An_item_is_archived_on_the_first_call_and_leaves_the_active_items()
    {
        var group = await Scene.AddGroupAsync();
        var kept = await Scene.AddItemAsync(group.Id);
        var item = await Scene.AddItemAsync(group.Id);

        var (result, row) = await world.CallAsync(Archive, ItemCall(item));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(item.Id, content.GetProperty("itemId").GetGuid());
        Assert.Equal(group.Id, content.GetProperty("groupId").GetGuid());
        Assert.True(content.GetProperty("archived").GetBoolean());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, Text(content, "helpText"));
        Assert.Equal(
            ["archived", "changed", "expectedVersion", "groupId", "helpText", "itemId", "text"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(Text(content, "expectedVersion"), Text((await StoredAsync(item.Id)).Item, "expectedVersion"));
        Assert.Equal([kept.Id], await Scene.ActiveItemIdsAsync(group.Id));
        AssertChanged(row, AuditEventTypes.CommonItem.Archived, Archive, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task A_restored_item_goes_to_the_end_of_its_group_and_its_version_works_for_the_next_change()
    {
        var group = await Scene.AddGroupAsync();
        var restored = await Scene.AddItemAsync(group.Id, archived: true);
        await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id);
        var before = await Scene.ActiveItemIdsAsync(group.Id);

        var (result, row) = await world.CallAsync(Restore, ItemCall(restored));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("archived").GetBoolean());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal([.. before, restored.Id], await Scene.ActiveItemIdsAsync(group.Id));
        AssertChanged(row, AuditEventTypes.CommonItem.Restored, Restore, Scene.Organisation.ManagerId);

        var (next, nextRow) = await world.CallAsync(Archive, ItemCall(restored.Id, Text(content, "expectedVersion")));

        Assert.NotEqual(true, next.IsError);
        Assert.True(Content(next).GetProperty("changed").GetBoolean());
        AssertChanged(nextRow, AuditEventTypes.CommonItem.Archived, Archive, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task The_items_of_an_archived_group_can_be_archived_and_restored()
    {
        var group = await Scene.AddGroupAsync(archived: true);
        var item = await Scene.AddItemAsync(group.Id);

        var (archived, _) = await world.CallAsync(Archive, ItemCall(item));
        var (restored, row) = await world.CallAsync(
            Restore, ItemCall(item.Id, Text(Content(archived), "expectedVersion")));

        Assert.True(Content(archived).GetProperty("changed").GetBoolean());
        Assert.True(Content(restored).GetProperty("changed").GetBoolean());
        Assert.False(Content(restored).GetProperty("archived").GetBoolean());
        AssertChanged(row, AuditEventTypes.CommonItem.Restored, Restore, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task A_reorder_puts_the_active_items_in_the_new_order_and_returns_them_with_their_versions()
    {
        var group = await Scene.AddGroupAsync();
        await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id, archived: true);
        var reversed = (await Scene.ActiveItemIdsAsync(group.Id)).AsEnumerable().Reverse().ToList();

        var (result, row) = await world.CallAsync(
            Reorder, ReorderCall(group.Id, reversed.Select(id => id.ToString())));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(group.Id, content.GetProperty("groupId").GetGuid());
        Assert.Equal(HelpText, Text(content, "helpText"));
        Assert.Equal(
            ["changed", "groupId", "helpText", "items"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var items = content.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(reversed, items.Select(item => item.GetProperty("itemId").GetGuid()));
        Assert.All(items, item => Assert.False(item.GetProperty("archived").GetBoolean()));
        Assert.Equal(reversed, await Scene.ActiveItemIdsAsync(group.Id));
        var stored = (await GroupAsync(group.Id)).GetProperty("items").EnumerateArray().ToList();
        foreach (var item in items)
        {
            var itemId = item.GetProperty("itemId").GetGuid();
            Assert.Equal(
                Text(item, "expectedVersion"),
                Text(stored.Single(listed => listed.GetProperty("itemId").GetGuid() == itemId), "expectedVersion"));
        }

        AssertChanged(row, AuditEventTypes.CommonItem.Reordered, Reorder, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task The_stored_order_answers_changed_false_and_the_audit_row_holds_every_ID_returned()
    {
        var group = await Scene.AddGroupAsync();
        await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id);
        var ids = await Scene.ActiveItemIdsAsync(group.Id);

        var (result, row) = await world.CallAsync(Reorder, ReorderCall(group.Id, ids.Select(id => id.ToString())));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("changed").GetBoolean());
        Assert.Equal(
            ids, content.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("itemId").GetGuid()));
        AssertRead(row, AssistantFlow.ClientId, Reorder, ("groupIds", [group.Id]), ("commonItemIds", [.. ids]));
    }

    [Fact]
    public async Task The_items_of_an_archived_group_can_be_reordered()
    {
        var group = await Scene.AddGroupAsync(archived: true);
        await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id);
        var reversed = (await Scene.ActiveItemIdsAsync(group.Id)).AsEnumerable().Reverse().ToList();

        var (result, row) = await world.CallAsync(
            Reorder, ReorderCall(group.Id, reversed.Select(id => id.ToString())));

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal(reversed, await Scene.ActiveItemIdsAsync(group.Id));
        AssertChanged(row, AuditEventTypes.CommonItem.Reordered, Reorder, Scene.Organisation.ManagerId);
    }

    [Theory]
    [MemberData(nameof(OrderMismatches))]
    public async Task A_set_of_IDs_that_is_not_the_active_items_of_the_group_is_refused_and_nothing_is_moved(
        string kind)
    {
        var group = await Scene.AddGroupAsync();
        await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id);
        var archived = await Scene.AddItemAsync(group.Id, archived: true);
        var ids = (await Scene.ActiveItemIdsAsync(group.Id)).Select(id => id.ToString()).ToList();
        List<string> sent = kind switch
        {
            "missing one" => ids[1..],
            "an archived item" => [.. ids, archived.Id.ToString()],
            "a repeat" => [.. ids, ids[0]],
            "an empty list" => [],
            "not an ID" => [.. ids[1..], "not-a-guid"],
            "a bidi character" => [.. ids[1..], ids[0] + Bidi],
            _ => [.. ids, Scene.ArchivedGroupItem.Id.ToString()],
        };
        var before = await Scene.ActiveItemIdsAsync(group.Id);

        var (result, row) = await world.CallAsync(Reorder, ReorderCall(group.Id, sent));

        AssertRefusedWith(result, "config.order_mismatch", OrderMismatch);
        AssertRefused(row, Reorder, "config.order_mismatch");
        Assert.Equal(before, await Scene.ActiveItemIdsAsync(group.Id));
    }

    [Fact]
    public async Task A_Support_person_can_make_each_change()
    {
        const ToolCaller support = ToolCaller.Support;
        var supportId = Scene.Organisation.SupportId;
        var group = await Scene.AddGroupAsync();
        var other = await Scene.AddGroupAsync();

        var (added, addRow) = await world.CallAsync(Add, AddCall(group.Id, $"Item {Unique()}"), support);
        var id = Content(added).GetProperty("itemId").GetGuid();
        AssertChanged(addRow, AuditEventTypes.CommonItem.Created, Add, supportId);

        var (updated, updateRow) = await world.CallAsync(
            Update, ItemCall(id, Text(Content(added), "expectedVersion"), $"Reworded {Unique()}", other.Id), support);
        Assert.True(Content(updated).GetProperty("changed").GetBoolean());
        AssertChanged(updateRow, AuditEventTypes.CommonItem.Updated, Update, supportId);

        await Scene.AddItemAsync(other.Id);
        var ids = await Scene.ActiveItemIdsAsync(other.Id);
        var (reordered, reorderRow) = await world.CallAsync(
            Reorder, ReorderCall(other.Id, ids.AsEnumerable().Reverse().Select(item => item.ToString())), support);
        Assert.True(Content(reordered).GetProperty("changed").GetBoolean());
        AssertChanged(reorderRow, AuditEventTypes.CommonItem.Reordered, Reorder, supportId);

        var current = Content(reordered).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("itemId").GetGuid() == id);
        var (archived, archiveRow) = await world.CallAsync(
            Archive, ItemCall(id, Text(current, "expectedVersion")), support);
        Assert.True(Content(archived).GetProperty("changed").GetBoolean());
        AssertChanged(archiveRow, AuditEventTypes.CommonItem.Archived, Archive, supportId);

        var (restored, restoreRow) = await world.CallAsync(
            Restore, ItemCall(id, Text(Content(archived), "expectedVersion")), support);
        Assert.True(Content(restored).GetProperty("changed").GetBoolean());
        Assert.False(Content(restored).GetProperty("archived").GetBoolean());
        AssertChanged(restoreRow, AuditEventTypes.CommonItem.Restored, Restore, supportId);
    }

    private static TheoryData<string, string> MatrixOf(string[] first, string[] second)
    {
        var data = new TheoryData<string, string>();
        foreach (var one in first)
        {
            foreach (var other in second)
            {
                data.Add(one, other);
            }
        }

        return data;
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static string Expand(string value) =>
        value.Replace("{BEL}", "\u0007").Replace("{RLO}", Bidi).Replace("{LF}", "\n");

    private static string Text(JsonElement element, string property) => element.GetProperty(property).GetString()!;

    private static HttpStatusCode Refused(string version) =>
        version == "stale" ? HttpStatusCode.PreconditionFailed : HttpStatusCode.PreconditionRequired;

    private static JsonObject AddCall(Guid groupId, string text) => new()
    {
        ["groupId"] = groupId.ToString(),
        ["text"] = text,
    };

    // A change to an item that sends the version it is at.
    private static JsonObject ItemCall(SceneItem item) => ItemCall(item.Id, ToolScene.ExpectedVersion(item.ETag));

    // A change to an item that sends the version given, if any, and the text and group if they are given.
    private static JsonObject ItemCall(Guid itemId, string? version, string? text = null, Guid? groupId = null)
    {
        var arguments = new JsonObject { ["itemId"] = itemId.ToString() };
        if (version is not null)
        {
            arguments["expectedVersion"] = version;
        }

        if (text is not null)
        {
            arguments["text"] = text;
        }

        if (groupId is not null)
        {
            arguments["groupId"] = groupId.Value.ToString();
        }

        return arguments;
    }

    private static JsonObject ReorderCall(Guid groupId, IEnumerable<string> itemIds) => new()
    {
        ["groupId"] = groupId.ToString(),
        ["itemIds"] = new JsonArray([.. itemIds.Select(id => (JsonNode?)JsonValue.Create(id))]),
    };

    private static void AssertRefusedWith(CallToolResult result, string code, string message)
    {
        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
    }

    // The answer of a change that changed nothing: the item as it is, the version it is at, and the one mcp.read row
    // that names the item and its group.
    private static void AssertNothingChanged(
        CallToolResult result,
        AuditRow row,
        string tool,
        Guid itemId,
        Guid groupId,
        string text,
        bool archived,
        string version)
    {
        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("changed").GetBoolean());
        Assert.Equal(itemId, content.GetProperty("itemId").GetGuid());
        Assert.Equal(groupId, content.GetProperty("groupId").GetGuid());
        Assert.Equal(text, Text(content, "text"));
        Assert.Equal(archived, content.GetProperty("archived").GetBoolean());
        Assert.Equal(version, Text(content, "expectedVersion"));
        AssertRead(row, AssistantFlow.ClientId, tool, ("groupIds", [groupId]), ("commonItemIds", [itemId]));
    }

    // As the SPA calls the app: over a session of the manager, with a token, and the If-Match given, if any.
    private async Task<HttpResponseMessage> AppAsync(HttpMethod method, string path, object? body, string? ifMatch)
    {
        using var browser = await Flow.SignInAsync(Scene.Organisation.ManagerId);
        var token = await browser.FetchTokenAsync();
        return method == HttpMethod.Put
            ? await browser.PutWithTokenAsync(path, token, body!, ifMatch)
            : await browser.PostWithTokenAsync(path, token, body, ifMatch);
    }

    private async Task<List<JsonElement>> ListAsync()
    {
        var (result, _) = await world.CallAsync(List, new() { ["includeArchived"] = true });
        return [.. Content(result).GetProperty("groups").EnumerateArray()];
    }

    private async Task<JsonElement> GroupAsync(Guid id) =>
        (await ListAsync()).Single(group => group.GetProperty("groupId").GetGuid() == id);

    // The item as list_common_items gives it, and the group it is in.
    private async Task<(Guid GroupId, JsonElement Item)> StoredAsync(Guid itemId)
    {
        foreach (var group in await ListAsync())
        {
            foreach (var item in group.GetProperty("items").EnumerateArray())
            {
                if (item.GetProperty("itemId").GetGuid() == itemId)
                {
                    return (group.GetProperty("groupId").GetGuid(), item);
                }
            }
        }

        throw new InvalidOperationException("The item is in no group.");
    }
}
