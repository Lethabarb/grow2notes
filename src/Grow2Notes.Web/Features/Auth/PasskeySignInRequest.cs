using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/passkey</c> (design.md §6.2): the credential that the browser's passkey prompt gave,
/// as <c>JSON.stringify</c> writes it, which is what Identity's <c>PasskeySignInAsync</c> reads.
/// </summary>
/// <remarks>
/// Required, so a request without it, or with an empty one, gets <c>422 validation.failed</c> rather than reaching
/// Identity, which throws on either. Public, as the validation source generator describes public types only.
/// </remarks>
public sealed record PasskeySignInRequest([property: Required] string CredentialJson);
