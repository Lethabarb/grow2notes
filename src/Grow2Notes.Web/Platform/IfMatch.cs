using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The <c>If-Match</c> check of design.md §6.6 and §6.7: a change to an existing row must send the ETag of the row as
/// the client loaded it, and is refused when the row has changed since. A row's token is its <c>RowVersion</c> as
/// base64, or a user's <c>ConcurrencyStamp</c>, and its ETag is that token as an RFC 9110 strong entity tag, in quotes.
/// </summary>
/// <remarks>
/// Only the current ETag itself matches. <c>*</c>, a weak tag (<c>W/"…"</c>) and an unquoted value never do, and nor
/// does a list of tags, even one that holds it: a client that loaded the row has one ETag for it, and anything else
/// shows nothing about which version it saw. This check runs before the change is saved; a save that still throws
/// <c>DbUpdateConcurrencyException</c>, because the row changed after it was loaded, is answered <c>412</c> by its
/// endpoint.
/// </remarks>
internal static class IfMatch
{
    /// <summary>
    /// Answers <c>428 precondition.required</c> when the request has no <c>If-Match</c>, <c>412 precondition.failed</c>
    /// when its <c>If-Match</c> is not the <see cref="ETag"/> of <paramref name="currentToken"/>, and
    /// <see langword="null"/> when it is, so the endpoint goes on.
    /// </summary>
    public static ProblemHttpResult? Check(HttpRequest request, string currentToken)
    {
        var current = ETag(currentToken);
        var ifMatch = request.Headers.IfMatch;

        // An empty If-Match names no version, like a missing one: the client's fault, not a row that changed.
        if (StringValues.IsNullOrEmpty(ifMatch))
        {
            return ErrorCode.PreconditionRequired.Problem();
        }

        return ifMatch.Count == 1 && ifMatch[0] == current ? null : ErrorCode.PreconditionFailed.Problem();
    }

    /// <summary>
    /// The ETag of a row whose token is <paramref name="token"/>: the token in quotes, the one <c>If-Match</c> value
    /// that <see cref="Check"/> accepts for it.
    /// </summary>
    public static string ETag(string token)
    {
        // An empty token would give the empty tag "", which a client could send without having loaded the row.
        ArgumentException.ThrowIfNullOrEmpty(token);
        return $"\"{token}\"";
    }
}
