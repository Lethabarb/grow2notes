using System.Net;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform.Audit;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>get_guide_prompts</c> and <c>set_guide_prompts</c> (mcp-server.md §5.2, S07.03.06, S07.03.07) through the real
/// <c>/mcp</c>: the text as stored, its version, what a change stores and answers, and the one audit row each call
/// writes. The guards that every tool shares are in <see cref="ToolMatrixTests"/>. The cases share the app of
/// <see cref="ToolWorld"/> and run one after the other, each storing the prompts it reads or changes.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class GuidePromptsToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Tool = "get_guide_prompts";
    private const string Set = "set_guide_prompts";

    private const string HelpText =
        "Changes apply straight away to every empty Guided notes box, including open drafts. Prompts are a guide " +
        "only. They disappear as soon as someone starts typing and are not saved into notes.";

    private const string Changed =
        "The guide prompts were changed since you looked them up. Look them up again with get_guide_prompts, " +
        "then retry.";

    // A mark in a refused value, which no refusal may repeat.
    private const string Mark = "Zq9x";

    // The value (with {BEL}, {TAB} and {RLO} for characters that a test's text cannot hold) and the app's message.
    public static TheoryData<string, string> Refusals { get; } = new()
    {
        { Mark + "{TAB}", "Guide prompts has a control or formatting character" },
        { Mark + "{BEL}", "Guide prompts has a control or formatting character" },
        { Mark + "{RLO}", "Guide prompts has a control or formatting character" },
        { Mark + new string('a', 997), "Guide prompts must be 1,000 characters or less" },
    };

    public static TheoryData<string> WrongVersions { get; } = ["stale", "missing"];

    private static readonly string Bidi = char.ConvertFromUtf32(0x202E);

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task Multi_line_prompts_are_returned_exactly_as_stored_with_their_version()
    {
        const string stored = "Ask how the day went.\nWhat did they enjoy?\r\n\r\n  Anything to follow up?\n";
        await Scene.SetGuidePromptsAsync(stored);

        var (result, row) = await world.CallAsync(Tool, new());

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(stored, content.GetProperty("text").GetString());
        Assert.Equal(await Scene.GuidePromptsVersionAsync(), content.GetProperty("expectedVersion").GetString());
        Assert.Equal(
            ["expectedVersion", "text"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        AssertRead(row, AssistantFlow.ClientId, Tool);
        Assert.Null(row.ParticipantId);
    }

    [Fact]
    public async Task No_prompts_are_an_empty_text_with_a_version()
    {
        await Scene.SetGuidePromptsAsync("");

        var (result, row) = await world.CallAsync(Tool, new());

        var content = Content(result);
        Assert.Equal("", content.GetProperty("text").GetString());
        Assert.Equal(await Scene.GuidePromptsVersionAsync(), content.GetProperty("expectedVersion").GetString());
        AssertRead(row, AssistantFlow.ClientId, Tool);
    }

    [Fact]
    public async Task The_version_changes_when_the_stored_text_does()
    {
        await Scene.SetGuidePromptsAsync("First");
        var (first, _) = await world.CallAsync(Tool, new());
        await Scene.SetGuidePromptsAsync("Second");

        var (second, _) = await world.CallAsync(Tool, new());

        Assert.Equal("Second", Content(second).GetProperty("text").GetString());
        Assert.NotEqual(
            Content(first).GetProperty("expectedVersion").GetString(),
            Content(second).GetProperty("expectedVersion").GetString());
    }

    [Fact]
    public async Task A_manager_of_another_organisation_gets_that_organisations_prompts_and_none_of_the_first()
    {
        var stored = $"Prompts {Scene.Token}";
        await Scene.SetGuidePromptsAsync(stored);

        var (result, row) = await world.CallAsync(Tool, new(), ToolCaller.ManagerOfAnotherOrganisation);

        Assert.Equal("", Content(result).GetProperty("text").GetString());
        Assert.DoesNotContain(stored, Body(result));
        AssertRead(row, AssistantFlow.ClientId, Tool);
    }

    [Fact]
    public async Task Multi_line_text_is_stored_with_its_inner_line_breaks_and_trimmed_ends()
    {
        await Scene.SetGuidePromptsAsync("Before");
        var version = await Scene.GuidePromptsVersionAsync();

        var (result, row) = await world.CallAsync(Set, SetArguments("  One\nTwo\r\n\r\nThree \n", version));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        Assert.Equal(await Scene.GuidePromptsVersionAsync(), content.GetProperty("expectedVersion").GetString());
        Assert.NotEqual(version, content.GetProperty("expectedVersion").GetString());
        Assert.Equal(
            ["changed", "expectedVersion", "helpText"],
            content.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("One\nTwo\r\n\r\nThree", await StoredAsync());
        AssertChanged(row, AuditEventTypes.GuidePrompts.Updated, Set, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task Text_that_is_only_white_space_is_stored_as_no_prompts()
    {
        await Scene.SetGuidePromptsAsync("Before");

        var (result, row) = await world.CallAsync(
            Set, SetArguments(" \n  \r\n ", await Scene.GuidePromptsVersionAsync()));

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal("", await StoredAsync());
        AssertChanged(row, AuditEventTypes.GuidePrompts.Updated, Set, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task One_thousand_characters_after_trimming_are_accepted_with_line_breaks_counted()
    {
        await Scene.SetGuidePromptsAsync("Before");
        var text = "a\r\n" + new string('b', 997);

        var (result, _) = await world.CallAsync(
            Set, SetArguments("  " + text + "  \n", await Scene.GuidePromptsVersionAsync()));

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(text, await StoredAsync());
        Assert.Equal(1000, text.Length);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refused_text_gets_the_apps_message_without_the_text_and_nothing_is_stored(
        string value, string message)
    {
        await Scene.SetGuidePromptsAsync("Before");

        var (result, row) = await world.CallAsync(
            Set,
            SetArguments(
                value.Replace("{TAB}", "\t").Replace("{BEL}", "\u0007").Replace("{RLO}", Bidi),
                await Scene.GuidePromptsVersionAsync()));

        AssertValidationFailed(result, "text", message);
        Assert.DoesNotContain(Mark, Body(result));
        AssertRefused(row, Set, "validation.failed");
        Assert.Equal("Before", await StoredAsync());
    }

    [Fact]
    public async Task The_version_that_a_change_returns_works_for_the_next_change()
    {
        await Scene.SetGuidePromptsAsync("Before");
        var (first, _) = await world.CallAsync(
            Set, SetArguments("First", await Scene.GuidePromptsVersionAsync()));
        var version = Content(first).GetProperty("expectedVersion").GetString()!;

        var (second, row) = await world.CallAsync(Set, SetArguments("Second\nline", version));

        Assert.NotEqual(true, second.IsError);
        Assert.True(Content(second).GetProperty("changed").GetBoolean());
        Assert.Equal("Second\nline", await StoredAsync());
        AssertChanged(row, AuditEventTypes.GuidePrompts.Updated, Set, Scene.Organisation.ManagerId);
    }

    [Fact]
    public async Task Text_that_was_read_back_and_is_sent_again_answers_changed_false_with_the_stored_version()
    {
        await Scene.SetGuidePromptsAsync("Ask how the day went.\r\n\r\nWhat did they enjoy?");
        var (read, _) = await world.CallAsync(Tool, new());
        var text = Content(read).GetProperty("text").GetString()!;
        var version = Content(read).GetProperty("expectedVersion").GetString()!;

        var (result, row) = await world.CallAsync(Set, SetArguments(text, version));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("changed").GetBoolean());
        Assert.Equal(version, content.GetProperty("expectedVersion").GetString());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        AssertRead(row, AssistantFlow.ClientId, Set);
        Assert.Equal(text, await StoredAsync());
    }

    [Fact]
    public async Task Text_stored_with_white_space_at_its_ends_by_another_route_changes_once_when_sent_again()
    {
        await Scene.SetGuidePromptsAsync("Padded\n");
        var (read, _) = await world.CallAsync(Tool, new());

        var (result, row) = await world.CallAsync(
            Set,
            SetArguments(
                Content(read).GetProperty("text").GetString()!,
                Content(read).GetProperty("expectedVersion").GetString()));

        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal("Padded", await StoredAsync());
        AssertChanged(row, AuditEventTypes.GuidePrompts.Updated, Set, Scene.Organisation.ManagerId);
    }

    // Both orders (R13): the tool answers a call that changes nothing before it looks at the version, and the app
    // refuses the same stale version (412) or a missing one (428) at once.
    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_no_change_call_answers_changed_false_whatever_the_version_but_the_app_refuses(string version)
    {
        await Scene.SetGuidePromptsAsync("First");
        var stale = await Scene.GuidePromptsVersionAsync();
        await Scene.SetGuidePromptsAsync("Second");
        var current = await Scene.GuidePromptsVersionAsync();

        var (result, row) = await world.CallAsync(Set, SetArguments("Second", version == "stale" ? stale : null));

        Assert.NotEqual(true, result.IsError);
        Assert.False(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal(current, Content(result).GetProperty("expectedVersion").GetString());
        AssertRead(row, AssistantFlow.ClientId, Set);
        Assert.Equal("Second", await StoredAsync());

        using var browser = await world.Flow.SignInAsync(Scene.Organisation.ManagerId);
        using var response = await browser.PutWithTokenAsync(
            "/api/admin/guide-prompts",
            await browser.FetchTokenAsync(),
            new { text = "Second" },
            version == "stale" ? $"\"{stale}\"" : null);
        Assert.Equal(
            version == "stale" ? HttpStatusCode.PreconditionFailed : HttpStatusCode.PreconditionRequired,
            response.StatusCode);
        Assert.Equal("Second", await StoredAsync());
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("missing")]
    [InlineData("empty")]
    public async Task A_wrong_version_is_refused_when_the_text_would_change_and_nothing_is_stored(string version)
    {
        await Scene.SetGuidePromptsAsync("Before");

        var (result, row) = await world.CallAsync(
            Set,
            SetArguments(
                "After",
                version switch
                {
                    "stale" => "AAAAAAAAAAA=",
                    "empty" => "",
                    _ => null,
                }));

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        var (code, message) = version == "stale"
            ? ("precondition.failed", Changed)
            : ("precondition.required", "Send the expectedVersion that the look-up returned.");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
        AssertRefused(row, Set, code);
        Assert.Equal("Before", await StoredAsync());
    }

    [Fact]
    public async Task A_Support_person_can_set_the_prompts()
    {
        await Scene.SetGuidePromptsAsync("Before");

        var (result, row) = await world.CallAsync(
            Set, SetArguments("By Support\nline two", await Scene.GuidePromptsVersionAsync()), ToolCaller.Support);

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal("By Support\nline two", await StoredAsync());
        AssertChanged(row, AuditEventTypes.GuidePrompts.Updated, Set, Scene.Organisation.SupportId);
    }

    private static JsonObject SetArguments(string text, string? version)
    {
        var arguments = new JsonObject { ["text"] = text };
        if (version is not null)
        {
            arguments["expectedVersion"] = version;
        }

        return arguments;
    }

    private async Task<string> StoredAsync()
    {
        var (result, _) = await world.CallAsync(Tool, new());
        return Content(result).GetProperty("text").GetString()!;
    }
}
