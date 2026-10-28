using System.Reflection;
using Grow2Notes.Tests.Features.Mcp.Planted;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.CommonItems;
using Grow2Notes.Web.Features.GuidePrompts;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.Tools;
using Grow2Notes.Web.Features.Participants;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Configuration;
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

    // NetArchTest matches a dependency by whole dotted names, not by characters, so a prefix such as Data.Note finds
    // only the type Note, and a trailing dot finds nothing. Names are therefore listed in full: every type of Data
    // whose name starts with these (every note type, and the audit event), and every namespace in or under these.
    private static readonly string[] ForbiddenTypePrefixes =
        ["Grow2Notes.Web.Data.Note", "Grow2Notes.Web.Data.AuditEvent"];

    private static readonly string[] ForbiddenNamespaces =
    [
        "Grow2Notes.Web.Features.Notes",
        "Grow2Notes.Web.Features.Reviews",
        "Grow2Notes.Web.Features.Reports",
        "Grow2Notes.Web.Features.Today",
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
        typeof(AuditEvent),
    ];

    // The classes, outside Features.Mcp, whose members the tools call. Each later task adds every handler its tools
    // call, so that what they read is held to this rule too.
    internal static IReadOnlyList<Type> ToolHandlers { get; } =
        [
            typeof(ParticipantList),
            typeof(GoalList),
            typeof(ParticipantChanges),
            typeof(GoalChanges),
            typeof(ConfigurationChanges),
            typeof(UserList),
            typeof(CommonItemGroupList),
            typeof(GuidePromptsHandler),
        ];

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

    // Computed from the assembly, so that a note type added later is forbidden without a change here.
    private static string[] Forbidden { get; } = ForbiddenEntries();

    /// <summary>
    /// Runs the rule over the types of <paramref name="assembly"/> that <paramref name="selected"/> chooses by name.
    /// NetArchTest searches the types nested in a chosen type too, which is where an async method's body is. This is
    /// the one function that the clean pass and every planted violation go through.
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
    public void Every_forbidden_name_is_one_the_web_assembly_has()
    {
        var names = Web.GetTypes().Select(type => type.FullName!).ToList();

        Assert.All(
            ForbiddenTypePrefixes,
            prefix => Assert.Contains(names, name => name.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.All(
            ForbiddenNamespaces,
            ns => Assert.Contains(names, name => name.StartsWith(ns + ".", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_forbidden_names_cover_every_note_type_and_the_audit_event()
    {
        Assert.All(NoteTypes, type => Assert.Contains(type.FullName, Forbidden));
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

    // Every type of the Web assembly that has one of the forbidden names: the types of Data that start with the type
    // prefixes, and the namespaces in or under the forbidden ones.
    private static string[] ForbiddenEntries()
    {
        var types = Web.GetTypes().Where(type => !type.IsNested).ToList();
        var forbiddenTypes = types
            .Select(type => type.FullName!)
            .Where(name => ForbiddenTypePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
        var forbiddenNamespaces = types
            .Select(type => type.Namespace)
            .OfType<string>()
            .Where(name => ForbiddenNamespaces.Any(
                forbidden => name == forbidden || name.StartsWith(forbidden + ".", StringComparison.Ordinal)))
            .Distinct();
        return [.. forbiddenTypes, .. forbiddenNamespaces];
    }

    // Chooses a type by its full name, and notes the types it chose.
    private sealed class Selection(Func<string, bool> selected) : ICustomRule
    {
        public HashSet<string> Names { get; } = [];

        public bool MeetsRule(TypeDefinition type)
        {
            if (!selected(type.FullName))
            {
                return false;
            }

            Names.Add(type.FullName);
            return true;
        }
    }
}
