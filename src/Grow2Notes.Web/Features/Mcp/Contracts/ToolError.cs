using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Grow2Notes.Web.Features.Mcp.Contracts;

/// <summary>
/// A tool's refusal (mcp-server.md §5.5): a normal result with <c>isError: true</c>, so that the model can correct
/// itself, holding <c>{ "error": { "code", "message", "fields" } }</c>. The same JSON is the result's text, for a client
/// that reads only content.
/// </summary>
/// <param name="Code">The code that §5.5 lists, which the audit event <c>mcp.refused</c> records.</param>
/// <param name="Message">What the model reads: it never echoes a value the model sent.</param>
/// <param name="Fields">The messages of each field that failed validation, or null.</param>
internal sealed record ToolError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Fields = null)
{
    /// <summary>Anything that no tool refused for: the transaction is rolled back and nothing was changed.</summary>
    public static ToolError Unexpected { get; } = new("unexpected", "Something went wrong. Nothing was changed.");

    /// <summary>
    /// The error code of <paramref name="result"/>, or null when it is not an error. An error that names no code is
    /// <see cref="Unexpected"/>.
    /// </summary>
    public static string? CodeOf(CallToolResult result)
    {
        if (result.IsError != true)
        {
            return null;
        }

        return result.StructuredContent is { ValueKind: JsonValueKind.Object } content
            && content.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("code", out var code)
            && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : Unexpected.Code;
    }

    public CallToolResult ToResult()
    {
        var body = new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["code"] = Code,
                ["message"] = Message,
                ["fields"] = JsonSerializer.SerializeToNode(Fields),
            },
        };

        return new()
        {
            IsError = true,
            StructuredContent = JsonSerializer.SerializeToElement(body),
            Content = [new TextContentBlock { Text = body.ToJsonString() }],
        };
    }
}
