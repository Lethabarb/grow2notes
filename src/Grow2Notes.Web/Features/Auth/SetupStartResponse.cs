namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The answer of <c>POST /api/auth/setup/start</c> (design.md §6.2): who the setup link is for, as the setup screen
/// shows them.
/// </summary>
internal sealed record SetupStartResponse(string Email, string DisplayName);
