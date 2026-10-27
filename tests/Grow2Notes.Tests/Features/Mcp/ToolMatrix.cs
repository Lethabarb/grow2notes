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
    ];

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

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
