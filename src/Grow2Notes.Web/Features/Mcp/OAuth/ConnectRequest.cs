using System.ComponentModel.DataAnnotations;

namespace Grow2Notes.Web.Features.Mcp.OAuth;

/// <summary>
/// The body of <c>POST /api/connect</c> (mcp-server.md §4.4): the handle of the request the person answers, and
/// whether they allow it.
/// </summary>
/// <remarks>
/// Both required, so a body without one gets <c>422 validation.failed</c> naming it. <c>Allow</c> is nullable so that a
/// missing value is told apart from a false one, which would be a Cancel. Public, as the validation source generator
/// describes public types only.
/// </remarks>
public sealed record ConnectRequest([property: Required] string Request, [property: Required] bool? Allow);
