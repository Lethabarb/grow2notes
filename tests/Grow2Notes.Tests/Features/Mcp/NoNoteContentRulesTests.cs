using System.Reflection;
using Grow2Notes.Tests.Features.Mcp.Planted;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.Tools;
using Grow2Notes.Web.Features.Participants;
using Mono.Cecil;
using NetArchTest.Rules;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// No tool can reach what a note says (mcp-server.md §6.3 rule 1, S07.02.06): nothing under
/// <c>Grow2Notes.Web.Features.Mcp</c>, and none of the handlers that the tools call (<see cref="ToolHandlers"/>), may
/// depend on a note type, on <c>AuditEvent</c>, or on the Notes, Reviews, Reports or Today features. Writing the audit
/// row goes through <c>IAuditWriter</c> and <c>McpCall</c>, which are allowed. Planted violations show that the rule
/// sees each way in, and the non-vacuity facts that it is looking at something.
/// </summary>
public sealed class NoNoteContentRulesTests
{
    private const string McpNamespace = "Grow2Notes.Web.Features.Mcp";

    // Prefixes of full names: Data.Note covers every note type (Note, NoteDraft, NoteReview, NoteSection, NoteStatus,
    // NoteVersion, NoteVersionGroup and NoteVersionItem), and the dots stop a feature's name from matching another's.
    private static readonly string[] Forbidden =
    [
        "Grow2Notes.Web.Data.Note",
        "Grow2Notes.Web.Data.AuditEvent",
        "Grow2Notes.Web.Features.Notes.",
        "Grow2Notes.Web.Features.Reviews.",
        "Grow2Notes.Web.Features.Reports.",
        "Grow2Notes.Web.Features.Today.",
    ];

    private static readonly Type[] NoteTypes =
    [
        typeof(Note),
        typeof(NoteDraft),
        typeof(NoteReview),
        typeof(NoteSection),
        typeof(NoteStatus),
        typeof(NoteVersion),
        typeof(NoteVersionGroup),
        typeof(NoteVersionItem),
    ];

    // The classes, outside Features.Mcp, whose members the tools call. Each later task adds every handler its tools
    // call, so that what they read is held to this rule too.
    internal static IReadOnlyList<Type> ToolHandlers { get; } = [typeof(ParticipantList), typeof(GoalList)];

    public static TheoryData<string> PlantedViolations { get; } =
    [
        typeof(PlantedNoteField).FullName!,
        typeof(PlantedNoteReviewParameter).FullName!,
        typeof(PlantedTodayReference).FullName!,
        typeof(PlantedNotesQuery).FullName!,
        typeof(PlantedReviewCommentQuery).FullName!,
        typeof(PlantedDraftSetQuery).FullName!,
        typeof(PlantedAuditQuery).FullName!,
        typeof(PlantedAuditSetQuery).FullName!,
        typeof(PlantedAuditDetailsQuery).FullName!,
    ];

    private static Assembly Web => typeof(ParticipantsTools).Assembly;

    /// <summary>
    /// Runs the rule over the types of <paramref name="assembly"/> that <paramref name="selected"/> chooses by the full
    /// name of the outermost type that holds them, so that the state machines and closures inside a method are chosen
    /// with it. This is the one function that the clean pass and every planted violation go through.
    /// </summary>
    internal static RuleRun Run(Assembly assembly, Func<string, bool> selected)
    {
        var selection = new Selection(selected);
        var result = Types.InAssembly(assembly).That().MeetCustomRule(selection)
            .Should().NotHaveDependencyOnAny(Forbidden)
            .GetResult();
        return new([.. selection.Names.Order(StringComparer.Ordinal)], result.FailingTypeNames ?? []);
    }

    [Fact]
    public void Nothing_in_Features_Mcp_or_a_tool_handler_depends_on_note_content()
    {
        var run = Run(Web, IsMcpOrHandler);

        Assert.Empty(run.Failing);
    }

    [Fact]
    public void Every_forbidden_entry_names_something_the_web_assembly_has()
    {
        var names = Web.GetTypes().Select(type => type.FullName!).ToList();

        Assert.All(
            Forbidden, entry => Assert.Contains(names, name => name.StartsWith(entry, StringComparison.Ordinal)));
    }

    [Fact]
    public void The_note_prefix_covers_every_note_type()
    {
        Assert.All(NoteTypes, type => Assert.StartsWith("Grow2Notes.Web.Data.Note", type.FullName));
    }

    [Fact]
    public void The_selection_holds_the_tools_the_look_ups_the_filter_and_the_handlers()
    {
        var run = Run(Web, IsMcpOrHandler);

        Assert.NotEmpty(run.Selected);
        Assert.Contains(typeof(ParticipantsTools).FullName, run.Selected);
        Assert.Contains(typeof(ParticipantLookups).FullName, run.Selected);
        Assert.Contains(typeof(McpAuditFilter).FullName, run.Selected);
        Assert.All(ToolHandlers, handler => Assert.Contains(handler.FullName, run.Selected));
    }

    [Fact]
    public void The_selection_holds_the_types_inside_the_methods_too()
    {
        var run = Run(Web, IsMcpOrHandler);

        var inside = typeof(ParticipantLookups).FullName + "/";
        Assert.Contains(run.Selected, name => name.StartsWith(inside, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(PlantedViolations))]
    public void The_rule_reports_each_planted_violation(string planted)
    {
        var run = Run(typeof(NoNoteContentRulesTests).Assembly, name => name == planted);

        Assert.Contains(planted, run.Selected);
        Assert.NotEmpty(run.Failing);
    }

    private static bool IsMcpOrHandler(string fullName) =>
        fullName.StartsWith(McpNamespace + ".", StringComparison.Ordinal)
        || ToolHandlers.Any(handler => handler.FullName == fullName);

    /// <summary>What <see cref="Run"/> looked at, and the types of it that broke the rule.</summary>
    internal sealed record RuleRun(IReadOnlyList<string> Selected, IReadOnlyList<string> Failing);

    // Chooses a type by the full name of its outermost declaring type, and notes the types it chose.
    private sealed class Selection(Func<string, bool> selected) : ICustomRule
    {
        public HashSet<string> Names { get; } = [];

        public bool MeetsRule(TypeDefinition type)
        {
            var outermost = type;
            while (outermost.DeclaringType is not null)
            {
                outermost = outermost.DeclaringType;
            }

            if (!selected(outermost.FullName))
            {
                return false;
            }

            Names.Add(type.FullName);
            return true;
        }
    }
}
