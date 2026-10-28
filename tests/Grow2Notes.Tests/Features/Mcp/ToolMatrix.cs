using System.Text.Json.Nodes;
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
    ];

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

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
