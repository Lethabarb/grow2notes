using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Grow2Notes.Web.Features.Mcp.Tools;

/// <summary>The success result of a tool (mcp-server.md §5.5), built the same way for every tool.</summary>
internal static class ToolResults
{
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
