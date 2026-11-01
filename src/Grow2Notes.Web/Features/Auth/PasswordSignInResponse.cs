namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The answer of <c>POST /api/auth/login</c> to a password that Identity accepts from a person who chose a second step
/// (design.md §6.2, D97): the step that comes next, <c>passkey</c> for one who holds a passkey and <c>totp</c>, the
/// code from their authenticator app, otherwise (<see cref="SecondStep"/>). A person who chose a password only gets a
/// <see cref="Me"/> instead.
/// </summary>
internal sealed record PasswordSignInResponse(string Next);
