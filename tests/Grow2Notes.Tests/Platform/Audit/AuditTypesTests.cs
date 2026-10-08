using System.Reflection;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;

namespace Grow2Notes.Tests.Platform.Audit;

/// <summary>
/// The event and entity type constants against design.md itself, not a copy of it, so the two cannot drift apart: when
/// an event is added to §5.4's catalogue, as Release 2's are (S07.02.03), the first test asks for its constant. Reading
/// the design fails on what it cannot read, such as a backticked word that is not a name or a second table, rather
/// than pass over it, so a new way of writing the catalogue stops the test instead of leaving an event without a
/// constant.
/// </summary>
public sealed partial class AuditTypesTests
{
    [Fact]
    public void The_event_type_constants_are_exactly_the_events_of_design_md_5_4() =>
        AssertSameNames("event type", EventTypesIn(DesignMd()), ConstantsIn(typeof(AuditEventTypes)));

    [Fact]
    public void The_entity_type_constants_are_exactly_the_entity_types_of_design_md_5_3() =>
        AssertSameNames("entity type", EntityTypesIn(DesignMd()), ConstantsIn(typeof(AuditEntityTypes)));

    [Fact]
    public void Every_event_type_fits_the_EventType_column() =>
        Assert.All(
            ConstantsIn(typeof(AuditEventTypes)),
            name => Assert.InRange(name.Length, 1, Limits.AuditEventType));

    [Fact]
    public void Every_entity_type_fits_the_EntityType_column() =>
        Assert.All(
            ConstantsIn(typeof(AuditEntityTypes)),
            name => Assert.InRange(name.Length, 1, Limits.AuditEntityType));

    [Fact]
    public void Reading_the_catalogue_expands_the_same_five_from_the_goal_events_and_reads_its_table_alone()
    {
        const string design = """
            ### 5.4 Audit event catalogue (A28)

            | Group | Events |
            |---|---|
            | Notes | `note.submitted` (v1), `note.edited` (v2 and later) |
            | Configuration | `goal.a`, `goal.b`, `goal.c`, `goal.d`, `goal.e`; the same five with a `item.` prefix |

            Reading a note is not audited, so there is no `note.read`.

            ### 5.5 The next section

            | Group | Events |
            |---|---|
            | Next | `next.section` |
            """;

        Assert.Equal(
            [
                "goal.a", "goal.b", "goal.c", "goal.d", "goal.e", "item.a", "item.b", "item.c", "item.d", "item.e",
                "note.edited", "note.submitted",
            ],
            EventTypesIn(design));
    }

    private const string Columns = "| Group | Events |\n|---|---|\n";

