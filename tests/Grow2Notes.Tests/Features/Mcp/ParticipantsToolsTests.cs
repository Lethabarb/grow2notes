using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>find_participants</c> and <c>get_participant</c> (mcp-server.md §5.2, S07.02.04) through the real <c>/mcp</c>, on
/// the app as production has it, with a token from the real flow: what each returns, the one audit row each call
/// writes, and what each refuses. The guards that every tool shares are in
/// <see cref="ToolMatrixTests"/>. The cases share the app of <see cref="ToolWorld"/>, so each adds what it needs
/// under a family name of its own.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantsToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Find = "find_participants";
    private const string Get = "get_participant";
    private const string MoreMatches = "More matches: narrow the search.";

    private const string NotFoundMessage =
        "No participant has that ID in this organisation. Find them with find_participants.";

    // Controls, the bidirectional controls and a tag character: the code points that TextInput refuses.
    public static TheoryData<int> RefusedCharacters { get; } =
    [
        0x0000, 0x0007, 0x0009, 0x000A, 0x001B, 0x007F, 0x061C, 0x200E, 0x200F, 0x202A, 0x202E, 0x2066, 0x2069, 0xE0041,
    ];

    public static TheoryData<string> ShortSearches { get; } = ["J", " J ", "J   "];

    public static TheoryData<string> UnbindableArguments { get; } = ["no search", "includeArchived of the wrong type"];

    public static TheoryData<string> IdsThatAreNotTheirs { get; } = ["unknown", "not-a-guid", "another organisation"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    private McpClient Client => world.ClientOf(ToolCaller.Manager);

    [Fact]
    public async Task Eleven_matches_give_the_first_ten_in_list_order_with_more_matches_and_audit_only_those_ten()
    {
        var family = $"Eleven{Scene.Token}";
        var added = await AddParticipantsAsync(family, 11);

        var (result, row) = await CallAsync(Find, new() { ["search"] = family });

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(added.Take(10), ParticipantIds(content));
        Assert.Equal(MoreMatches, content.GetProperty("moreMatches").GetString());
        Assert.Null(row.ParticipantId);
        AssertRead(row, AssistantFlow.ClientId, Find, ("participantIds", [.. added.Take(10)]));
    }

    [Fact]
    public async Task Exactly_ten_matches_give_no_more_matches()
    {
        var family = $"Ten{Scene.Token}";
        var added = await AddParticipantsAsync(family, 10);

        var (result, row) = await CallAsync(Find, new() { ["search"] = family });

        var content = Content(result);
        Assert.Equal(added, ParticipantIds(content));
        Assert.False(content.TryGetProperty("moreMatches", out _));
        AssertRead(row, AssistantFlow.ClientId, Find, ("participantIds", [.. added]));
    }

    [Fact]
    public async Task Archived_participants_are_found_only_with_include_archived_and_say_so()
    {
        var family = $"Mixed{Scene.Token}";
        var active = await Flow.App.Services.AddParticipantAsync(
            Scene.Organisation, givenName: "Ada", familyName: family);
        var archived = await Flow.App.Services.AddParticipantAsync(
            Scene.Organisation, archived: true, givenName: "Bea", familyName: family);

        var (without, _) = await CallAsync(Find, new() { ["search"] = family });
        var (with, _) = await CallAsync(Find, new() { ["search"] = family, ["includeArchived"] = true });

        Assert.Equal([active.Id], ParticipantIds(Content(without)));
        Assert.Equal([active.Id, archived.Id], ParticipantIds(Content(with)));
        Assert.Equal(
            ["active", "archived"],
            Content(with).GetProperty("participants").EnumerateArray()
                .Select(match => match.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task One_match_gives_its_names_and_status_and_a_row_with_its_participant()
    {
        var (result, row) = await CallAsync(Find, new() { ["search"] = $"{Scene.GivenName} {Scene.FamilyName}" });

        var match = Assert.Single(Content(result).GetProperty("participants").EnumerateArray());
        Assert.Equal(Scene.Participant.Id, match.GetProperty("participantId").GetGuid());
        Assert.Equal(Scene.GivenName, match.GetProperty("givenName").GetString());
        Assert.Equal(Scene.FamilyName, match.GetProperty("familyName").GetString());
        Assert.Equal("active", match.GetProperty("status").GetString());
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Equal(Scene.Participant.Id, row.ParticipantId);
        AssertRead(row, AssistantFlow.ClientId, Find, ("participantIds", [Scene.Participant.Id]));
    }

    [Fact]
    public async Task No_match_is_an_empty_list_and_an_mcp_read_row_of_count_0()
    {
        var (result, row) = await CallAsync(Find, new() { ["search"] = $"Nobody{Scene.Token}" });

        Assert.NotEqual(true, result.IsError);
        Assert.Empty(Content(result).GetProperty("participants").EnumerateArray());
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Null(row.ParticipantId);
        AssertRead(row, AssistantFlow.ClientId, Find, ("participantIds", []));
    }

    [Theory]
    [MemberData(nameof(ShortSearches))]
    public async Task A_search_under_two_characters_after_trimming_is_refused(string search)
    {
        var (result, row) = await CallAsync(Find, new() { ["search"] = search });

        AssertValidationFailed(result, "search", "Search must be at least 2 characters");
        AssertRefused(row, Find, "validation.failed");
    }

    [Fact]
    public async Task A_search_of_single_letter_words_that_is_long_enough_passes()
    {
        var (result, row) = await CallAsync(Find, new() { ["search"] = "J C" });

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
    }

    [Theory]
    [MemberData(nameof(RefusedCharacters))]
    public async Task A_search_with_a_control_or_bidirectional_character_is_refused_and_not_echoed(int codePoint)
    {
        var search = $"Qz{char.ConvertFromUtf32(codePoint)}{Scene.Token}";

        var (result, row) = await CallAsync(Find, new() { ["search"] = search });

        AssertValidationFailed(result, "search", "Search has a control or formatting character");
        Assert.DoesNotContain(Scene.Token, Body(result), StringComparison.Ordinal);
        AssertRefused(row, Find, "validation.failed");
    }

    [Fact]
    public async Task A_search_of_101_characters_is_refused()
    {
        var (result, row) = await CallAsync(Find, new() { ["search"] = new string('a', 101) });

        AssertValidationFailed(result, "search", "Search must be 100 characters or less");
        AssertRefused(row, Find, "validation.failed");
    }

    [Theory]
    [MemberData(nameof(UnbindableArguments))]
    public async Task Arguments_the_sdk_cannot_bind_are_an_error_result_audited_as_unexpected_without_an_echo(
        string kind)
    {
        JsonObject arguments = kind == "no search"
            ? new()
            : new() { ["search"] = Scene.FamilyName, ["includeArchived"] = Scene.Token };

        var (result, row) = await CallAsync(Find, arguments);

        Assert.True(result.IsError);
        Assert.DoesNotContain(Scene.Token, Body(result), StringComparison.Ordinal);
        AssertRefused(row, Find, "unexpected");
    }

    [Fact]
    public async Task A_participant_comes_back_with_active_goals_in_list_order_then_archived_goals()
    {
        var (result, _) = await CallAsync(Get, new() { ["participantId"] = Scene.Participant.Id.ToString() });

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(Scene.Participant.Id, content.GetProperty("participantId").GetGuid());
        Assert.Equal(Scene.GivenName, content.GetProperty("givenName").GetString());
        Assert.Equal(Scene.FamilyName, content.GetProperty("familyName").GetString());
        Assert.Equal("active", content.GetProperty("status").GetString());
        var goals = content.GetProperty("goals").EnumerateArray().ToList();
        Assert.Equal(Scene.Goals.Select(goal => goal.Id), goals.Select(goal => goal.GetProperty("goalId").GetGuid()));
        Assert.Equal(Scene.Goals.Select(goal => goal.Text), goals.Select(goal => goal.GetProperty("text").GetString()));
        Assert.Equal(
            Scene.Goals.Select(goal => goal.Archived), goals.Select(goal => goal.GetProperty("archived").GetBoolean()));
    }

    [Fact]
    public async Task Each_expected_version_is_the_etag_token_of_its_row()
    {
        var (result, _) = await CallAsync(Get, new() { ["participantId"] = Scene.Participant.Id.ToString() });

        var content = Content(result);
        Assert.Equal(Scene.Participant.ETag, IfMatch.ETag(content.GetProperty("expectedVersion").GetString()!));
        Assert.Equal(
            Scene.Goals.Select(goal => goal.ETag),
            content.GetProperty("goals").EnumerateArray()
                .Select(goal => IfMatch.ETag(goal.GetProperty("expectedVersion").GetString()!)));
    }

    [Fact]
    public async Task An_archived_participant_is_returned_with_the_status_archived()
    {
        var (result, _) = await CallAsync(Get, new() { ["participantId"] = Scene.Archived.Id.ToString() });

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal("archived", content.GetProperty("status").GetString());
        Assert.Equal(Scene.Archived.ETag, IfMatch.ETag(content.GetProperty("expectedVersion").GetString()!));
        Assert.Empty(content.GetProperty("goals").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(IdsThatAreNotTheirs))]
    public async Task An_id_that_is_unknown_not_an_id_or_another_organisations_is_one_not_found(string kind)
    {
        var id = kind switch
        {
            "unknown" => Guid.NewGuid().ToString(),
            "another organisation" => Scene.OtherOrganisation.ParticipantId.ToString(),
            _ => kind,
        };

        var (result, row) = await CallAsync(Get, new() { ["participantId"] = id });

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("not_found", error.GetProperty("code").GetString());
        Assert.Equal(NotFoundMessage, error.GetProperty("message").GetString());
        AssertRefused(row, Get, "not_found");
    }

    [Fact]
    public async Task A_get_audits_the_participant_and_exactly_the_goals_returned()
    {
        var (_, row) = await CallAsync(Get, new() { ["participantId"] = Scene.Participant.Id.ToString() });

        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Equal(Scene.Participant.Id, row.ParticipantId);
        AssertRead(
            row,
            AssistantFlow.ClientId,
            Get,
            ("participantIds", [Scene.Participant.Id]),
            ("goalIds", [.. Scene.Goals.Select(goal => goal.Id)]));
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details))!;
        Assert.Equal(1 + Scene.Goals.Count, details["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task Both_tools_repeat_their_structured_content_as_their_one_text_block()
    {
        var found = await CallAsync(Find, new() { ["search"] = Scene.FamilyName });
        var got = await CallAsync(Get, new() { ["participantId"] = Scene.Participant.Id.ToString() });

        foreach (var (result, _) in new[] { found, got })
        {
            var text = JsonDocument.Parse(Assert.Single(result.Content.OfType<TextContentBlock>()).Text).RootElement;
            Assert.True(JsonElement.DeepEquals(Content(result), text));
        }
    }

    [Fact]
    public async Task The_tools_list_has_output_schemas_and_all_five_hints()
    {
        var tools = (await Client.ListToolsAsync(cancellationToken: Ct)).ToDictionary(tool => tool.Name);

        Assert.Equal("Find participants by name", tools[Find].ProtocolTool.Title);
        Assert.Equal("Get a participant and their goals", tools[Get].ProtocolTool.Title);
        foreach (var name in new[] { Find, Get })
        {
            var annotations = Assert.IsType<ToolAnnotations>(tools[name].ProtocolTool.Annotations);
            Assert.True(annotations.ReadOnlyHint);
            Assert.True(annotations.IdempotentHint);
            Assert.False(annotations.DestructiveHint);
            Assert.False(annotations.OpenWorldHint);
            Assert.NotNull(tools[name].ProtocolTool.OutputSchema);
        }

        var required = tools[Find].ProtocolTool.OutputSchema!.Value.GetProperty("required").EnumerateArray()
            .Select(name => name.GetString());
        Assert.Equal(["participants"], required);
    }

    [Fact]
    public async Task No_result_has_a_date_of_birth_in_any_form()
    {
        var found = await CallAsync(Find, new() { ["search"] = Scene.FamilyName, ["includeArchived"] = true });
        var got = await CallAsync(Get, new() { ["participantId"] = Scene.Participant.Id.ToString() });
        var born = Scene.DateOfBirth;
        string[] forms =
        [
            born.ToString("yyyy-MM-dd"),
            born.ToString("dd/MM/yyyy"),
            born.ToString("d/M/yyyy"),
            born.ToString("MM/dd/yyyy"),
            born.ToString("M/d/yyyy"),
        ];

        foreach (var (result, _) in new[] { found, got })
        {
            Assert.DoesNotContain(
                PropertyNames(Content(result)), name => name.Contains("birth", StringComparison.OrdinalIgnoreCase));
            Assert.All(forms, form => Assert.DoesNotContain(form, Body(result), StringComparison.Ordinal));
        }
    }

    private static List<Guid> ParticipantIds(JsonElement content) =>
    [
        .. content.GetProperty("participants").EnumerateArray()
            .Select(match => match.GetProperty("participantId").GetGuid()),
    ];

    private async Task<(CallToolResult Result, AuditRow Row)> CallAsync(string tool, JsonObject arguments)
    {
        var lastEventId = await LastEventIdAsync(Flow);
        var result = await ToolScene.CallAsync(Client, tool, arguments);
        return (result, Assert.Single(await EventsAfterAsync(Flow, lastEventId)));
    }

    // Participants of one family name whose given names are in the order of the list, so that so are the IDs.
    private async Task<List<Guid>> AddParticipantsAsync(string familyName, int count)
    {
        var ids = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var added = await Flow.App.Services.AddParticipantAsync(
                Scene.Organisation, givenName: $"{(char)('A' + index)}ora", familyName: familyName);
            ids.Add(added.Id);
        }

        return ids;
    }
}
