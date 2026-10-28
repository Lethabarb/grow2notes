using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// What the tests of one tool's class share in calling a tool on the app of a <see cref="ToolWorld"/> and reading what
/// it answered.
/// </summary>
internal static class ToolCalls
{
    /// <summary>The structured content of a result.</summary>
    public static JsonElement Content(CallToolResult result) => Assert.NotNull(result.StructuredContent);

    /// <summary>The whole result as the client received it, structured content and text block together.</summary>
    public static string Body(CallToolResult result) =>
        JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions);

    /// <summary>The names of every property of <paramref name="element"/>, at any depth.</summary>
    public static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };

    /// <summary>
    /// Asserts that <paramref name="result"/> is a <c>validation.failed</c> refusal whose one message for
    /// <paramref name="field"/> is <paramref name="message"/>.
    /// </summary>
    public static void AssertValidationFailed(CallToolResult result, string field, string message)
    {
        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("validation.failed", error.GetProperty("code").GetString());
        Assert.Equal("Some arguments are not valid. See fields.", error.GetProperty("message").GetString());
        Assert.Equal(
            [message], error.GetProperty("fields").GetProperty(field).EnumerateArray().Select(m => m.GetString()));
    }

    /// <summary>
    /// Calls <paramref name="tool"/> as <paramref name="caller"/> and gives the result with the one audit row that the
    /// call wrote.
    /// </summary>
    public static async Task<(CallToolResult Result, AuditRow Row)> CallAsync(
        this ToolWorld world, string tool, JsonObject arguments, ToolCaller caller = ToolCaller.Manager)
    {
        var lastEventId = await LastEventIdAsync(world.Flow);
        var result = await ToolScene.CallAsync(world.ClientOf(caller), tool, arguments);
        return (result, Assert.Single(await EventsAfterAsync(world.Flow, lastEventId)));
    }
}
