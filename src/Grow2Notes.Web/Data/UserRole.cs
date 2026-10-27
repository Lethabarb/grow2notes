namespace Grow2Notes.Web.Data;

/// <summary>
/// What a user may do (D24). Stored as <c>tinyint</c> with fixed values, so a stored role never changes meaning;
/// <c>3 = Support</c> is Release 2's IT operator, who only connects an AI assistant (mcp-server.md §2.2, §3.5).
/// </summary>
internal enum UserRole : byte
{
    Worker = 1,
    Manager = 2,
    Support = 3,
}
