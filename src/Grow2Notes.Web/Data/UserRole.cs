namespace Grow2Notes.Web.Data;

/// <summary>
/// What a user may do (D24). Stored as <c>tinyint</c> with fixed values, so a stored role never changes meaning;
/// Release 2 adds <c>3 = Support</c> (mcp-server.md §3.5, convention 2).
/// </summary>
internal enum UserRole : byte
{
    Worker = 1,
    Manager = 2,
}
