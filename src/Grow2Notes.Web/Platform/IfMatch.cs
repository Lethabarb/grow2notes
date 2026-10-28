using Microsoft.Extensions.Primitives;

namespace Grow2Notes.Web.Platform;

/// <summary>
/// The <c>If-Match</c> check of design.md §6.6 and §6.7: a change to an existing row must send the ETag of the row as
/// the client loaded it, and is refused when the row has changed since. A row's token is its <c>RowVersion</c> as
/// base64 (<see cref="Configuration.RowVersionToken"/>), or a user's <c>ConcurrencyStamp</c>, and its ETag is that
/// token as an RFC 9110 strong entity tag, in quotes.
/// </summary>
/// <remarks>
/// Only the current ETag itself matches. <c>*</c>, a weak tag (<c>W/"…"</c>) and an unquoted value never do, and nor
/// does a list of tags, even one that holds it: a client that loaded the row has one ETag for it, and anything else
/// shows nothing about which version it saw. The check gives a design.md §6.9 code rather than an HTTP result, so that
/// a handler gives the same code to the app's endpoint, which answers the code's <see cref="ErrorCode.Problem"/>, and
/// to Release 2's tools, which pass their <c>expectedVersion</c> through <see cref="ETag"/> (mcp-server.md §5.1, §5.5).
/// It runs before the change is saved; a save that still throws <c>DbUpdateConcurrencyException</c>, because the row
/// changed after it was loaded, gives <see cref="ErrorCode.PreconditionFailed"/> too.
/// </remarks>
internal static class IfMatch
{
    /// <summary>
    /// Gives <see cref="ErrorCode.PreconditionRequired"/> when the request sent no <c>If-Match</c>,
    /// <see cref="ErrorCode.PreconditionFailed"/> when its <c>If-Match</c> is not the <see cref="ETag"/> of
    /// <paramref name="currentToken"/>, and <see langword="null"/> when it is, so the change goes on.
    /// </summary>
    /// <param name="ifMatch">The request's <c>If-Match</c> values, as <c>Headers.IfMatch</c> gives them.</param>
    /// <param name="currentToken">The token of the row as it is stored now.</param>
    public static ErrorCode? Check(StringValues ifMatch, string currentToken)
    {
        var current = ETag(currentToken);

        // An empty If-Match names no version, like a missing one: the client's fault, not a row that changed.
        if (StringValues.IsNullOrEmpty(ifMatch))
        {
            return ErrorCode.PreconditionRequired;
        }

        return ifMatch.Count == 1 && ifMatch[0] == current ? null : ErrorCode.PreconditionFailed;
    }

    /// <summary>
    /// <see cref="Check"/>, with the refusal given when the channel gives it: at once for <see cref="Channel.App"/>,
    /// and held back for <see cref="Channel.Mcp"/>, whose call that changes nothing is not refused (mcp-server.md
    /// §5.1). The one place the channel decides this, for every handler that checks a version.
    /// </summary>
    /// <param name="actor">Who changes the row, and by which channel.</param>
    /// <param name="ifMatch">
    /// The <c>If-Match</c> values, or the tool's <c>expectedVersion</c> through <see cref="ETag"/>.
    /// </param>
    /// <param name="currentToken">The token of the row as it is stored now.</param>
    public static VersionCheck CheckFor(Actor actor, StringValues ifMatch, string currentToken)
    {
        var refusal = Check(ifMatch, currentToken);
        return actor.Channel == Channel.Mcp ? new(null, refusal) : new(refusal, null);
    }

    /// <summary>
    /// The <c>If-Match</c> values for a tool's <c>expectedVersion</c>: none when it is missing or empty, so that
    /// <see cref="Check"/> gives <see cref="ErrorCode.PreconditionRequired"/>, and otherwise its <see cref="ETag"/>.
    /// </summary>
    public static StringValues FromExpectedVersion(string? expectedVersion) =>
        string.IsNullOrEmpty(expectedVersion) ? StringValues.Empty : ETag(expectedVersion);

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
