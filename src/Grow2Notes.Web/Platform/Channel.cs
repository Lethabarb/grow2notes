namespace Grow2Notes.Web.Platform;

/// <summary>
/// Where an admin handler's call comes from (mcp-server.md §3.5, convention 1): the app's own endpoints, or, in
/// Release 2, a tool of the MCP server, whose events say <c>"via": "mcp"</c> (mcp-server.md §9). Its values are
/// explicit, as the Definition of Done asks of the admin stories' enums, and none is 0, so a channel left unset is no
/// channel.
/// </summary>
internal enum Channel
{
    App = 1,
    Mcp = 2,
}
