using System.Text.RegularExpressions;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// Reads design.md, for a test that checks the code against the design itself, not a copy of it, so the two cannot
/// drift apart. Each reader fails on what it cannot read, such as a second table, rather than pass over it, so a new
/// way of writing the design stops the test instead of leaving something unchecked.
/// </summary>
internal static partial class DesignMd
{
    public static string Read() => File.ReadAllText(Path.Combine(Repository.Root(), "design", "design.md"));

    /// <summary>
    /// The lines under the one heading in <paramref name="design"/> that starts with <paramref name="heading"/>, up to
    /// the next heading of the same level or a higher one.
    /// </summary>
    public static List<string> Section(string design, string heading) =>
        Section(design.ReplaceLineEndings("\n").Split('\n'), heading);

    /// <inheritdoc cref="Section(string, string)"/>
    public static List<string> Section(IReadOnlyList<string> lines, string heading)
    {
        var found = lines.Index().Where(line => line.Item.StartsWith(heading, StringComparison.Ordinal)).ToList();
        if (found is not [var (start, title)])
        {
            throw new InvalidOperationException($"Expected one heading starting \"{heading}\", found {found.Count}.");
        }

        var level = HeadingLevel(title);
        return [.. lines.Skip(start + 1).TakeWhile(line => HeadingLevel(line) > level)];
    }

    /// <summary>
    /// The body rows of the one table in <paramref name="lines"/>, each cell keyed by its column's name. A second
    /// table, or the one table split in two by a blank or prose line, throws rather than leave its rows unread.
    /// </summary>
    public static List<Dictionary<string, string>> OnlyTable(IReadOnlyList<string> lines, string section)
    {
        var tables = lines.Where((line, i) => IsTableRow(line) && (i == 0 || !IsTableRow(lines[i - 1]))).Count();
        if (tables != 1)
        {
            throw new InvalidOperationException($"design.md {section} has {tables} tables, where this test reads one.");
        }

        var rows = lines
            .SkipWhile(line => !IsTableRow(line))
            .TakeWhile(IsTableRow)
            .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .ToArray();
        if (rows is not [var columns, var underline, .. var body] || !underline.All(TableUnderline().IsMatch))
        {
            throw new InvalidOperationException(
                $"design.md {section} has a table with no |---| line under its column names.");
        }

        return [.. body.Select(cells => columns.Zip(cells).ToDictionary(cell => cell.First, cell => cell.Second))];
    }

    /// <summary>
    /// The lines inside the one fenced <c>text</c> block in <paramref name="lines"/>. A second fenced block, or a fence
    /// left open, throws rather than leave its lines unread.
    /// </summary>
    public static List<string> OnlyTextBlock(IReadOnlyList<string> lines, string section)
    {
        var fences = lines.Index().Where(line => line.Item.StartsWith("```", StringComparison.Ordinal)).ToList();
        if (fences is not [(var start, "```text"), (var end, "```")])
        {
            throw new InvalidOperationException(
                $"design.md {section} has {fences.Count} fences, where this test reads one ```text block.");
        }

        return [.. lines.Skip(start + 1).Take(end - start - 1)];
    }

    // A line that is not a heading ranks below every heading, so a section runs on through it.
    private static int HeadingLevel(string line) =>
        Heading().Match(line) is { Success: true } heading ? heading.Groups[1].Length : int.MaxValue;

    private static bool IsTableRow(string line) => line.StartsWith('|');

    [GeneratedRegex(@"^(#{1,6}) ")]
    private static partial Regex Heading();

    [GeneratedRegex("^:?-+:?$")]
    private static partial Regex TableUnderline();
}
