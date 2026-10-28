using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Web.Platform;
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
    /// The refusal of one argument (<c>validation.failed</c>): <paramref name="fieldMessage"/> says what is wrong with
    /// <paramref name="field"/>, and never echoes its value.
    /// </summary>
    public static ToolError ValidationFailed(string field, string fieldMessage) =>
        new(
            ErrorCode.ValidationFailed.Code,
            "Some arguments are not valid. See fields.",
            new Dictionary<string, string[]> { [field] = [fieldMessage] });

    /// <summary>
    /// A change that sent no <c>expectedVersion</c> (<c>precondition.required</c>), which the look-up of the row gives.
    /// </summary>
    public static ToolError PreconditionRequired { get; } =
        new(ErrorCode.PreconditionRequired.Code, "Send the expectedVersion that the look-up returned.");

    /// <summary>
    /// The refusal of the arguments that a handler refused (<c>validation.failed</c>), each message under its
    /// argument's name: the handler's keys are its members' names, which are camel-cased as the arguments are.
    /// </summary>
    public static ToolError ValidationFailed(IReadOnlyDictionary<string, string[]> errors) =>
        new(
            ErrorCode.ValidationFailed.Code,
            "Some arguments are not valid. See fields.",
            errors.ToDictionary(error => JsonNamingPolicy.CamelCase.ConvertName(error.Key), error => error.Value));

    /// <summary>
    /// The refusal for a row that does not exist in the organisation, whether it exists in another or nowhere
    /// (<c>not_found</c>).
    /// </summary>
    public static ToolError NotFound(string message) => new("not_found", message);

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
