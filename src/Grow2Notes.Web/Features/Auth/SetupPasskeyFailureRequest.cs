using System.ComponentModel.DataAnnotations;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/setup/passkey/failure</c> (design.md §6.2, D103): why the browser made no passkey,
/// as the error that its passkey prompt threw gives it: its <c>name</c> (such as <c>NotAllowedError</c>),
/// SimpleWebAuthn's <c>code</c> where it has one, and its words.
/// </summary>
/// <remarks>
/// The SPA cuts each to its <see cref="Limits"/> length before it sends it, so a longer value, or no name, gets
/// <c>422 validation.failed</c>. Public, as the validation source generator describes public types only.
/// </remarks>
public sealed record SetupPasskeyFailureRequest(
    [property: Required, MaxLength(Limits.PasskeyFailureName)] string Name,
    [property: MaxLength(Limits.PasskeyFailureCode)] string? Code,
    [property: MaxLength(Limits.PasskeyFailureMessage)] string? Message);
