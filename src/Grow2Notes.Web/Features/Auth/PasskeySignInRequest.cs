namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/passkey</c> (design.md §6.2): the credential that the browser's passkey prompt gave,
/// as <c>JSON.stringify</c> writes it, which is what Identity's <c>PasskeySignInAsync</c> reads.
/// </summary>
internal sealed record PasskeySignInRequest(string CredentialJson);