    [Theory]
    [InlineData(Columns + "| Planted | `goal.a`; the same six with a `item.` prefix |", "`item.`")]
    [InlineData(Columns + "| Planted | `Goal.created` |", "`Goal.created`")]
    [InlineData(Columns + "| Planted | `created` |", "`created`")]
    [InlineData(Columns + "| Planted | `goal.a`, `goal.b`; the same five with a `item.` prefix |", "2 goal. events")]
    [InlineData(
        Columns + "| Notes | `note.a` |\n\n| Event | When |\n|---|---|\n| `mcp.read` | Every look-up |",
        "2 tables")]
    [InlineData(Columns + "| Notes | `note.a` |\nAnd the operator's:\n| Operator | `admin.a` |", "2 tables")]
    [InlineData("Events are written as `note.a`.", "0 tables")]
    [InlineData("| Group | Events |\n| Notes | `note.a` |", "no |---| line")]
    public void Reading_the_catalogue_refuses_what_it_cannot_read(string table, string named)
    {
        var design = $"""
            ### 5.4 Audit event catalogue (A28)

            {table}
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => EventTypesIn(design));

        Assert.Contains(named, ex.Message, StringComparison.Ordinal);
    }

    private static string DesignMd() => File.ReadAllText(Path.Combine(Repository.Root(), "design", "design.md"));

    /// <summary>
    /// The events of §5.4's catalogue: the backticked names in its table's Events column, with each "the same five with
    /// a <c>prefix.</c> prefix" expanded from the <c>goal.</c> events, as the catalogue writes the common item and
    /// common item group events.
    /// </summary>
    private static SortedSet<string> EventTypesIn(string design)
    {
        var events = OnlyTable(Section(Lines(design), "### 5.4 "), "§5.4").Select(row => row["Events"]).ToList();
        var prefixes = events
            .SelectMany(cell => SameFiveWithAPrefix().Matches(cell))
            .Select(match => match.Groups[1].Value)
            .ToList();
        var named = events
            .SelectMany(cell => BacktickedNames(SameFiveWithAPrefix().Replace(cell, ""), EventName(), "§5.4"))
            .ToList();

        const string goal = "goal.";
        var goalActions = named
            .Where(name => name.StartsWith(goal, StringComparison.Ordinal))
            .Select(name => name[goal.Length..])
            .Distinct()
            .ToList();
        if (prefixes.Count > 0 && goalActions.Count != 5)
        {
            throw new InvalidOperationException(
                $"design.md §5.4 has {goalActions.Count} {goal} events to take \"the same five\" from.");
        }

        return new(
            named.Concat(from prefix in prefixes from action in goalActions select prefix + action),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The entity types of §5.3: the backticked names in the Notes of the <c>EntityType</c> row of its
    /// <c>AuditEvent</c> table.
    /// </summary>
    private static SortedSet<string> EntityTypesIn(string design)
    {
        var auditEvent = Section(Section(Lines(design), "### 5.3 "), "#### AuditEvent");
        var entityType = OnlyTable(auditEvent, "§5.3").Single(row => row["Field"] == "EntityType");
        return new(BacktickedNames(entityType["Notes"], EntityTypeName(), "§5.3"), StringComparer.Ordinal);
    }

    private static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');

    /// <summary>
    /// The lines under the one heading that starts with <paramref name="heading"/>, up to the next heading of the same
    /// level or a higher one.
    /// </summary>
    private static List<string> Section(IReadOnlyList<string> lines, string heading)
    {
        var found = lines.Index().Where(line => line.Item.StartsWith(heading, StringComparison.Ordinal)).ToList();
        if (found is not [var (start, title)])
        {
            throw new InvalidOperationException($"Expected one heading starting \"{heading}\", found {found.Count}.");
        }

        var level = HeadingLevel(title);
        return [.. lines.Skip(start + 1).TakeWhile(line => HeadingLevel(line) > level)];
    }

    // A line that is not a heading ranks below every heading, so a section runs on through it.
    private static int HeadingLevel(string line) =>
        Heading().Match(line) is { Success: true } heading ? heading.Groups[1].Length : int.MaxValue;

    /// <summary>
    /// The body rows of the one table in <paramref name="lines"/>, each cell keyed by its column's name. A second
    /// table, or the one table split in two by a blank or prose line, throws rather than leave its rows unread.
    /// </summary>
    private static List<Dictionary<string, string>> OnlyTable(IReadOnlyList<string> lines, string section)
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

    private static bool IsTableRow(string line) => line.StartsWith('|');

    private static IEnumerable<string> BacktickedNames(string text, Regex name, string section) =>
        from Match match in Backticked().Matches(text)
        let value = match.Groups[1].Value
        select name.IsMatch(value)
            ? value
            : throw new InvalidOperationException($"design.md {section} has `{value}`, which this test cannot read.");

    /// <summary>
    /// The value of every public <c>const string</c> in <paramref name="type"/> and its public nested classes.
    /// </summary>
    private static List<string> ConstantsIn(Type type) =>
    [
        .. from field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
           where field.IsLiteral && field.FieldType == typeof(string)
           select (string)field.GetRawConstantValue()!,
        .. type.GetNestedTypes().SelectMany(ConstantsIn),
    ];

    private static void AssertSameNames(string kind, SortedSet<string> design, List<string> constants)
    {
        string[] differences =
        [
            .. design.Except(constants, StringComparer.Ordinal)
                .Select(name => $"- {name}: in design.md, but no constant has it"),
            .. constants.Except(design, StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .Select(name => $"- {name}: a constant, but not in design.md"),
            .. constants.GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => $"- {group.Key}: the value of {group.Count()} constants"),
        ];

        if (differences.Length > 0)
        {
            Assert.Fail($"The {kind} constants differ from design.md:\n{string.Join('\n', differences)}");
        }
    }

    [GeneratedRegex(@"^(#{1,6}) ")]
    private static partial Regex Heading();

    [GeneratedRegex("`([^`]*)`")]
    private static partial Regex Backticked();

    // The prefix keeps its dot, as the catalogue writes it, so a prefix plus an action is an event name.
    [GeneratedRegex(@"the same five with a `([a-z_]+\.)` prefix")]
    private static partial Regex SameFiveWithAPrefix();

    [GeneratedRegex("^:?-+:?$")]
    private static partial Regex TableUnderline();

    [GeneratedRegex(@"^[a-z_]+\.[a-z_]+$")]
    private static partial Regex EventName();

    [GeneratedRegex("^[A-Z][A-Za-z]*$")]
    private static partial Regex EntityTypeName();
}
