using System.Text.RegularExpressions;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Finds what design.md §9.7's strict Content Security Policy would block in the SPA's build, for
/// <c>SpaBuildTests</c>: inline script, inline style and <c>data:</c> URIs (design.md §7.2). Each finder names each
/// thing it finds, so a failing test shows what broke the policy.
/// </summary>
/// <remarks>
/// The page is read with regular expressions, as the test project has no HTML parser and the page is Vite's short,
/// well-formed <c>index.html</c>. They read a start tag's attributes as a browser's tokenizer does, quoted values that
/// hold a <c>&gt;</c> and attributes after a <c>/</c> or straight after a quoted value included. But they read text
/// that a browser does not take for a tag, such as text in a comment, a script or a <c>&lt;title&gt;</c>, as if it
/// were one, so such text can hide a real tag or show a tag that is not there. Vite's page has no such text.
/// </remarks>
internal static partial class CspBlockedContent
{
    /// <summary>
    /// Each inline script (a <c>&lt;script&gt;</c> with no <c>src</c>, of any type), <c>&lt;style&gt;</c> element,
    /// <c>style</c> attribute, inline event handler (an attribute whose name starts with <c>on</c>, on any element)
    /// and <c>data:</c> URI in <paramref name="page"/>.
    /// </summary>
    public static List<string> InPage(string page)
    {
        List<string> found = [];
        foreach (Match tag in StartTag().Matches(page))
        {
            var element = tag.Groups["element"].Value.ToLowerInvariant();
            var attributes = tag.Groups["attribute"].Captures.Select(name => name.Value.ToLowerInvariant()).ToList();
            if (element == "script" && !attributes.Contains("src"))
            {
                found.Add("<script> with no src");
            }

            if (element == "style")
            {
                found.Add("<style> element");
            }

            found.AddRange(
                from attribute in attributes
                where attribute == "style" || attribute.StartsWith("on", StringComparison.Ordinal)
                select $"{attribute} attribute on <{element}>");
        }

        found.AddRange(DataUris(page));
        return found;
    }

    /// <summary>
    /// The start of each <c>data:</c> URI in <paramref name="text"/>, such as a built script or stylesheet: as far as
    /// its media type, as in <c>data:image/png</c>, or its <c>,</c> or <c>;</c>.
    /// </summary>
    public static List<string> DataUris(string text) => [.. DataUri().Matches(text).Select(uri => uri.Value)];

    // The element's name, then each attribute's name with its value, if any, in double quotes, single quotes or none,
    // the attributes separated by white space or /. Each attribute is an atomic group, so a tag with no closing >
    // fails at once rather than backtracking through every way of splitting its attributes.
    [GeneratedRegex(
        """
        <(?<element>[a-z][^\s/>]*)
        (?>[\s/]*(?<attribute>[^\s/>][^\s/>=]*)(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]*))?)*
        [\s/]*>
        """,
        RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex StartTag();

    // data: followed by a media type, such as data:image/svg+xml, or directly by , or ;, as in data:, and data:;base64,
    // (S06.01.04's Notes). A bare data: is not matched, because the React bundle holds object keys such as data:e.
    [GeneratedRegex(@"data:(?:[a-z0-9!#$&^_.+-]+/[a-z0-9!#$&^_.+-]+|[,;])", RegexOptions.IgnoreCase)]
    private static partial Regex DataUri();
}
