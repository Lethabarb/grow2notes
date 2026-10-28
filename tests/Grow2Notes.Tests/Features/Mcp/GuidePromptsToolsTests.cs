using Grow2Notes.Tests.Fixtures;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>get_guide_prompts</c> (mcp-server.md §5.2, S07.03.06) through the real <c>/mcp</c>: the text as stored, its
/// version, and the one audit row each call writes, which names no ID. The guards that every tool shares are in
/// <see cref="ToolMatrixTests"/>. The cases share the app of <see cref="ToolWorld"/> and run one after the other, each
/// storing the prompts it reads.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class GuidePromptsToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Tool = "get_guide_prompts";

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
}
