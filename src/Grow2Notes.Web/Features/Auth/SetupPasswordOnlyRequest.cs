using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/setup/password-only</c> (design.md §6.2): the password the person chose on view F, as
/// typed, never trimmed.
/// </summary>
/// <remarks>
/// Required, so a body without it, or with one that is empty or only whitespace, gets <c>422 validation.failed</c>
/// naming it before the endpoint runs. Public, as the validation source generator describes public types only.
/// </remarks>
public sealed record SetupPasswordOnlyRequest([property: Required] string Password);
