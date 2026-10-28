using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>The success result of a tool (mcp-server.md §5.5), built the same way for every tool.</summary>
internal static class ToolResults
{
    /// <summary>What a change to a goal does to the notes (design.md §4.8), which every goal result carries.</summary>
    public const string HelpText =
        "Changes apply to notes started from now on. Notes already started or submitted keep the wording they were " +
        "written with.";

    /// <summary>
    /// <paramref name="result"/> as the result's structured content, with the identical JSON as its one text block for
    /// a client that reads only content.
    /// </summary>
    public static CallToolResult Of<T>(T result)
    {
        var json = JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
        return new()
        {
            StructuredContent = json,
            Content = [new TextContentBlock { Text = json.GetRawText() }],
        };
    }
}
