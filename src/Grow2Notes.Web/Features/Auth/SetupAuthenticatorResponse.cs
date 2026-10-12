namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The answer of <c>GET /api/auth/setup/authenticator</c> (design.md §6.2, §8.1 step 4): the authenticator key that
/// the invite stored, which view G shows as the setup key, and the <c>otpauth://</c> URI that adds it to an
/// authenticator app, which <c>GET /api/auth/setup/authenticator/qr.png</c> draws as a QR code.
/// </summary>
internal sealed record SetupAuthenticatorResponse(string SharedKey, string AuthenticatorUri);
