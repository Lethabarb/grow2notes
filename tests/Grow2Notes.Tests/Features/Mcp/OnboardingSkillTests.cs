using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The Claude Code skill that onboards a provider (D95, T41) is instructions to a model, so these tests hold what it
/// says to the server: it names only tools the server lists, calls only its ten, and the settings it tells the owner
/// to copy ask before every access tool. It also holds no address, no setup link and no committed MCP configuration.
/// </summary>
public sealed partial class OnboardingSkillTests
{
    private const string ServerPrefix = "mcp__grow2notes__";

    // Adding a tool to the skill is a deliberate change: it is made here and in SKILL.md together.
    private static readonly string[] SkillTools =
    [
        "list_common_items",
        "add_common_item_group",
        "add_common_item",
        "get_guide_prompts",
        "set_guide_prompts",
        "find_participants",
        "get_participant",
        "add_goal",
        "list_users",
        "invite_user",
    ];

    private static readonly string[] AccessTools =
    [
        "invite_user",
        "reset_user_sign_in",
        "deactivate_user",
        "archive_participant",
    ];

    private static readonly string[] DeniedTools = ["WebSearch", "WebFetch", "Bash", "PowerShell", "Write", "Edit"];

    // An error code that is written like a tool name.
    private static readonly string[] ErrorCodes = ["rate_limited"];

    private static readonly string[] FrontMatterKeys = ["name", "description", "disable-model-invocation"];

    public static TheoryData<string, string> PlantedText { get; } = new()
    {
        { "call `list_goals` first", "list_goals" },
        { "ask for mcp__grow2notes__list_goals.", "list_goals" },
        { "\"mcp__grow2notes__invite_user\",", "invite_user" },
        { "user.email_in_use", string.Empty },
        { "retryAfterSeconds", string.Empty },
        { "mcp__grow2notes__*", string.Empty },
        { "Mcp__Enabled", string.Empty },
        { "app-grow2notes-test", string.Empty },
    };

    private static string SkillFolder => Path.Combine(Repository.Root(), ".claude", "skills", "onboard-provider");

    [GeneratedRegex(@"(?<![\w.])(?:mcp__grow2notes__)?(?<name>[a-z]+(?:_[a-z]+)+)\b")]
    private static partial Regex ToolName();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailAddress();

    [Fact]
    public void Every_tool_the_skill_names_is_one_the_server_lists()
    {
        var listed = ServerTools();
        var named = Directory.EnumerateFiles(SkillFolder, "*.md")
            .SelectMany(file => NamesIn(File.ReadAllText(file)))
            .ToHashSet(StringComparer.Ordinal);

        // A moved folder or an ignored file would otherwise pass with nothing to scan.
        Assert.Contains("invite_user", named);

        var unknown = named.Except(listed).Except(ErrorCodes).Order(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            Assert.Fail(
                $"The skill names tool-like words the server does not list: {string.Join(", ", unknown)}. "
                    + "Rename the tool to one the server lists, or write other words with hyphens or dots.");
        }
    }

