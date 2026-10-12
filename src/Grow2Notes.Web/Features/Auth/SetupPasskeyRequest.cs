using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/setup/passkey</c> (design.md §6.2): the new passkey's credential, as the browser's
/// <c>navigator.credentials.create()</c> gave it, written as JSON.
/// </summary>
/// <remarks>
/// The SPA also sends <c>name</c>, always "Grow2Notes" (D70), which is not bound: the app's JSON skips a member that
/// the type does not have, and every passkey is stored under that name whatever is sent. Public, as the validation
/// source generator describes public types only.
/// </remarks>
public sealed record SetupPasskeyRequest([property: Required] string CredentialJson);
