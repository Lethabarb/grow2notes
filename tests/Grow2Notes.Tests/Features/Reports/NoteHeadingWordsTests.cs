using System.Reflection;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Features.Reports;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// The words that a note shares with its report files (D104, A159): the report's headings and the note form's and read
/// view's headings are the same words, because <see cref="NoteWords"/> and the SPA's <c>noteWords</c> in
/// <c>src/copy/strings.ts</c> hold the same ones (T40). Each constant's name is its key, with a lower-case first
/// letter. The test reads <c>strings.ts</c> as text, as <c>SpaLimitsTests</c> reads <c>limits.ts</c>, so it needs no
/// build of the SPA. Its reader reports what it cannot read rather than pass over it, so a new way of writing the
/// object stops the test instead of leaving a word unchecked.
/// </summary>
public sealed partial class NoteHeadingWordsTests
{
    private const string NoObject =
        "Read no noteWords from strings.ts, which holds them as noteWords: { key: 'words', … }, two spaces in.";

    [Fact]
    public void The_noteWords_of_strings_ts_are_the_words_of_NoteWords()
    {
        var stringsTs = File.ReadAllText(
            Path.Combine(Repository.Root(), "src", "grow2notes-spa", "src", "copy", "strings.ts"));

        Assert.Empty(Problems(stringsTs));
    }

    [Fact]
    public void A_sample_with_every_key_and_word_passes()
    {
        Assert.Empty(Problems(Sample(Entries())));
    }

    [Fact]
    public void A_word_that_differs_fails()
    {
        var entries = Entries().Select(entry => IsSick(entry) ? "sick: 'Unwell'," : entry);

        Assert.Equal(["noteWords.sick is 'Unwell', where NoteWords.Sick is 'Sick'."], Problems(Sample(entries)));
    }

    [Fact]
    public void A_missing_key_and_an_extra_key_fail()
    {
        var entries = Entries().Where(entry => !IsSick(entry)).Append("absent: 'Absent',");

        Assert.Equal(
            ["noteWords.absent: NoteWords has no constant Absent.", "noteWords has no key sick for NoteWords.Sick."],
            Problems(Sample(entries)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("// No words yet.\n")]
    [InlineData("export const strings = {\n  noteWords: {},\n};\n")]
    [InlineData("export const strings = {\n  // noteWords: { sick: 'Sick' },\n};\n")]
    public void A_file_with_no_noteWords_fails(string sample)
    {
        Assert.Equal([NoObject], Problems(sample));
    }

    [Theory]
    [InlineData("Sick: 'Sick'")]
    [InlineData("sick: 4 + 1")]
    [InlineData("sick")]
    [InlineData("...otherWords")]
    public void A_word_written_another_way_fails(string entry)
    {
        var entries = Entries().Where(each => !IsSick(each)).Append($"{entry},");

        Assert.Contains(
            $"Cannot read \"{entry}\" in noteWords, where each word is key: 'words'.", Problems(Sample(entries)));
    }

    private static bool IsSick(string entry) => entry.StartsWith("sick:", StringComparison.Ordinal);

    /// <summary>The constants of <see cref="NoteWords"/> as the SPA's object holds them, one entry each.</summary>
    private static IEnumerable<string> Entries() =>
        Constants().Select(constant => $"{CamelCase(constant.Name)}: '{constant.Words}',");

    /// <summary>
    /// What is wrong with <paramref name="stringsTs"/>: a key with no constant, a word that differs from its
    /// constant's, an entry it cannot read, a constant with no key of its name, or that it holds no <c>noteWords</c> at
    /// all.
    /// </summary>
    private static List<string> Problems(string stringsTs)
    {
        if (NoteWordsObject().Match(stringsTs) is not { Success: true } found)
        {
            return [NoObject];
        }

        var body = Comment().Replace(found.Groups["body"].Value, "");
        var entries = Entry().Matches(body);
        var unread = Entry()
            .Replace(body, "")
            .Split(',')
            .Select(rest => rest.Trim())
            .Where(rest => rest.Length > 0)
            .ToList();
        if (entries.Count == 0 && unread.Count == 0)
        {
            return [NoObject];
        }

        var constants = Constants().ToDictionary(constant => CamelCase(constant.Name));
        var written = new HashSet<string>(StringComparer.Ordinal);
        List<string> problems =
        [
            .. unread.Select(rest => $"Cannot read \"{rest}\" in noteWords, where each word is key: 'words'."),
        ];
        foreach (Match entry in entries)
        {
            var key = entry.Groups["key"].Value;
            var words = entry.Groups["words"].Value;
            written.Add(key);
            if (!constants.TryGetValue(key, out var constant))
            {
                problems.Add($"noteWords.{key}: NoteWords has no constant {char.ToUpperInvariant(key[0])}{key[1..]}.");
            }
            else if (words != constant.Words)
            {
                problems.Add($"noteWords.{key} is '{words}', where NoteWords.{constant.Name} is '{constant.Words}'.");
            }
        }

        problems.AddRange(
            constants
                .Where(constant => !written.Contains(constant.Key))
                .Select(constant => $"noteWords has no key {constant.Key} for NoteWords.{constant.Value.Name}."));
        return problems;
    }

    // Each constant of NoteWords, internal as they are: its name and its words.
    private static IEnumerable<(string Name, string Words)> Constants() =>
        typeof(NoteWords)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (field.Name, (string)field.GetRawConstantValue()!));

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    // A strings.ts in the real file's form, with the given entries in noteWords, one to a line.
    private static string Sample(IEnumerable<string> entries) =>
        "export const strings = {\n  appName: 'Grow2Notes',\n  noteWords: {\n"
        + string.Concat(entries.Select(entry => $"    {entry}\n"))
        + "  },\n  note: { loading: 'Loading' },\n};\n";

    [GeneratedRegex(@"^  noteWords: \{(?<body>.*?)^  \},", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex NoteWordsObject();

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comment();

    // A key in camelCase, not the end of a longer word, and its words in single quotes, which hold no quote or
    // backslash.
    [GeneratedRegex(@"(?<![A-Za-z0-9_.])(?<key>[a-z][A-Za-z0-9]*)\s*:\s*'(?<words>[^'\\\n]*)'")]
    private static partial Regex Entry();
}