    [Fact]
    public void The_skill_calls_only_the_ten_tools_of_its_list()
    {
        var fences = Fences(File.ReadAllLines(Path.Combine(SkillFolder, "SKILL.md")));

        var list = Assert.Single(fences);
        Assert.Equal("text", list.Info);
        var tools = list.Lines.Where(line => line.Trim().Length > 0).Select(line => line.Trim()).ToArray();
        Assert.Equal(SkillTools.Order(StringComparer.Ordinal), tools.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_settings_the_owner_copies_ask_before_every_access_tool()
    {
        var fences = Fences(File.ReadAllLines(Path.Combine(SkillFolder, "by-hand.md")));

        var settings = Assert.Single(fences, fence => fence.Info == "json");
        var permissions = JsonNode.Parse(string.Join('\n', settings.Lines))?["permissions"]?.AsObject();
        Assert.NotNull(permissions);

        Assert.Equal(
            AccessTools.Select(tool => ServerPrefix + tool).Order(StringComparer.Ordinal),
            Strings(permissions["ask"]).Order(StringComparer.Ordinal));
        Assert.Equal(
            SkillTools.Where(tool => tool != "invite_user")
                .Select(tool => ServerPrefix + tool)
                .Order(StringComparer.Ordinal),
            Strings(permissions["allow"]).Order(StringComparer.Ordinal));
        var denied = Strings(permissions["deny"]);
        Assert.All(DeniedTools, tool => Assert.Contains(tool, denied));
    }

    [Theory]
    [MemberData(nameof(PlantedText))]
    public void The_scanner_reads_a_tool_name_bare_or_as_claude_code_writes_it(string text, string expected)
    {
        var found = NamesIn(text).ToArray();

        string[] wanted = expected.Length == 0 ? [] : [expected];
        Assert.Equal(wanted, found);
    }

    [Fact]
    public void The_skill_pre_approves_no_tool_and_starts_only_when_the_owner_asks()
    {
        var lines = File.ReadAllLines(Path.Combine(SkillFolder, "SKILL.md"));
        Assert.Equal("---", lines[0]);
        var end = Array.IndexOf(lines, "---", 1);
        Assert.True(end > 0, "SKILL.md's front matter has no closing ---.");
        var frontMatter = lines[1..end];

        Assert.DoesNotContain(frontMatter, line => line.Length == 0 || char.IsWhiteSpace(line[0]));
        var values = frontMatter.ToDictionary(
            line => line[..line.IndexOf(':', StringComparison.Ordinal)],
            line => line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim(),
            StringComparer.Ordinal);
        Assert.Equal(FrontMatterKeys.Order(StringComparer.Ordinal), values.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("onboard-provider", values["name"]);
        Assert.Equal("true", values["disable-model-invocation"]);
        var description = frontMatter.Single(line => line.StartsWith("description:", StringComparison.Ordinal));
        Assert.True(description.Length <= 120, "The description is one line of at most 120 characters.");
    }

    [Fact]
    public void The_template_is_a_header_of_kind_name_and_text_with_no_date_of_birth()
    {
        Assert.Equal(["Kind,Name,Text"], File.ReadAllLines(Path.Combine(SkillFolder, "template.csv")));
    }

    [Fact]
    public void The_skill_holds_no_email_address_or_setup_link()
    {
        var files = Directory.EnumerateFiles(SkillFolder).ToArray();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.False(EmailAddress().IsMatch(text), $"{Path.GetFileName(file)} holds an email address.");
            Assert.DoesNotContain("/setup#", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void No_file_in_the_repository_connects_an_agent_to_a_site()
    {
        // Project scope, in this file, is the only committed MCP configuration that Claude Code reads. Another file
        // in .claude/ is allowed.
        Assert.False(File.Exists(Path.Combine(Repository.Root(), ".mcp.json")));
    }

    private static IEnumerable<string> NamesIn(string text) =>
        ToolName().Matches(text).Select(match => match.Groups["name"].Value);

    private static string[] ServerTools()
    {
        var file = Path.Combine(Repository.Root(), "tests", "Grow2Notes.Tests", "Features", "Mcp", "tools-list.json");
        return [.. JsonNode.Parse(File.ReadAllText(file))!.AsArray().Select(tool => tool!["name"]!.GetValue<string>())];
    }

    private static string[] Strings(JsonNode? array) =>
        [.. array!.AsArray().Select(item => item!.GetValue<string>())];

    private static List<Fence> Fences(string[] lines)
    {
        var fences = new List<Fence>();
        Fence? open = null;
        foreach (var line in lines)
        {
            if (!line.StartsWith("```", StringComparison.Ordinal))
            {
                open?.Lines.Add(line);
            }
            else if (open is null)
            {
                open = new Fence(line[3..].Trim(), []);
            }
            else
            {
                fences.Add(open);
                open = null;
            }
        }

        Assert.Null(open);
        return fences;
    }

    private sealed record Fence(string Info, List<string> Lines);
}
