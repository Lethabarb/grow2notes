using System.Text.Json.Nodes;

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
/// <param name="ValidArguments">Arguments that succeed in the scene's first organisation, and hold something.</param>
/// <param name="ErrorCases">The calls it refuses, with the code of each.</param>
/// <param name="OtherOrganisationCode">
/// The code the tool refuses the valid arguments with when a manager of another organisation sends them, or null when
/// the tool succeeds and returns nothing of the first organisation.
/// </param>
internal sealed record ToolRow(
    string Name,
    Func<ToolScene, JsonObject> ValidArguments,
    IReadOnlyList<ToolErrorCase> ErrorCases,
    string? OtherOrganisationCode);

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
            scene => new() { ["search"] = scene.FamilyName },
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
            OtherOrganisationCode: null),
        new(
            "get_participant",
            scene => new() { ["participantId"] = scene.Participant.Id.ToString() },
            [
                new("an unknown ID", _ => new() { ["participantId"] = Guid.NewGuid().ToString() }, "not_found"),
                new("not an ID", _ => new() { ["participantId"] = "not-a-guid" }, "not_found"),
                new("no ID", _ => new(), "unexpected"),
            ],
            OtherOrganisationCode: "not_found"),
    ];
}
