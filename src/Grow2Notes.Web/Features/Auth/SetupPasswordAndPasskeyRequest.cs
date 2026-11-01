using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/setup/password-and-passkey</c> (design.md §6.2): the password the person chose on
/// view F, as typed, never trimmed, and the new passkey's credential, as the browser's
/// <c>navigator.credentials.create()</c> gave it, written as JSON.
/// </summary>
/// <remarks>
/// Both required, so a body without one, or with one that is empty or only whitespace, gets
/// <c>422 validation.failed</c> naming it before the endpoint runs. There is no name: every passkey is stored under
/// "Grow2Notes" (D70). Public, as the validation source generator describes public types only.
/// </remarks>
public sealed record SetupPasswordAndPasskeyRequest(
    [property: Required] string Password, [property: Required] string CredentialJson);
