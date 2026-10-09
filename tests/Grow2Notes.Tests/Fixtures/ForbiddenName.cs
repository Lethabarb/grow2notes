using System.Text.RegularExpressions;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Finds the parent company's name, which appears nowhere in the app or the repository (D42), in a file's path and
/// text. The match ignores case and takes the name's words in order, with any run of characters other than letters and
/// digits, or none, between them, so it finds the name wrapped over two lines of Markdown, hyphenated in a file name
/// or run together in a domain. A match on an innocent word is reported too, to be looked at rather than passed. What
/// it reports never holds the name, as a failing test prints it in CI's public log.
/// </summary>
internal sealed partial class ForbiddenName
{
    /// <summary>The environment variable that holds the name, filled in CI from the Actions secret.</summary>
    public const string Variable = "FORBIDDEN_NAME";

    private readonly Regex pattern;

    public ForbiddenName(string name)
    {
        var words = Word().Matches(name).Select(word => word.Value).ToList();
        if (words.Count == 0)
        {
            throw new ArgumentException($"{Variable} holds no letter or digit.", nameof(name));
        }

        // Each word is letters and digits only, so none needs escaping.
        pattern = new Regex(
            string.Join(@"[^\p{L}\p{N}]*", words), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Each place the name appears in the file at <paramref name="path"/>: the path itself, then each line of
    /// <paramref name="text"/> that a match starts on, counted from 1. Each names the file by its path with the name
    /// replaced by <c>&lt;FORBIDDEN_NAME&gt;</c>.
    /// </summary>
    public IEnumerable<string> FindIn(string path, string text)
    {
        var shown = pattern.Replace(path, $"<{Variable}>");
        if (pattern.IsMatch(path))
        {
            yield return $"{shown}: its path";
        }

        foreach (var line in pattern.Matches(text).Select(match => LineAt(text, match.Index)).Distinct())
        {
            yield return $"{shown}: line {line}";
        }
    }

    private static int LineAt(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}
