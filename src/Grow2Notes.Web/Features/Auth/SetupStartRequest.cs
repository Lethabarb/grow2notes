using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// The body of <c>POST /api/auth/setup/start</c> (design.md §6.2): the <c>u</c> and <c>t</c> of the setup link's
/// fragment, the user's ID and the setup token, as the SPA reads them.
/// </summary>
/// <remarks>
/// Both are strings, so a garbled link reaches the endpoint and gets its <c>410</c>, not a binding <c>400</c>, which
/// the SPA's <c>api()</c> wrapper would take for a stale antiforgery token and send again. Public, as the validation
/// source generator describes public types only.
/// </remarks>
public sealed record SetupStartRequest([property: Required] string UserId, [property: Required] string Token);
