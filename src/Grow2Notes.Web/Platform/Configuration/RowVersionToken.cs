namespace Grow2Notes.Web.Platform.Configuration;

/// <summary>
/// The version of a configuration row as the API gives it (design.md §6.6, §6.7, D78): its token, which each list
/// gives the row as <c>rowVersion</c> and <see cref="IfMatch.Check"/> takes as the row's current token, and its
/// <c>ETag</c> header, which the response for a single row carries and the client sends back as <c>If-Match</c>.
/// </summary>
internal static class RowVersionToken
{
    /// <summary>The row's <c>RowVersion</c> as base64: the token, and the row's <c>rowVersion</c> in a list.</summary>
    public static string Of(IVersionedRow row) => Convert.ToBase64String(row.RowVersion);

    /// <summary>
    /// The row's <c>ETag</c> header, its token in quotes (<see cref="IfMatch.ETag"/>): the one <c>If-Match</c> value
    /// that <see cref="IfMatch.Check"/> passes for it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The row has not been saved yet, so SQL Server has given it no version, and its token is empty.
    /// </exception>
    public static string ETag(IVersionedRow row) => IfMatch.ETag(Of(row));
}
