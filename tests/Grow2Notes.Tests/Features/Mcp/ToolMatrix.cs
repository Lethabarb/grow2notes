using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// A call that a tool must refuse, and the code it refuses with.
/// </summary>
/// <param name="Name">What the case is, for the name of the test.</param>
/// <param name="Arguments">The arguments of the call, in the scene.</param>
/// <param name="Code">The code of the one <c>mcp.refused</c> row the call writes.</param>
internal sealed record ToolErrorCase(string Name, Func<ToolScene, JsonObject> Arguments, string Code);

/// <summary>What the tests of every tool need to know about one tool.</summary>
/// <param name="Name">The tool's name, as <c>tools/list</c> gives it.</param>
/// <param name="ValidArguments">
/// Arguments that succeed in the scene's first organisation, and hold something. A tool that changes a row seeds one
/// of its own, without an audit row, which is why this is asynchronous.
/// </param>
/// <param name="ErrorCases">The calls it refuses, with the code of each.</param>
/// <param name="OtherOrganisationCode">
/// The code the tool refuses the valid arguments with when a manager of another organisation sends them, or null when
/// the tool succeeds and returns nothing of the first organisation.
/// </param>
/// <param name="Event">
/// The audit event type that the valid arguments make the tool write, or null for a look-up.
/// </param>
internal sealed record ToolRow(
    string Name,
    Func<ToolScene, Task<JsonObject>> ValidArguments,
    IReadOnlyList<ToolErrorCase> ErrorCases,
    string? OtherOrganisationCode,
    string? Event);

