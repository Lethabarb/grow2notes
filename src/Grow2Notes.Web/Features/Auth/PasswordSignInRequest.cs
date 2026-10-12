using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/login</c> (design.md §6.2): the email address, as the SPA sends it, trimmed, and the
/// password, as typed, never trimmed.
/// </summary>
/// <remarks>
/// Both required, so a body without one, or with one that is empty or only whitespace, gets
/// <c>422 validation.failed</c> naming it before the endpoint runs. Public, as the validation source generator
/// describes public types only.
/// </remarks>
public sealed record PasswordSignInRequest([property: Required] string Email, [property: Required] string Password);
