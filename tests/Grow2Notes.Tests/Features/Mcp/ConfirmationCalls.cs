using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// What the tests of the tools that ask the person to confirm (mcp-server.md §5.3, A136) share in calling them as a
/// client with forms or without and in reading what they answered: the capabilities of each kind of client, the answers
/// that are not a yes, and the asserts of a result. The mechanism itself is proved on its stand-in in
/// <see cref="ToolConfirmationTests"/>. Public so that the theories can name its data; the helpers that take the
/// internal test types are internal.
/// </summary>
public static class ConfirmationCalls
{
    /// <summary>The capabilities of a client that shows forms.</summary>
    internal const string FormCapabilities = """{"elicitation":{"form":{}}}""";

    /// <summary>A version that no row has.</summary>
    internal const string StaleVersion = "AAAAAAAAAAA=";

    /// <summary>The answer of a form that was accepted with its box ticked.</summary>
    internal const string AcceptedYes = """{"confirm":{"action":"accept","content":{"confirm":true}}}""";

    /// <summary>What a state that is not for the call, or has lapsed, is refused with.</summary>
    internal const string StateRefused =
        "This confirmation has expired or does not match this call. Nothing was changed. " +
        "Call the tool again to ask the person afresh.";

    public static TheoryData<string> CapabilitiesThatGetThePreview { get; } = ["{}", """{"elicitation":{"url":{}}}"""];

    // A blank elicitation counts as the form: the SDK reads it so.
    public static TheoryData<string> CapabilitiesThatGetTheForm { get; } =
        [FormCapabilities, """{"elicitation":{}}"""];

    // The last sends no answer at all.
    public static TheoryData<string> AnswersThatAreNotYes { get; } =
    [
        """{"confirm":{"action":"decline"}}""",
        """{"confirm":{"action":"cancel"}}""",
        """{"confirm":{"action":"accept","content":{"confirm":false}}}""",
        """{"confirm":{"action":"accept","content":{"confirm":"true"}}}""",
        """{"confirm":{"action":"accept"}}""",
        "",
    ];

    internal static void AssertJson(JsonObject expected, JsonElement actual) =>
        AssertJson(expected, JsonNode.Parse(actual.GetRawText()));

    internal static void AssertJson(JsonObject expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Expected {expected}, got {actual}.");

    internal static void AssertError(CallToolResult result, string code, string message)
    {
        Assert.True(result.IsError);
        Assert.Equal(code, ToolError.CodeOf(result));
        Assert.Equal(message, Content(result).GetProperty("error").GetProperty("message").GetString());
    }

    internal static void AssertRawError(JsonObject result, string code, string message)
    {
        Assert.True(result["isError"]!.GetValue<bool>());
        var error = result["structuredContent"]!["error"]!;
        Assert.Equal(code, error["code"]!.GetValue<string>());
        Assert.Equal(message, error["message"]!.GetValue<string>());
    }

    internal static string Flip(string state)
    {
        var at = state.Length / 2;
        return state[..at] + (state[at] == 'A' ? 'B' : 'A') + state[(at + 1)..];
    }

    // A raw 2026-07-28 call with the capabilities given, and the one audit row it wrote.
    internal static async Task<(JsonObject Result, AuditRow Row)> RawAsync(
        AssistantFlow flow,
        string tool,
        string token,
        JsonObject arguments,
        string capabilities,
        string? state = null,
        string? answer = null)
    {
        var lastEventId = await LastEventIdAsync(flow);
        var result = await flow.PostToolCallAsync(
            token,
            tool,
            arguments,
            JsonNode.Parse(capabilities)!.AsObject(),
            state,
            answer is null ? null : JsonNode.Parse(answer)!.AsObject());
        return (result, Assert.Single(await EventsAfterAsync(flow, lastEventId)));
    }

    // The first call of a form: the state that comes back with it.
    internal static async Task<string> AskAsync(
        AssistantFlow flow, string tool, string token, JsonObject arguments, string capabilities)
    {
        var (asked, _) = await RawAsync(flow, tool, token, arguments, capabilities);
        Assert.Equal("input_required", asked["resultType"]!.GetValue<string>());
        return asked["requestState"]!.GetValue<string>();
    }
}
