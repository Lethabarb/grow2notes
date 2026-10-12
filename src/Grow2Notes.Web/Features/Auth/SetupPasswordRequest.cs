using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/setup/password</c> (design.md §6.2): the password the person chose on view F, as
/// typed, never trimmed, and the code from their authenticator app, as typed on view G.
/// </summary>
/// <remarks>
/// Both required, so a body without one, or with one that is empty or only whitespace, gets
/// <c>422 validation.failed</c> naming it before the endpoint runs. Public, as the validation source generator
/// describes public types only.
/// </remarks>
public sealed record SetupPasswordRequest([property: Required] string Password, [property: Required] string TotpCode);
