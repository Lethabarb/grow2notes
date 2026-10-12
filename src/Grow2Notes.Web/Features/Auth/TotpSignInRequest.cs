using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/login/totp</c> (design.md §6.2): the code from the person's authenticator app, as
/// typed, which the endpoint reads with the code check (<see cref="OneTimeCode"/>).
/// </summary>
/// <remarks>
/// Required, so a body without it, or with one that is empty or only whitespace, gets <c>422 validation.failed</c>
/// naming it before the endpoint runs. Public, as the validation source generator describes public types only.
/// </remarks>
public sealed record TotpSignInRequest([property: Required] string Code);
