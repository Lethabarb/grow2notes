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
/// <c>add_common_item_group</c>, <c>rename_common_item_group</c>, <c>reorder_common_item_groups</c>,
/// <c>archive_common_item_group</c> and <c>restore_common_item_group</c> (mcp-server.md §5.2, S07.03.04, S07.03.05)
/// through the real <c>/mcp</c>: what each returns and stores, the event or the one refusal row that each call writes,
/// and the order of the checks. The guards that every tool shares, including who may call each and that another
/// organisation's rows are not found, are in <see cref="ToolMatrixTests"/>. The cases share the app of
/// <see cref="ToolWorld"/> and run one after the other, so each adds the groups it changes and never changes a group of
/// the scene. A reorder sends every active group and so moves them all: no case here depends on where a group of the
/// scene is, only on where the groups it added are.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class CommonItemGroupToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Add = "add_common_item_group";
    private const string Rename = "rename_common_item_group";
    private const string Reorder = "reorder_common_item_groups";
    private const string Archive = "archive_common_item_group";
    private const string Restore = "restore_common_item_group";
    private const string List = "list_common_items";

    private const string HelpText =
        "Changes apply to notes started from now on. Notes already started or submitted keep the wording they were " +
        "written with.";

    private const string EmptyGroupLine = "The new group is empty. It does not show on notes until it has an item.";

    private const string NotFound =
        "No common item group has that ID in this organisation. Find it with list_common_items.";

    private const string Changed =
        "This group was changed since you looked it up. Look it up again with list_common_items, then retry.";

    private const string EveryNoteFixed =
        "The Every note group cannot be renamed, archived or restored, and always comes first. Choose another group.";

    private const string OrderMismatch =
        "groupIds must hold every active group except Every note exactly once. " +
        "Look them up again with list_common_items, then retry.";

    // A mark in a refused value, which no refusal may repeat.
    private const string Mark = "Zq9x";

    // The name (with {BEL}, {LF} and {RLO} for characters that a test's text cannot hold) and the app's message.
    public static TheoryData<string, string> NameRefusals { get; } = new()
    {
        { "", "Enter the group name" },
        { "   ", "Enter the group name" },
        { Mark + new string('a', 197), "Group name must be 200 characters or less" },
        { Mark + "{BEL}", "Group name has a control or formatting character" },
        { Mark + "{LF}x", "Group name has a control or formatting character" },
        { Mark + "{RLO}", "Group name has a control or formatting character" },
    };

    public static TheoryData<string, string> EveryNoteChanges { get; } = MatrixOf(
        [Rename, Archive, Restore], ["current", "stale", "missing"]);

    public static TheoryData<string, string> WrongVersionsOfChanges { get; } = MatrixOf(
        [Rename, Archive, Restore], ["stale", "missing"]);

    public static TheoryData<string> WrongVersions { get; } = ["stale", "missing"];

    public static TheoryData<string> OrderMismatches { get; } =
        [
            "missing one", "an archived group", "a repeat", "an empty list", "not an ID", "a bidi character",
            "Every note",
        ];

    private static readonly string Bidi = char.ConvertFromUtf32(0x202E);

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task A_group_is_added_empty_at_the_end_and_the_organisation_keeps_its_one_Every_note()
    {
        var before = await Scene.ActiveGroupIdsAsync();
        var name = $"Group {Unique()}";

        var (result, row) = await world.CallAsync(Add, new() { ["name"] = $"  {name} " });

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        var id = content.GetProperty("groupId").GetGuid();
        Assert.Equal(name, content.GetProperty("name").GetString());
        Assert.False(content.GetProperty("archived").GetBoolean());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal($"{EmptyGroupLine} {HelpText}", content.GetProperty("helpText").GetString());
        Assert.Equal(
            ["archived", "changed", "expectedVersion", "groupId", "helpText", "name"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal([.. before, id], await Scene.ActiveGroupIdsAsync());
        var stored = await GroupAsync(id);
        Assert.Equal(content.GetProperty("expectedVersion").GetString(), Text(stored, "expectedVersion"));
        Assert.Empty(stored.GetProperty("items").EnumerateArray());
        Assert.Single(await ListAsync(includeArchived: true), group => group.GetProperty("isEveryNote").GetBoolean());
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Created, Add, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task The_version_that_an_add_returns_works_for_a_rename()
    {
        var (added, _) = await world.CallAsync(Add, new() { ["name"] = $"Group {Unique()}" });
        var content = Content(added);
        var renamed = $"Renamed {Unique()}";

        var (result, row) = await world.CallAsync(
            Rename, GroupCall(content.GetProperty("groupId").GetGuid(), Text(content, "expectedVersion"), renamed));

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal(renamed, Content(result).GetProperty("name").GetString());
        Assert.Equal(HelpText, Content(result).GetProperty("helpText").GetString());
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Updated, Rename, Scene.Organisation.ManagerId);
    }

    [Theory]
    [MemberData(nameof(NameRefusals))]
    public async Task A_refused_name_gets_the_apps_message_without_the_name_and_no_group_is_added(
        string name, string message)
    {
        var before = await Scene.ActiveGroupIdsAsync();

        var (result, row) = await world.CallAsync(Add, new() { ["name"] = Expand(name) });

        AssertValidationFailed(result, "name", message);
        Assert.DoesNotContain(Mark, Body(result));
        AssertRefused(row, Add, "validation.failed");
        Assert.Equal(before, await Scene.ActiveGroupIdsAsync());
    }

    [Theory]
    [MemberData(nameof(NameRefusals))]
    public async Task A_refused_name_in_a_rename_gets_the_same_message_and_the_group_is_not_changed(
        string name, string message)
    {
        var group = await Scene.AddGroupAsync();

        var (result, row) = await world.CallAsync(Rename, GroupCall(group, Expand(name)));

        AssertValidationFailed(result, "name", message);
        Assert.DoesNotContain(Mark, Body(result));
        AssertRefused(row, Rename, "validation.failed");
        Assert.Equal(group.Name, Text(await GroupAsync(group.Id), "name"));
    }

    [Fact]
    public async Task A_group_is_renamed_with_trimmed_name_and_the_new_version()
    {
        var group = await Scene.AddGroupAsync();
        var name = $"Renamed {Unique()}";

        var (result, row) = await world.CallAsync(Rename, GroupCall(group, $" {name}  "));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(group.Id, content.GetProperty("groupId").GetGuid());
        Assert.Equal(name, content.GetProperty("name").GetString());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.NotEqual(ToolScene.ExpectedVersion(group.ETag), Text(content, "expectedVersion"));
        Assert.Equal(Text(await GroupAsync(group.Id), "expectedVersion"), Text(content, "expectedVersion"));
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Updated, Rename, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task An_archived_group_can_be_renamed()
    {
        var group = await Scene.AddGroupAsync(archived: true);
        var name = $"Renamed {Unique()}";

        var (result, row) = await world.CallAsync(Rename, GroupCall(group, name));

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        Assert.True(Content(result).GetProperty("archived").GetBoolean());
        Assert.Equal(name, Text(await GroupAsync(group.Id), "name"));
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Updated, Rename, Scene.Organisation.ManagerId);
    }

    [Theory]
    [MemberData(nameof(EveryNoteChanges))]
    public async Task Every_note_is_refused_whatever_the_version(string tool, string version)
    {
        var everyNote = Assert.Single(
            await ListAsync(includeArchived: true), group => group.GetProperty("isEveryNote").GetBoolean());
        var current = Text(everyNote, "expectedVersion");
        var arguments = GroupCall(
            Scene.EveryNoteGroupId,
            version switch
            {
                "current" => current,
                "stale" => "AAAAAAAAAAA=",
                _ => null,
            },
            tool == Rename ? $"Renamed {Unique()}" : null);

        var (result, row) = await world.CallAsync(tool, arguments);

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("config.every_note_fixed", error.GetProperty("code").GetString());
        Assert.Equal(EveryNoteFixed, error.GetProperty("message").GetString());
        AssertRefused(row, tool, "config.every_note_fixed");
        var after = Assert.Single(
            await ListAsync(includeArchived: true), group => group.GetProperty("isEveryNote").GetBoolean());
        Assert.Equal(everyNote.GetRawText(), after.GetRawText());
    }

    [Theory]
    [MemberData(nameof(WrongVersionsOfChanges))]
    public async Task A_wrong_version_is_refused_when_a_value_would_change_and_the_group_is_not_changed(
        string tool, string version)
    {
        var group = await Scene.AddGroupAsync(archived: tool == Restore);
        var stored = (await GroupAsync(group.Id)).GetRawText();
        var arguments = GroupCall(
            group.Id, version == "stale" ? "AAAAAAAAAAA=" : null, tool == Rename ? $"Renamed {Unique()}" : null);

        var (result, row) = await world.CallAsync(tool, arguments);

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        var (code, message) = version == "stale"
            ? ("precondition.failed", Changed)
            : ("precondition.required", "Send the expectedVersion that the look-up returned.");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
        AssertRefused(row, tool, code);
        Assert.Equal(stored, (await GroupAsync(group.Id)).GetRawText());
    }

    [Theory]
    [InlineData(Rename)]
    [InlineData(Archive)]
    [InlineData(Restore)]
    public async Task An_unknown_or_malformed_ID_is_not_found_in_the_words_of_the_tool(string tool)
    {
        foreach (var id in new[] { Guid.NewGuid().ToString(), "not-a-guid" })
        {
            var arguments = GroupCall(Guid.NewGuid(), "AAAAAAAAAAA=", tool == Rename ? "Name" : null);
            arguments["groupId"] = id;

            var (result, row) = await world.CallAsync(tool, arguments);

            Assert.True(result.IsError);
            Assert.Equal("not_found", Content(result).GetProperty("error").GetProperty("code").GetString());
            Assert.Equal(NotFound, Content(result).GetProperty("error").GetProperty("message").GetString());
            AssertRefused(row, tool, "not_found");
        }
    }

    // Both orders (R13): the tool answers a call that changes nothing before it looks at the version, and the app
    // refuses the same stale version (412) or a missing one (428) at once.
    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_rename_to_the_stored_name_answers_changed_false_but_the_app_refuses(string version)
    {
        var group = await Scene.AddGroupAsync();
        var name = $"Renamed {Unique()}";
        var (first, _) = await world.CallAsync(Rename, GroupCall(group, name));
        var current = Text(Content(first), "expectedVersion");

        var (result, row) = await world.CallAsync(
            Rename, GroupCall(group.Id, version == "stale" ? ToolScene.ExpectedVersion(group.ETag) : null, name));

        AssertNothingChanged(result, row, Rename, group.Id, name, archived: false, current);
        Assert.Equal(
            Refused(version),
            await AppAsync(HttpMethod.Put, $"/api/admin/common-item-groups/{group.Id}", new { name }, group, version));
        Assert.Equal(current, Text(await GroupAsync(group.Id), "expectedVersion"));
    }

    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_repeated_archive_answers_changed_false_but_the_app_refuses(string version)
    {
        var group = await Scene.AddGroupAsync();
        var (first, _) = await world.CallAsync(Archive, GroupCall(group));
        var current = Text(Content(first), "expectedVersion");

        var (result, row) = await world.CallAsync(
            Archive, GroupCall(group.Id, version == "stale" ? ToolScene.ExpectedVersion(group.ETag) : null));

        AssertNothingChanged(result, row, Archive, group.Id, group.Name, archived: true, current);
        Assert.Equal(
            Refused(version),
            await AppAsync(
                HttpMethod.Post, $"/api/admin/common-item-groups/{group.Id}/archive", null, group, version));
        Assert.Equal(current, Text(await GroupAsync(group.Id), "expectedVersion"));
    }

    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_repeated_restore_answers_changed_false_but_the_app_refuses(string version)
    {
        var group = await Scene.AddGroupAsync(archived: true);
        var (first, _) = await world.CallAsync(Restore, GroupCall(group));
        var current = Text(Content(first), "expectedVersion");

        var (result, row) = await world.CallAsync(
            Restore, GroupCall(group.Id, version == "stale" ? ToolScene.ExpectedVersion(group.ETag) : null));

        AssertNothingChanged(result, row, Restore, group.Id, group.Name, archived: false, current);
        Assert.Equal(
            Refused(version),
            await AppAsync(
                HttpMethod.Post, $"/api/admin/common-item-groups/{group.Id}/restore", null, group, version));
        Assert.Equal(current, Text(await GroupAsync(group.Id), "expectedVersion"));
    }

    [Fact]
    public async Task An_archived_group_keeps_its_items_and_leaves_the_default_list()
    {
        var group = await Scene.AddGroupAsync();
        var item = await Scene.AddItemAsync(group.Id);

        var (result, row) = await world.CallAsync(Archive, GroupCall(group));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.True(content.GetProperty("archived").GetBoolean());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        Assert.Equal(
            ["archived", "changed", "expectedVersion", "groupId", "helpText", "name"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            await ListAsync(includeArchived: false), listed => listed.GetProperty("groupId").GetGuid() == group.Id);
        var archived = await GroupAsync(group.Id);
        Assert.Equal(
            [item.Id], archived.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()));
        Assert.False(archived.GetProperty("items")[0].GetProperty("archived").GetBoolean());
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Archived, Archive, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task A_restored_group_goes_to_the_end_with_its_active_items_and_its_version_works_for_the_next_change()
    {
        var group = await Scene.AddGroupAsync(archived: true);
        var active = await Scene.AddItemAsync(group.Id);
        await Scene.AddItemAsync(group.Id, archived: true);
        await Scene.AddGroupAsync();
        var before = await Scene.ActiveGroupIdsAsync();

        var (result, row) = await world.CallAsync(Restore, GroupCall(group));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("archived").GetBoolean());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal([.. before, group.Id], await Scene.ActiveGroupIdsAsync());
        var restored = (await ListAsync(includeArchived: false))
            .Single(listed => listed.GetProperty("groupId").GetGuid() == group.Id);
        Assert.Equal(
            [active.Id],
            restored.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()));
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Restored, Restore, Scene.Organisation.ManagerId);

        var (next, nextRow) = await world.CallAsync(Archive, GroupCall(group.Id, Text(content, "expectedVersion")));

        Assert.NotEqual(true, next.IsError);
        Assert.True(Content(next).GetProperty("changed").GetBoolean());
        AssertChanged(nextRow, AuditEventTypes.CommonItemGroup.Archived, Archive, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task A_reorder_keeps_Every_note_first_and_returns_the_list_that_list_common_items_gives()
    {
        await Scene.AddGroupAsync();
        await Scene.AddGroupAsync();
        var ids = await Scene.ActiveGroupIdsAsync();
        var reversed = ids.AsEnumerable().Reverse().ToList();

        var (result, row) = await world.CallAsync(Reorder, GroupIdsCall(reversed));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        Assert.Equal(
            ["changed", "groups", "helpText"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var groups = content.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(Scene.EveryNoteGroupId, groups[0].GetProperty("groupId").GetGuid());
        Assert.True(groups[0].GetProperty("isEveryNote").GetBoolean());
        Assert.Equal(reversed, groups.Skip(1).Select(group => group.GetProperty("groupId").GetGuid()));
        Assert.Equal(reversed, await Scene.ActiveGroupIdsAsync());
        Assert.Equal(
            JsonSerializer.Serialize(await ListAsync(includeArchived: false)),
            JsonSerializer.Serialize(groups));
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Reordered, Reorder, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task The_stored_order_answers_changed_false_and_the_audit_row_holds_every_ID_returned()
    {
        var (result, row) = await world.CallAsync(Reorder, GroupIdsCall(await Scene.ActiveGroupIdsAsync()));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("changed").GetBoolean());
        var groups = content.GetProperty("groups").EnumerateArray().ToList();
        AssertRead(
            row,
            AssistantFlow.ClientId,
            Reorder,
            ("groupIds", [.. groups.Select(group => group.GetProperty("groupId").GetGuid())]),
            (
                "commonItemIds",
                [
                    .. groups.SelectMany(group => group.GetProperty("items").EnumerateArray())
                        .Select(item => item.GetProperty("itemId").GetGuid()),
                ]));
    }

    [Fact]
    public async Task The_versions_that_a_reorder_returns_work_for_a_following_archive()
    {
        var first = await Scene.AddGroupAsync();
        var second = await Scene.AddGroupAsync();
        var ids = await Scene.ActiveGroupIdsAsync();
        var rotated = ids.Skip(1).Append(ids[0]).ToList();
        var (reordered, _) = await world.CallAsync(Reorder, GroupIdsCall(rotated));
        var returned = Content(reordered).GetProperty("groups").EnumerateArray()
            .Single(group => group.GetProperty("groupId").GetGuid() == second.Id);

        var (result, row) = await world.CallAsync(Archive, GroupCall(second.Id, Text(returned, "expectedVersion")));

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        AssertChanged(row, AuditEventTypes.CommonItemGroup.Archived, Archive, Scene.Organisation.ManagerId);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [MemberData(nameof(OrderMismatches))]
    public async Task A_set_of_IDs_that_is_not_the_active_groups_except_Every_note_is_refused_and_nothing_is_moved(
        string kind)
    {
        await Scene.AddGroupAsync();
        await Scene.AddGroupAsync();
        var archived = await Scene.AddGroupAsync(archived: true);
        var ids = (await Scene.ActiveGroupIdsAsync()).Select(id => id.ToString()).ToList();
        List<string> sent = kind switch
        {
            "missing one" => ids[1..],
            "an archived group" => [.. ids, archived.Id.ToString()],
            "a repeat" => [.. ids, ids[0]],
            "an empty list" => [],
            "not an ID" => [.. ids[1..], "not-a-guid"],
            "a bidi character" => [.. ids[1..], ids[0] + Bidi],
            _ => [Scene.EveryNoteGroupId.ToString(), .. ids],
        };
        var before = await Scene.ActiveGroupIdsAsync();

        var (result, row) = await world.CallAsync(Reorder, GroupIdsCall([.. sent]));

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("config.order_mismatch", error.GetProperty("code").GetString());
        Assert.Equal(OrderMismatch, error.GetProperty("message").GetString());
        AssertRefused(row, Reorder, "config.order_mismatch");
        Assert.Equal(before, await Scene.ActiveGroupIdsAsync());
    }

    [Fact]
    public async Task A_Support_person_can_make_each_change()
    {
        const ToolCaller support = ToolCaller.Support;
        var supportId = Scene.Organisation.SupportId;

        var (added, addRow) = await world.CallAsync(Add, new() { ["name"] = $"Group {Unique()}" }, support);
        var id = Content(added).GetProperty("groupId").GetGuid();
        AssertChanged(addRow, AuditEventTypes.CommonItemGroup.Created, Add, supportId);

        var (renamed, renameRow) = await world.CallAsync(
            Rename, GroupCall(id, Text(Content(added), "expectedVersion"), $"Renamed {Unique()}"), support);
        Assert.True(Content(renamed).GetProperty("changed").GetBoolean());
        AssertChanged(renameRow, AuditEventTypes.CommonItemGroup.Updated, Rename, supportId);

        await Scene.AddGroupAsync();
        var ids = await Scene.ActiveGroupIdsAsync();
        var (reordered, reorderRow) = await world.CallAsync(
            Reorder, GroupIdsCall([.. ids.Skip(1).Append(ids[0])]), support);
        Assert.True(Content(reordered).GetProperty("changed").GetBoolean());
        AssertChanged(reorderRow, AuditEventTypes.CommonItemGroup.Reordered, Reorder, supportId);

        var current = Content(reordered).GetProperty("groups").EnumerateArray()
            .Single(group => group.GetProperty("groupId").GetGuid() == id);
        var (archived, archiveRow) = await world.CallAsync(
            Archive, GroupCall(id, Text(current, "expectedVersion")), support);
        Assert.True(Content(archived).GetProperty("changed").GetBoolean());
        AssertChanged(archiveRow, AuditEventTypes.CommonItemGroup.Archived, Archive, supportId);

        var (restored, restoreRow) = await world.CallAsync(
            Restore, GroupCall(id, Text(Content(archived), "expectedVersion")), support);
        Assert.True(Content(restored).GetProperty("changed").GetBoolean());
        Assert.False(Content(restored).GetProperty("archived").GetBoolean());
        AssertChanged(restoreRow, AuditEventTypes.CommonItemGroup.Restored, Restore, supportId);
    }

    private static TheoryData<string, string> MatrixOf(string[] tools, string[] versions)
    {
        var data = new TheoryData<string, string>();
        foreach (var tool in tools)
        {
            foreach (var version in versions)
            {
                data.Add(tool, version);
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

    // A change to a group that sends the version it is at, and the name if one is given.
    private static JsonObject GroupCall(SceneGroup group, string? name = null) =>
        GroupCall(group.Id, ToolScene.ExpectedVersion(group.ETag), name);

    // A change to a group that sends the version given, if any, and the name if one is given.
    private static JsonObject GroupCall(Guid groupId, string? version, string? name = null)
    {
        var arguments = new JsonObject { ["groupId"] = groupId.ToString() };
        if (version is not null)
        {
            arguments["expectedVersion"] = version;
        }

        if (name is not null)
        {
            arguments["name"] = name;
        }

        return arguments;
    }

    private static JsonObject GroupIdsCall(IEnumerable<Guid> groupIds) =>
        GroupIdsCall(groupIds.Select(id => id.ToString()));

    private static JsonObject GroupIdsCall(IEnumerable<string> groupIds) => new()
    {
        ["groupIds"] = new JsonArray([.. groupIds.Select(id => (JsonNode?)JsonValue.Create(id))]),
    };

    // The answer of a change that changed nothing: the group as it is, the version it is at, and the one mcp.read row
    // that names the group.
    private static void AssertNothingChanged(
        CallToolResult result, AuditRow row, string tool, Guid groupId, string name, bool archived, string version)
    {
        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("changed").GetBoolean());
        Assert.Equal(groupId, content.GetProperty("groupId").GetGuid());
        Assert.Equal(name, Text(content, "name"));
        Assert.Equal(archived, content.GetProperty("archived").GetBoolean());
        Assert.Equal(version, Text(content, "expectedVersion"));
        AssertRead(row, AssistantFlow.ClientId, tool, ("groupIds", [groupId]));
    }

    private async Task<HttpStatusCode> AppAsync(
        HttpMethod method, string path, object? body, SceneGroup group, string version)
    {
        using var browser = await Flow.SignInAsync(Scene.Organisation.ManagerId);
        var token = await browser.FetchTokenAsync();
        var ifMatch = version == "stale" ? group.ETag : null;
        using var response = method == HttpMethod.Put
            ? await browser.PutWithTokenAsync(path, token, body!, ifMatch)
            : await browser.PostWithTokenAsync(path, token, body, ifMatch);
        return response.StatusCode;
    }

    private async Task<List<JsonElement>> ListAsync(bool includeArchived)
    {
        var (result, _) = await world.CallAsync(List, new() { ["includeArchived"] = includeArchived });
        return [.. Content(result).GetProperty("groups").EnumerateArray()];
    }

    private async Task<JsonElement> GroupAsync(Guid id) =>
        (await ListAsync(includeArchived: true)).Single(group => group.GetProperty("groupId").GetGuid() == id);
}
