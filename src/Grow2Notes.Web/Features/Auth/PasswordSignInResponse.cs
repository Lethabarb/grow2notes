namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The answer of <c>POST /api/auth/login</c> to a password that Identity accepts (design.md §6.2): the step that comes
/// next, which is always the code step, <c>totp</c>, as every password user has an authenticator app.
/// </summary>
internal sealed record PasswordSignInResponse(string Next);
