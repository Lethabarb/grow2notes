using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The SPA's <c>src/limits.ts</c> holds the length limits that its forms check, each named after the
/// <see cref="Limits"/> constant it mirrors, in camelCase (D71, form-validation.md), so the client cannot drift from
/// the server. The test reads the file as text, so it needs neither a build of the SPA nor an OpenAPI document. Its
/// reader reports what it cannot read rather than pass over it, so a new way of writing the file stops the test instead
/// of leaving a limit unchecked.
/// </summary>
public sealed partial class SpaLimitsTests
{
    private const string NoLimit =
        "Read no limit from limits.ts, which holds them as export const limits = { name: number, … } as const;";

    [Fact]
    public void Each_limit_in_limits_ts_is_the_Limits_constant_of_the_same_name()
    {
        var limitsTs = File.ReadAllText(Path.Combine(Repository.Root(), "src", "grow2notes-spa", "src", "limits.ts"));

        Assert.Empty(Problems(limitsTs));
    }

    [Fact]
    public void A_sample_with_comments_and_digit_separators_passes()
    {
        const string sample = """
            /** The limits. */
            export const limits = {
              /** A minimum. */
              passwordMinLength: 12,
              // A maximum, its digits grouped as JavaScript allows.
              guidedNotes: 20_000,
              reviewComment: 500 // with no comma after the last
            } as const;
            """;

        Assert.Empty(Problems(sample));
    }

    [Fact]
    public void A_limit_that_differs_from_its_constant_fails()
    {
        Assert.Equal(
            ["passwordMinLength is 11, where Limits.PasswordMinLength is 12."],
            Problems(Sample("passwordMinLength: 11,")));
    }

    [Theory]
    [InlineData("passwordMaxLength", "PasswordMaxLength")]
    [InlineData("passwordminlength", "Passwordminlength")] // the name's letter case counts
    public void A_limit_with_no_constant_of_its_name_fails(string key, string constant)
    {
        Assert.Equal(
            [$"{key}: Limits has no constant {constant}."],
            Problems(Sample("passwordMinLength: 12,", $"{key}: 12,")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("// No limit yet.\n")]
    [InlineData("export const limits = {} as const;\n")]
    [InlineData("export const limits = {\n  // passwordMinLength: 12,\n} as const;\n")]
    [InlineData("export const LIMITS = {\n  passwordMinLength: 12,\n} as const;\n")]
    [InlineData("export const limits = {\n  passwordMinLength: 12,\n};\n")]
    public void A_file_with_no_limit_fails(string sample)
    {
        Assert.Equal([NoLimit], Problems(sample));
    }

    [Theory]
    [InlineData("PasswordMinLength: 12")]
    [InlineData("passwordMinLength: 6 * 2")]
    [InlineData("passwordMinLength: '12'")]
    [InlineData("passwordMinLength")]
    [InlineData("...serverLimits")]
    public void A_limit_written_another_way_fails(string entry)
    {
        Assert.Equal(
            [$"Cannot read \"{entry}\" in limits.ts, where each limit is name: number."],
            Problems(Sample("passwordMinLength: 12,", $"{entry},")));
    }

    /// <summary>
    /// What is wrong with <paramref name="limitsTs"/>: each limit whose value differs from the <see cref="Limits"/>
    /// constant of its name in PascalCase, or that has no such constant; each entry it cannot read; or that it reads no
    /// limit at all.
    /// </summary>
    private static List<string> Problems(string limitsTs)
    {
        var constants = typeof(Limits)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .ToDictionary(field => field.Name, field => (int)field.GetRawConstantValue()!);
        var body = LimitsObject().Match(limitsTs) is { Success: true } found
            ? Comment().Replace(found.Groups["body"].Value, "")
            : "";
        var entries = body.Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();
        if (entries.Count == 0)
        {
            return [NoLimit];
        }

        List<string> problems = [];
        foreach (var entry in entries)
        {
            if (Limit().Match(entry) is not { Success: true } limit)
            {
                problems.Add($"Cannot read \"{entry}\" in limits.ts, where each limit is name: number.");
                continue;
            }

            var key = limit.Groups["key"].Value;
            var value = int.Parse(
                limit.Groups["value"].Value.Replace("_", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
            var name = char.ToUpperInvariant(key[0]) + key[1..];
            if (!constants.TryGetValue(name, out var constant))
            {
                problems.Add($"{key}: Limits has no constant {name}.");
            }
            else if (value != constant)
            {
                problems.Add($"{key} is {value}, where Limits.{name} is {constant}.");
            }
        }

        return problems;
    }

    // A limits.ts in the real file's form, holding the given entries, one to a line.
    private static string Sample(params string[] entries) =>
        $"export const limits = {{\n{string.Concat(entries.Select(entry => $"  {entry}\n"))}}} as const;\n";

    [GeneratedRegex(
        @"^export const limits = \{(?<body>.*?)\} as const;", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex LimitsObject();

    [GeneratedRegex(@"/\*.*?\*/|//[^\n]*", RegexOptions.Singleline)]
    private static partial Regex Comment();

    // The key in camelCase, and a whole number, its digits perhaps grouped by underscores as in 20_000.
    [GeneratedRegex(@"^(?<key>[a-z][A-Za-z0-9]*)\s*:\s*(?<value>[0-9]+(?:_[0-9]+)*)$")]
    private static partial Regex Limit();
}
