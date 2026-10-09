using System.Text.RegularExpressions;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// design.md §9.7's security headers, read from its text block (<see cref="DesignMd"/>), for a test that checks a
/// response's headers against the design itself, not a copy of it.
/// </summary>
internal static partial class DesignedSecurityHeaders
{
    /// <summary>Each header of design.md §9.7, by name, with its value on one line.</summary>
    public static Dictionary<string, string> Read() => In(DesignMd.Read());

    /// <summary>
    /// Each header in the text block of <paramref name="design"/>'s §9.7, by name, with its value on one line: a line
    /// that starts with a space is the value above it wrapped, and is joined to it with one space. A line that is
    /// neither a header nor a wrapped value, a header named twice, or a block with no header throws rather than leave
    /// something unchecked.
    /// </summary>
    public static Dictionary<string, string> In(string design)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? name = null;
        foreach (var line in DesignMd.OnlyTextBlock(DesignMd.Section(design, "### 9.7 "), "§9.7"))
        {
            if (name is not null && WrappedValue().Match(line) is { Success: true } wrapped)
            {
                headers[name] += $" {wrapped.Groups["value"].Value}";
            }
            else if (Header().Match(line) is { Success: true } header)
            {
                name = header.Groups["name"].Value;
                if (!headers.TryAdd(name, header.Groups["value"].Value))
                {
                    throw new InvalidOperationException($"design.md §9.7 has {name} twice.");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"design.md §9.7 has the line \"{line}\" in its headers, which this test cannot read.");
            }
        }

        return headers.Count > 0
            ? headers
            : throw new InvalidOperationException("design.md §9.7 has no header in its text block.");
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z]+(-[A-Za-z]+)*): (?<value>\S.*?)\s*$")]
    private static partial Regex Header();

    [GeneratedRegex(@"^\s+(?<value>\S.*?)\s*$")]
    private static partial Regex WrappedValue();
}