/// <summary>
/// One row for each tool the app lists, which <see cref="ToolMatrixTests"/> holds to what every tool must do, and the
/// contract rules and the canary reuse. A tool with no row fails the build; a task that adds a tool adds its row, its
/// handlers to the architecture rule, and its entry in tools-list.json.
/// </summary>
internal static class ToolMatrix
{
    public static IReadOnlyList<ToolRow> Rows { get; } =
    [
        new(
            "find_participants",
            scene => Task.FromResult(new JsonObject { ["search"] = scene.FamilyName }),
            [
                new("a short search", _ => new() { ["search"] = "J" }, "validation.failed"),
                new(
                    "a control character",
                    scene => new() { ["search"] = scene.FamilyName + "\u0007" },
                    "validation.failed"),
                new("too long", _ => new() { ["search"] = new string('a', 101) }, "validation.failed"),
                new("no search", _ => new(), "unexpected"),
                new("a null search", _ => new() { ["search"] = null }, "unexpected"),
                new(
                    "includeArchived of the wrong type",
                    scene => new() { ["search"] = scene.FamilyName, ["includeArchived"] = scene.FamilyName },
                    "unexpected"),
            ],
            OtherOrganisationCode: null,
            Event: null),
        new(
            "get_participant",
            scene => Task.FromResult(new JsonObject { ["participantId"] = scene.Participant.Id.ToString() }),
            [
                new("an unknown ID", _ => new() { ["participantId"] = Guid.NewGuid().ToString() }, "not_found"),
                new("not an ID", _ => new() { ["participantId"] = "not-a-guid" }, "not_found"),
                new("no ID", _ => new(), "unexpected"),
            ],
            OtherOrganisationCode: "not_found",
            Event: null),
        new(
            "add_participant",
            scene => Task.FromResult(NewParticipantCall($"Ann{Unique()}", $"Rowe{Unique()}", "1987-04-09")),
            [
                new(
                    "a given name of 101 characters",
                    _ => NewParticipantCall(new string('a', 101), "Rowe", "1987-04-09"),
                    "validation.failed"),
                new(
                    "a family name with a control character",
                    scene => NewParticipantCall("Ann", $"Rowe\u0007{scene.Token}", "1987-04-09"),
                    "validation.failed"),
                new("an empty given name", _ => NewParticipantCall("", "Rowe", "1987-04-09"), "validation.failed"),
                new("an empty family name", _ => NewParticipantCall("Ann", "", "1987-04-09"), "validation.failed"),
                new("a blank date", _ => NewParticipantCall("Ann", "Rowe", " "), "validation.failed"),
                new("a date with no padding", _ => NewParticipantCall("Ann", "Rowe", "2012-5-3"), "validation.failed"),
                new(
                    "a date with a leading space",
                    _ => NewParticipantCall("Ann", "Rowe", " 2012-05-03"),
                    "validation.failed"),
                new("a date with slashes", _ => NewParticipantCall("Ann", "Rowe", "2012/05/03"), "validation.failed"),
                new("a future date", _ => NewParticipantCall("Ann", "Rowe", "2999-01-01"), "validation.failed"),
                new(
                    "no given name",
                    _ => new() { ["familyName"] = "Rowe", ["dateOfBirth"] = "1987-04-09" },
                    "unexpected"),
                new(
                    "no date of birth",
                    _ => new() { ["givenName"] = "Ann", ["familyName"] = "Rowe" },
                    "unexpected"),
            ],
            OtherOrganisationCode: null,
            Event: AuditEventTypes.Participant.Created),
        new(
            "update_participant",
            async scene =>
            {
                var added = await scene.AddParticipantAsync();
                return new()
                {
                    ["participantId"] = added.Id.ToString(),
                    ["expectedVersion"] = ToolScene.ExpectedVersion(added.ETag),
                    ["givenName"] = $"Eve{Unique()}",
                    ["familyName"] = $"Lowe{Unique()}",
                    ["dateOfBirth"] = "1987-04-09",
                };
            },
            [
                new(
                    "an archived participant with their current version",
                    scene => new()
                    {
                        ["participantId"] = scene.Archived.Id.ToString(),
                        ["expectedVersion"] = ToolScene.ExpectedVersion(scene.Archived.ETag),
                        ["givenName"] = $"Rename{scene.Token}",
                    },
                    "participant.archived"),
                new(
                    "a made-up version and a change",
                    scene => With(RenameOfSceneParticipant(scene), "expectedVersion", "AAAAAAAAAAA="),
                    "precondition.failed"),
                new(
                    "no version and a change",
                    scene => Without("expectedVersion", RenameOfSceneParticipant(scene)),
                    "precondition.required"),
                new(
                    "an empty version and a change",
                    scene => With(RenameOfSceneParticipant(scene), "expectedVersion", ""),
                    "precondition.required"),
                new(
                    "an unknown ID",
                    scene => With(RenameOfSceneParticipant(scene), "participantId", Guid.NewGuid().ToString()),
                    "not_found"),
                new(
                    "not an ID",
                    scene => With(RenameOfSceneParticipant(scene), "participantId", "not-a-guid"),
                    "not_found"),
                new(
                    "a given name of 101 characters",
                    scene => ChangeOfSceneParticipant(scene, "givenName", new string('a', 101)),
                    "validation.failed"),
                new(
                    "a family name with a control character",
                    scene => ChangeOfSceneParticipant(scene, "familyName", $"Rowe\u0007{scene.Token}"),
                    "validation.failed"),
                new(
                    "an empty given name",
                    scene => ChangeOfSceneParticipant(scene, "givenName", ""),
                    "validation.failed"),
                new(
                    "none of the three fields",
                    scene => Without("givenName", ChangeOfSceneParticipant(scene, "givenName", "x")),
                    "validation.failed"),
                new(
                    "a blank date",
                    scene => ChangeOfSceneParticipant(scene, "dateOfBirth", " "),
                    "validation.failed"),
                new(
                    "a date with no padding",
                    scene => ChangeOfSceneParticipant(scene, "dateOfBirth", "2012-5-3"),
                    "validation.failed"),
                new(
                    "a date with a leading space",
                    scene => ChangeOfSceneParticipant(scene, "dateOfBirth", " 2012-05-03"),
                    "validation.failed"),
                new(
                    "a date with slashes",
                    scene => ChangeOfSceneParticipant(scene, "dateOfBirth", "2012/05/03"),
                    "validation.failed"),
                new(
                    "a future date",
                    scene => ChangeOfSceneParticipant(scene, "dateOfBirth", "2999-01-01"),
                    "validation.failed"),
                new(
                    "no ID",
                    scene => Without("participantId", RenameOfSceneParticipant(scene)),
                    "unexpected"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.Participant.Updated),
        new(
            "add_goal",
            async scene =>
            {
                var participant = await scene.AddParticipantAsync();
                return AddGoalCall(participant.Id.ToString(), $"Goal {Unique()}");
            },
            [
                new(
                    "an archived participant",
                    scene => AddGoalCall(scene.ArchivedParticipant.Id.ToString(), "Goal x"),
                    "participant.archived"),
                new("an unknown ID", _ => AddGoalCall(Guid.NewGuid().ToString(), "Goal x"), "not_found"),
                new("not an ID", _ => AddGoalCall("not-a-guid", "Goal x"), "not_found"),
                new(
                    "text of 201 characters",
                    scene => AddGoalCall(scene.Participant.Id.ToString(), new string('a', 201)),
                    "validation.failed"),
                new(
                    "a control character",
                    scene => AddGoalCall(scene.Participant.Id.ToString(), "Goal\u0007x"),
                    "validation.failed"),
                new(
                    "a line break",
                    scene => AddGoalCall(scene.Participant.Id.ToString(), "Goal\nx"),
                    "validation.failed"),
                new("empty text", scene => AddGoalCall(scene.Participant.Id.ToString(), ""), "validation.failed"),
                new(
                    "no text",
                    scene => Without("text", AddGoalCall(scene.Participant.Id.ToString(), "Goal x")),
                    "unexpected"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.Goal.Created),
        new(
            "update_goal",
            async scene =>
            {
                var goal = await FreshGoalAsync(scene, archived: false);
                return GoalCall(goal, $"Goal {Unique()}");
            },
            [
                new(
                    "a goal of an archived participant with its current version",
                    scene => GoalCall(scene.ArchivedGoal, $"Reword {scene.Token}"),
                    "participant.archived"),
                new(
                    "a made-up version and a change",
                    scene => With(RewordOfSceneGoal(scene), "expectedVersion", "AAAAAAAAAAA="),
                    "precondition.failed"),
                new(
                    "no version and a change",
                    scene => Without("expectedVersion", RewordOfSceneGoal(scene)),
                    "precondition.required"),
                new(
                    "an empty version and a change",
                    scene => With(RewordOfSceneGoal(scene), "expectedVersion", ""),
                    "precondition.required"),
                new(
                    "an unknown ID",
                    scene => With(RewordOfSceneGoal(scene), "goalId", Guid.NewGuid().ToString()),
                    "not_found"),
                new(
                    "not an ID",
                    scene => With(RewordOfSceneGoal(scene), "goalId", "not-a-guid"),
                    "not_found"),
                new(
                    "text of 201 characters",
                    scene => With(RewordOfSceneGoal(scene), "text", new string('a', 201)),
                    "validation.failed"),
                new(
                    "a control character",
                    scene => With(RewordOfSceneGoal(scene), "text", "Goal\u0007x"),
                    "validation.failed"),
                new(
                    "a line break",
                    scene => With(RewordOfSceneGoal(scene), "text", "Goal\nx"),
                    "validation.failed"),
                new("empty text", scene => With(RewordOfSceneGoal(scene), "text", ""), "validation.failed"),
                new("no text", scene => Without("text", RewordOfSceneGoal(scene)), "unexpected"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.Goal.Updated),
        new(
            "reorder_goals",
            async scene =>
            {
                var participant = await scene.AddParticipantAsync();
                var goals = await scene.AddGoalsAsync(participant.Id, 2);
                return ReorderCall(participant.Id, goals[1].Id.ToString(), goals[0].Id.ToString());
            },
            [
                new(
                    "an archived participant with their goal",
                    scene => ReorderCall(scene.ArchivedParticipant.Id, scene.ArchivedGoal.Id.ToString()),
                    "participant.archived"),
                new(
                    "an archived participant with a wrong list",
                    scene => ReorderCall(scene.ArchivedParticipant.Id, Guid.NewGuid().ToString()),
                    "participant.archived"),
                new(
                    "an unknown ID",
                    scene => With(ReorderOfSceneGoals(scene), "participantId", Guid.NewGuid().ToString()),
                    "not_found"),
                new(
                    "not an ID",
                    scene => With(ReorderOfSceneGoals(scene), "participantId", "not-a-guid"),
                    "not_found"),
                new(
                    "a goal missing",
                    scene => ReorderCall(scene.Participant.Id, ActiveIdsOf(scene)[..2]),
                    "config.order_mismatch"),
                new(
                    "a goal repeated",
                    scene => ReorderCall(scene.Participant.Id, [.. ActiveIdsOf(scene), ActiveIdsOf(scene)[0]]),
                    "config.order_mismatch"),
                new(
                    "an archived goal",
                    scene => ReorderCall(scene.Participant.Id, [.. ActiveIdsOf(scene), scene.Goals[3].Id.ToString()]),
                    "config.order_mismatch"),
                new(
                    "an ID that is not a GUID",
                    scene => ReorderCall(scene.Participant.Id, [.. ActiveIdsOf(scene)[..2], "not-a-guid"]),
                    "config.order_mismatch"),
                new(
                    "an ID with a control character",
                    scene => ReorderCall(
                        scene.Participant.Id, [.. ActiveIdsOf(scene)[..2], ActiveIdsOf(scene)[2] + "\u0007"]),
                    "config.order_mismatch"),
                new("no goalIds", scene => Without("goalIds", ReorderOfSceneGoals(scene)), "unexpected"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.Goal.Reordered),
        new(
            "archive_goal",
            async scene => GoalCall(await FreshGoalAsync(scene, archived: false)),
            PlaceErrorCases(scene => scene.Goals[0]),
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.Goal.Archived),
        new(
            "restore_goal",
            async scene => GoalCall(await FreshGoalAsync(scene, archived: true)),
            PlaceErrorCases(scene => scene.Goals[3]),
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.Goal.Restored),
        new(
            "list_users",
            scene => Task.FromResult(new JsonObject { ["search"] = scene.Token }),
            [
                new(
                    "a control character",
                    scene => new() { ["search"] = scene.Token + "\u0007" },
                    "validation.failed"),
                new("too long", _ => new() { ["search"] = new string('a', 257) }, "validation.failed"),
            ],
            OtherOrganisationCode: null,
            Event: null),
        new(
            "list_common_items",
            _ => Task.FromResult(new JsonObject { ["includeArchived"] = true }),
            [
                new(
                    "includeArchived of the wrong type",
                    scene => new() { ["includeArchived"] = scene.Token },
                    "unexpected"),
            ],
            OtherOrganisationCode: null,
            Event: null),
        new(
            "get_guide_prompts",
            _ => Task.FromResult(new JsonObject()),
            [],
            OtherOrganisationCode: null,
            Event: null),
        new(
            "set_guide_prompts",
            async scene => PromptsCall($"Prompt {Unique()}\nNext {Unique()}", await scene.GuidePromptsVersionAsync()),
            [
                new(
                    "a made-up version and a change",
                    _ => PromptsCall($"Prompt {Unique()}", "AAAAAAAAAAA="),
                    "precondition.failed"),
                new(
                    "no version and a change",
                    _ => PromptsCall($"Prompt {Unique()}"),
                    "precondition.required"),
                new(
                    "an empty version and a change",
                    _ => PromptsCall($"Prompt {Unique()}", ""),
                    "precondition.required"),
                new("a tab", _ => PromptsCall("Prompt\tx", "AAAAAAAAAAA="), "validation.failed"),
                new("a bidi character", _ => PromptsCall("Prompt" + Bidi + "x", "AAAAAAAAAAA="), "validation.failed"),
                new("1,001 characters", _ => PromptsCall(new string('a', 1001), "AAAAAAAAAAA="), "validation.failed"),
                new("no text", _ => new() { ["expectedVersion"] = "AAAAAAAAAAA=" }, "unexpected"),
            ],
            OtherOrganisationCode: "precondition.failed",
            Event: AuditEventTypes.GuidePrompts.Updated),
        new(
            "add_common_item_group",
            _ => Task.FromResult(new JsonObject { ["name"] = $"Group {Unique()}" }),
            [
                new("an empty name", _ => new() { ["name"] = "" }, "validation.failed"),
                new("201 characters", _ => new() { ["name"] = new string('a', 201) }, "validation.failed"),
                new("a control character", _ => new() { ["name"] = "Group\u0007x" }, "validation.failed"),
                new("a line break", _ => new() { ["name"] = "Group\nx" }, "validation.failed"),
                new("a bidi character", _ => new() { ["name"] = "Group" + Bidi + "x" }, "validation.failed"),
                new("no name", _ => new(), "unexpected"),
            ],
            OtherOrganisationCode: null,
            Event: AuditEventTypes.CommonItemGroup.Created),
        new(
            "rename_common_item_group",
            async scene => GroupCall(await scene.AddGroupAsync(), $"Renamed {Unique()}"),
            [
                .. NameErrorCases(scene => GroupCall(scene.Group, $"Rename {scene.Token}")),
                new(
                    "Every note with a made-up version",
                    scene => GroupCall(scene.EveryNoteGroupId, "AAAAAAAAAAA=", "Renamed"),
                    "config.every_note_fixed"),
                .. GroupErrorCases(scene => GroupCall(scene.Group, $"Rename {scene.Token}")),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItemGroup.Updated),
        new(
            "reorder_common_item_groups",
            async scene =>
            {
                await scene.AddGroupAsync();
                await scene.AddGroupAsync();
                var ids = await scene.ActiveGroupIdsAsync();
                return GroupIdsCall([.. ids[1..].Append(ids[0]).Select(id => id.ToString())]);
            },
            [
                new(
                    "Every note's ID",
                    scene => GroupIdsCall(scene.EveryNoteGroupId.ToString(), scene.Group.Id.ToString()),
                    "config.order_mismatch"),
                new(
                    "an archived group",
                    scene => GroupIdsCall(scene.ArchivedGroup.Id.ToString()),
                    "config.order_mismatch"),
                new(
                    "a repeat",
                    scene => GroupIdsCall(scene.Group.Id.ToString(), scene.Group.Id.ToString()),
                    "config.order_mismatch"),
                new("an empty list", _ => GroupIdsCall(), "config.order_mismatch"),
                new("not an ID", _ => GroupIdsCall("not-a-guid"), "config.order_mismatch"),
                new(
                    "a bidi character in an ID",
                    scene => GroupIdsCall(scene.Group.Id + Bidi),
                    "config.order_mismatch"),
                new("no groupIds", _ => new(), "unexpected"),
            ],
            OtherOrganisationCode: "config.order_mismatch",
            Event: AuditEventTypes.CommonItemGroup.Reordered),
        new(
            "archive_common_item_group",
            async scene => GroupCall(await scene.AddGroupAsync()),
            [
                new(
                    "Every note with a made-up version",
                    scene => GroupCall(scene.EveryNoteGroupId, "AAAAAAAAAAA="),
                    "config.every_note_fixed"),
                .. GroupErrorCases(scene => GroupCall(scene.Group)),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItemGroup.Archived),
        new(
            "restore_common_item_group",
            async scene => GroupCall(await scene.AddGroupAsync(archived: true)),
            [
                new(
                    "Every note with a made-up version",
                    scene => GroupCall(scene.EveryNoteGroupId, "AAAAAAAAAAA="),
                    "config.every_note_fixed"),
                .. GroupErrorCases(scene => GroupCall(scene.ArchivedGroup)),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItemGroup.Restored),
        new(
            "add_common_item",
            async scene => AddItemCall((await scene.AddGroupAsync()).Id, $"Item {Unique()}"),
            [
                new(
                    "an archived group",
                    scene => AddItemCall(scene.ArchivedGroup.Id, "Item x"),
                    "config.group_archived"),
                new("an unknown group", _ => AddItemCall(Guid.NewGuid(), "Item x"), "not_found"),
                new(
                    "not a group ID",
                    _ => With(AddItemCall(Guid.NewGuid(), "Item x"), "groupId", "not-a-guid"),
                    "not_found"),
                new("no group ID", _ => new() { ["text"] = "Item x" }, "unexpected"),
                .. TextErrorCases(scene => AddItemCall(scene.Group.Id, "Item x")),
                new("no text", scene => new() { ["groupId"] = scene.Group.Id.ToString() }, "unexpected"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItem.Created),
        new(
            "update_common_item",
            async scene =>
            {
                var group = await scene.AddGroupAsync();
                var item = await scene.AddItemAsync(group.Id);
                return With(ItemTextCall(item, $"Reworded {Unique()}"), "groupId", group.Id);
            },
            [
                .. TextErrorCases(scene => ItemTextCall(scene.Items[0], "Reword")),
                new("neither text nor groupId", scene => ItemCall(scene.Items[0]), "validation.failed"),
                new(
                    "an item of an archived group with its own groupId",
                    scene => With(
                        ItemTextCall(scene.ArchivedGroupItem, "Reword"), "groupId", scene.ArchivedGroup.Id),
                    "config.group_archived"),
                new(
                    "an item of an archived group with no groupId",
                    scene => ItemTextCall(scene.ArchivedGroupItem, "Reword"),
                    "config.group_archived"),
                new(
                    "a move into an archived group",
                    scene => With(ItemCall(scene.Items[0]), "groupId", scene.ArchivedGroup.Id),
                    "config.group_archived"),
                new(
                    "an unknown groupId",
                    scene => With(ItemCall(scene.Items[0]), "groupId", Guid.NewGuid()),
                    "not_found"),
                new(
                    "a groupId that is not an ID",
                    scene => With(ItemTextCall(scene.Items[0], "Reword"), "groupId", "not-a-guid"),
                    "not_found"),
                .. ItemErrorCases(scene => ItemTextCall(scene.Items[0], "Reword")),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItem.Updated),
        new(
            "archive_common_item",
            async scene => ItemCall(await FreshItemAsync(scene, archived: false)),
            ItemErrorCases(scene => ItemCall(scene.Items[0])),
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItem.Archived),
        new(
            "restore_common_item",
            async scene => ItemCall(await FreshItemAsync(scene, archived: true)),
            ItemErrorCases(scene => ItemCall(scene.Items[2])),
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItem.Restored),
        new(
            "reorder_common_items",
            async scene =>
            {
                var group = await scene.AddGroupAsync();
                await scene.AddItemAsync(group.Id);
                await scene.AddItemAsync(group.Id);
                var ids = await scene.ActiveItemIdsAsync(group.Id);
                return ItemIdsCall(group.Id, [.. ids.AsEnumerable().Reverse().Select(id => id.ToString())]);
            },
            [
                new(
                    "an archived item",
                    scene => ItemIdsCall(scene.Group.Id, [.. scene.Items.Select(item => item.Id.ToString())]),
                    "config.order_mismatch"),
                new(
                    "a missing item",
                    scene => ItemIdsCall(scene.Group.Id, scene.Items[0].Id.ToString()),
                    "config.order_mismatch"),
                new(
                    "a repeat",
                    scene => ItemIdsCall(
                        scene.Group.Id, scene.Items[0].Id.ToString(), scene.Items[0].Id.ToString()),
                    "config.order_mismatch"),
                new(
                    "an item of another group",
                    scene => ItemIdsCall(
                        scene.Group.Id,
                        scene.Items[0].Id.ToString(),
                        scene.Items[1].Id.ToString(),
                        scene.ArchivedGroupItem.Id.ToString()),
                    "config.order_mismatch"),
                new("an empty list", scene => ItemIdsCall(scene.Group.Id), "config.order_mismatch"),
                new("not an ID", scene => ItemIdsCall(scene.Group.Id, "not-a-guid"), "config.order_mismatch"),
                new(
                    "a bidi character in an ID",
                    scene => ItemIdsCall(scene.Group.Id, scene.Items[0].Id + Bidi, scene.Items[1].Id.ToString()),
                    "config.order_mismatch"),
                new(
                    "an unknown group",
                    scene => ItemIdsCall(Guid.NewGuid(), scene.Items[0].Id.ToString()),
                    "not_found"),
                new(
                    "a group that is not an ID",
                    scene => With(ItemIdsCall(scene.Group.Id), "groupId", "not-a-guid"),
                    "not_found"),
                new("no group ID", scene => Without("groupId", ItemIdsCall(scene.Group.Id)), "unexpected"),
                new("no itemIds", scene => Without("itemIds", ItemIdsCall(scene.Group.Id)), "unexpected"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.CommonItem.Reordered),
        new(
            "deactivate_user",
            async scene =>
            {
                var worker = await scene.AddUserAsync(UserRole.Worker);
                return UserCall(
                    worker.Email, $"Asked by phone {Unique()}", confirm: true, version: worker.Version);
            },
            [
                new("an unknown email", _ => UserCall($"nobody.{Unique()}@example.test"), "not_found"),
                new(
                    "a control character in the email",
                    scene => UserCall(scene.InvitedUser.Email + "\u0007"),
                    "validation.failed"),
                new(
                    "a line break in the reason",
                    scene => UserCall(scene.InvitedUser.Email, "Asked\nby phone"),
                    "validation.failed"),
                new(
                    "a reason of 201 characters",
                    scene => UserCall(scene.InvitedUser.Email, new string('a', 201)),
                    "validation.failed"),
                new(
                    "a confirmation with no version",
                    scene => UserCall(scene.InvitedUser.Email, confirm: true),
                    "precondition.required"),
                new(
                    "a confirmation with a made-up version",
                    scene => UserCall(scene.InvitedUser.Email, confirm: true, version: "AAAAAAAAAAA="),
                    "precondition.failed"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.User.Deactivated),
        new(
            "invite_user",
            _ => Task.FromResult(InviteCall(
                $"invitee.{Unique()}@example.test",
                $"Invitee {Unique()}",
                "worker",
                reason: $"Asked by phone {Unique()}",
                confirm: true)),
            [
                new(
                    "the role support",
                    _ => InviteCall($"invitee.{Unique()}@example.test", "Invitee Support", "support"),
                    "role.not_allowed"),
                new(
                    "the role Worker in capitals",
                    _ => InviteCall($"invitee.{Unique()}@example.test", "Invitee Capital", "Worker"),
                    "role.not_allowed"),
                new(
                    "the role admin",
                    _ => InviteCall($"invitee.{Unique()}@example.test", "Invitee Admin", "admin"),
                    "role.not_allowed"),
                new(
                    "an empty role",
                    _ => InviteCall($"invitee.{Unique()}@example.test", "Invitee Empty", ""),
                    "validation.failed"),
                new(
                    "a blank name",
                    _ => InviteCall($"invitee.{Unique()}@example.test", "   ", "worker"),
                    "validation.failed"),
                new(
                    "a name of 101 characters",
                    _ => InviteCall($"invitee.{Unique()}@example.test", new string('a', 101), "worker"),
                    "validation.failed"),
                new(
                    "a control character in the email",
                    _ => InviteCall($"invitee.{Unique()}@example.test\u0007", "Invitee Control", "worker"),
                    "validation.failed"),
                new(
                    "a bidi character in the role",
                    _ => InviteCall($"invitee.{Unique()}@example.test", "Invitee Bidi", "work" + Bidi + "er"),
                    "validation.failed"),
                new(
                    "a line break in the reason",
                    _ => InviteCall(
                        $"invitee.{Unique()}@example.test", "Invitee Break", "worker", reason: "Asked\nby phone"),
                    "validation.failed"),
                new(
                    "an address that Identity refuses",
                    _ => InviteCall(
                        $"zo\u00eb.{Unique()}@example.test", "Invitee Letter", "worker", confirm: true),
                    "validation.failed"),
                new(
                    "an address that has an account",
                    scene => InviteCall(scene.InvitedUser.Email, "Invitee Twice", "worker", confirm: true),
                    "user.email_in_use"),
                new(
                    "no role",
                    _ => Without(
                        "role", InviteCall($"invitee.{Unique()}@example.test", "Invitee No Role", "worker")),
                    "unexpected"),
                new(
                    "no email",
                    _ => Without(
                        "email", InviteCall($"invitee.{Unique()}@example.test", "Invitee No Email", "worker")),
                    "unexpected"),
                new(
                    "no name",
                    _ => Without(
                        "displayName", InviteCall($"invitee.{Unique()}@example.test", "Invitee No Name", "worker")),
                    "unexpected"),
            ],
            OtherOrganisationCode: null,
            Event: AuditEventTypes.User.Invited),
        new(
            "reset_user_sign_in",
            async scene =>
            {
                var worker = await scene.AddUserAsync(UserRole.Worker);
                return UserCall(worker.Email, $"Asked by phone {Unique()}", confirm: true, version: worker.Version);
            },
            [
                new("an unknown email", _ => UserCall($"nobody.{Unique()}@example.test"), "not_found"),
                new(
                    "a control character in the email",
                    scene => UserCall(scene.InvitedUser.Email + "\u0007"),
                    "validation.failed"),
                new(
                    "a line break in the reason",
                    scene => UserCall(scene.InvitedUser.Email, "Asked\nby phone"),
                    "validation.failed"),
                new(
                    "a reason of 201 characters",
                    scene => UserCall(scene.InvitedUser.Email, new string('a', 201)),
                    "validation.failed"),
                new(
                    "a deactivated account, confirmed",
                    scene => UserCall(scene.DeactivatedUser.Email, confirm: true, version: "AAAAAAAAAAA="),
                    "user.not_active"),
                new(
                    "a confirmation with no version",
                    scene => UserCall(scene.InvitedUser.Email, confirm: true),
                    "precondition.required"),
                new(
                    "a confirmation with a made-up version",
                    scene => UserCall(scene.InvitedUser.Email, confirm: true, version: "AAAAAAAAAAA="),
                    "precondition.failed"),
            ],
            OtherOrganisationCode: "not_found",
            Event: AuditEventTypes.User.SetupLinkSent),
    ];

    private static readonly string Bidi = char.ConvertFromUtf32(0x202E);

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static JsonObject UserCall(
        string email, string? reason = null, bool confirm = false, string? version = null)
    {
        var arguments = new JsonObject { ["email"] = email };
        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        if (confirm)
        {
            arguments["confirm"] = true;
        }

        if (version is not null)
        {
            arguments["expectedVersion"] = version;
        }

        return arguments;
    }

    private static JsonObject InviteCall(
        string email, string displayName, string role, string? reason = null, bool confirm = false)
    {
        var arguments = new JsonObject { ["email"] = email, ["displayName"] = displayName, ["role"] = role };
        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        if (confirm)
        {
            arguments["confirm"] = true;
        }

        return arguments;
    }

    private static JsonObject PromptsCall(string text, string? version = null)
    {
        var arguments = new JsonObject { ["text"] = text };
        if (version is not null)
        {
            arguments["expectedVersion"] = version;
        }

        return arguments;
    }

    private static JsonObject GroupCall(SceneGroup group, string? name = null) =>
        GroupCall(group.Id, ToolScene.ExpectedVersion(group.ETag), name);

    // A change to a group that sends the version given, and the name if one is given.
    private static JsonObject GroupCall(Guid groupId, string version, string? name = null)
    {
        var arguments = new JsonObject { ["groupId"] = groupId.ToString(), ["expectedVersion"] = version };
        if (name is not null)
        {
            arguments["name"] = name;
        }

        return arguments;
    }

    private static JsonObject GroupIdsCall(params string[] groupIds) => new()
    {
        ["groupIds"] = new JsonArray([.. groupIds.Select(id => (JsonNode?)JsonValue.Create(id))]),
    };

    // The refusals of a name, which differ in the name sent and not in the group, so the group is the scene's.
    private static IReadOnlyList<ToolErrorCase> NameErrorCases(Func<ToolScene, JsonObject> change) =>
    [
        new("an empty name", scene => With(change(scene), "name", ""), "validation.failed"),
        new("201 characters", scene => With(change(scene), "name", new string('a', 201)), "validation.failed"),
        new("a control character", scene => With(change(scene), "name", "Group\u0007x"), "validation.failed"),
        new("a line break", scene => With(change(scene), "name", "Group\nx"), "validation.failed"),
        new("a bidi character", scene => With(change(scene), "name", "Group" + Bidi + "x"), "validation.failed"),
    ];

    // The refusals of a change to a group that the version and the ID decide, which differ in the scene's group that
    // they change and so, for a rename, in the name that they send.
    private static IReadOnlyList<ToolErrorCase> GroupErrorCases(Func<ToolScene, JsonObject> change) =>
    [
        new(
            "a made-up version and a change",
            scene => With(change(scene), "expectedVersion", "AAAAAAAAAAA="),
            "precondition.failed"),
        new(
            "no version and a change",
            scene => Without("expectedVersion", change(scene)),
            "precondition.required"),
        new(
            "an empty version and a change",
            scene => With(change(scene), "expectedVersion", ""),
            "precondition.required"),
        new("an unknown ID", scene => With(change(scene), "groupId", Guid.NewGuid().ToString()), "not_found"),
        new("not an ID", scene => With(change(scene), "groupId", "not-a-guid"), "not_found"),
        new("no ID", scene => Without("groupId", change(scene)), "unexpected"),
    ];

    private static JsonObject AddItemCall(Guid groupId, string text) => new()
    {
        ["groupId"] = groupId.ToString(),
        ["text"] = text,
    };

    // A change to an item that sends the version it is at.
    private static JsonObject ItemCall(SceneItem item) => new()
    {
        ["itemId"] = item.Id.ToString(),
        ["expectedVersion"] = ToolScene.ExpectedVersion(item.ETag),
    };

    private static JsonObject ItemTextCall(SceneItem item, string text) => With(ItemCall(item), "text", text);

    private static JsonObject ItemIdsCall(Guid groupId, params string[] itemIds) => new()
    {
        ["groupId"] = groupId.ToString(),
        ["itemIds"] = new JsonArray([.. itemIds.Select(id => (JsonNode?)JsonValue.Create(id))]),
    };

    // An item that the call adds, in a group of its own, so that the call changes a row of its own.
    private static async Task<SceneItem> FreshItemAsync(ToolScene scene, bool archived) =>
        await scene.AddItemAsync((await scene.AddGroupAsync()).Id, archived);

    // The refusals of a text, which differ in the text sent and not in the row, so the row is the scene's.
    private static IReadOnlyList<ToolErrorCase> TextErrorCases(Func<ToolScene, JsonObject> change) =>
    [
        new("an empty text", scene => With(change(scene), "text", ""), "validation.failed"),
        new("201 characters", scene => With(change(scene), "text", new string('a', 201)), "validation.failed"),
        new("a control character", scene => With(change(scene), "text", "Item\u0007x"), "validation.failed"),
        new("a line break", scene => With(change(scene), "text", "Item\nx"), "validation.failed"),
        new("a bidi character", scene => With(change(scene), "text", "Item" + Bidi + "x"), "validation.failed"),
    ];

    // The refusals of a change to an item that the version and the ID decide, as GroupErrorCases gives for a group.
    private static IReadOnlyList<ToolErrorCase> ItemErrorCases(Func<ToolScene, JsonObject> change) =>
    [
        new(
            "a made-up version and a change",
            scene => With(change(scene), "expectedVersion", "AAAAAAAAAAA="),
            "precondition.failed"),
        new(
            "no version and a change",
            scene => Without("expectedVersion", change(scene)),
            "precondition.required"),
        new(
            "an empty version and a change",
            scene => With(change(scene), "expectedVersion", ""),
            "precondition.required"),
        new("an unknown ID", scene => With(change(scene), "itemId", Guid.NewGuid()), "not_found"),
        new("not an ID", scene => With(change(scene), "itemId", "not-a-guid"), "not_found"),
        new("no ID", scene => Without("itemId", change(scene)), "unexpected"),
    ];

    private static JsonObject With(JsonObject arguments, string name, Guid value) =>
        With(arguments, name, value.ToString());

    private static JsonObject AddGoalCall(string participantId, string text) => new()
    {
        ["participantId"] = participantId,
        ["text"] = text,
    };

    // A change to a goal that sends the version it is at, and the text if one is given.
    private static JsonObject GoalCall(SceneGoal goal, string? text = null)
    {
        var arguments = new JsonObject
        {
            ["goalId"] = goal.Id.ToString(),
            ["expectedVersion"] = ToolScene.ExpectedVersion(goal.ETag),
        };
        if (text is not null)
        {
            arguments["text"] = text;
        }

        return arguments;
    }

    private static JsonObject ReorderCall(Guid participantId, params string[] goalIds) => new()
    {
        ["participantId"] = participantId.ToString(),
        ["goalIds"] = new JsonArray([.. goalIds.Select(id => (JsonNode?)JsonValue.Create(id))]),
    };

    // A goal of a participant that the call adds, so that the call changes a row of its own.
    private static async Task<SceneGoal> FreshGoalAsync(ToolScene scene, bool archived) =>
        await scene.AddGoalAsync((await scene.AddParticipantAsync()).Id, archived);

    // A reword of the scene's first active goal, with the version it is at, which no case that expects a refusal may
    // let through, as the scene's rows are shared.
    private static JsonObject RewordOfSceneGoal(ToolScene scene) => GoalCall(scene.Goals[0], $"Reword {scene.Token}");

    private static string[] ActiveIdsOf(ToolScene scene) =>
        [.. scene.Goals.Where(goal => !goal.Archived).Select(goal => goal.Id.ToString())];

    // The scene's active goals in their stored order, which is a change of nothing: only a case that is refused for
    // another reason may send it.
    private static JsonObject ReorderOfSceneGoals(ToolScene scene) =>
        ReorderCall(scene.Participant.Id, ActiveIdsOf(scene));

    // The refusals of archive_goal and restore_goal, which differ only in the scene's goal that they change: an active
    // one to archive, an archived one to restore.
    private static IReadOnlyList<ToolErrorCase> PlaceErrorCases(Func<ToolScene, SceneGoal> goalOf)
    {
        JsonObject Change(ToolScene scene) => GoalCall(goalOf(scene));
        return
        [
            new(
                "a goal of an archived participant with its current version",
                scene => GoalCall(scene.ArchivedGoal),
                "participant.archived"),
            new(
                "a made-up version and a change",
                scene => With(Change(scene), "expectedVersion", "AAAAAAAAAAA="),
                "precondition.failed"),
            new(
                "no version and a change",
                scene => Without("expectedVersion", Change(scene)),
                "precondition.required"),
            new(
                "an empty version and a change",
                scene => With(Change(scene), "expectedVersion", ""),
                "precondition.required"),
            new(
                "an unknown ID",
                scene => With(Change(scene), "goalId", Guid.NewGuid().ToString()),
                "not_found"),
            new("not an ID", scene => With(Change(scene), "goalId", "not-a-guid"), "not_found"),
            new("no ID", scene => Without("goalId", Change(scene)), "unexpected"),
        ];
    }

    private static JsonObject NewParticipantCall(string givenName, string familyName, string dateOfBirth) => new()
    {
        ["givenName"] = givenName,
        ["familyName"] = familyName,
        ["dateOfBirth"] = dateOfBirth,
    };

    private static JsonObject RenameOfSceneParticipant(ToolScene scene) =>
        ChangeOfSceneParticipant(scene, "givenName", $"Rename{scene.Token}");

    private static JsonObject With(JsonObject arguments, string name, string value)
    {
        arguments[name] = value;
        return arguments;
    }

    private static JsonObject Without(string name, JsonObject arguments)
    {
        arguments.Remove(name);
        return arguments;
    }

    // A change to the scene's participant that sends the version they are at, and the one field given, which no case
    // that expects a refusal may let through, as the scene's rows are shared.
    private static JsonObject ChangeOfSceneParticipant(ToolScene scene, string field, string value) => new()
    {
        ["participantId"] = scene.Participant.Id.ToString(),
        ["expectedVersion"] = ToolScene.ExpectedVersion(scene.Participant.ETag),
        [field] = value,
    };
}
