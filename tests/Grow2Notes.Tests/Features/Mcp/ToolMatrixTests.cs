using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// What every tool the app lists must do (mcp-server.md §10 item 1, S07.02.04), held to its row in
/// <see cref="ToolMatrix"/>: each caller gets the outcome the row says, each error case its code with one
/// <c>mcp.refused</c> row, no text input lets in a control or bidirectional character, and no token that is no longer
/// good gets past the endpoint. A tool with no row fails
/// <see cref="Every_listed_tool_has_a_row_and_every_row_is_listed"/>. Every later tool adds a row, and so cases, so the
/// cases share the app of <see cref="ToolWorld"/> unless they change what the whole app shares: a token case that
/// moves the clock or removes the registration starts an app of its own, and the others use a person of their own.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ToolMatrixTests(Grow2NotesFactory factory, SqlServerFixture sqlServer, ToolWorld world)
    : IClassFixture<Grow2NotesFactory>, IClassFixture<ToolWorld>
{

    // Every way for a token to stop being good that the endpoint must answer with 401, and the absence of one.
    private static readonly string[] TokenCases =
    [
        "deactivated",
        "sign-in reset",
        "made a worker",
        "expired",
        "wrong audience",
        "removed registration",
        "no token",
    ];

    public static TheoryData<string> Tools { get; } = [.. ToolMatrix.Rows.Select(row => row.Name)];

    public static MatrixTheoryData<string, ToolCaller> ToolsAndCallers { get; } =
        new(ToolMatrix.Rows.Select(row => row.Name), Enum.GetValues<ToolCaller>());

    public static TheoryData<string, string> ErrorCases { get; } = ErrorCasesOfEveryTool();

    public static MatrixTheoryData<string, string> ToolsAndTokenCases { get; } =
        new(ToolMatrix.Rows.Select(row => row.Name), TokenCases);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task Every_listed_tool_has_a_row_and_every_row_is_listed()
    {
        var client = world.ClientOf(ToolCaller.Manager);

        var listed = (await client.ListToolsAsync(cancellationToken: Ct)).Select(tool => tool.Name);

        Assert.Equal(
            ToolMatrix.Rows.Select(row => row.Name).Order(StringComparer.Ordinal),
            listed.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ToolsAndCallers))]
    public async Task Each_tool_answers_each_caller_as_its_row_says(string tool, ToolCaller caller)
    {
        var row = RowOf(tool);
        var lastEventId = await LastEventIdAsync(Flow);

        var result = await ToolScene.CallAsync(world.ClientOf(caller), tool, row.ValidArguments(Scene));

        var audit = Assert.Single(await EventsAfterAsync(Flow, lastEventId));
        if (caller == ToolCaller.ManagerOfAnotherOrganisation && row.OtherOrganisationCode is { } code)
        {
            Assert.True(result.IsError);
            Assert.Equal(code, ToolError.CodeOf(result));
            AssertRefused(audit, tool, code);
        }
        else
        {
            Assert.NotEqual(true, result.IsError);
            AssertAuditHoldsTheIdsReturned(audit, tool, IdsOf(Assert.NotNull(result.StructuredContent)));
        }

        if (caller == ToolCaller.ManagerOfAnotherOrganisation)
        {
            // Not an ID, a name or a goal of the first organisation, in either form of the result.
            foreach (var secret in new[] { Scene.Participant.Id.ToString(), Scene.Token })
            {
                Assert.DoesNotContain(secret, JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions));
            }
        }
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task Each_error_case_answers_its_code(string tool, string caseName)
    {
        var errorCase = RowOf(tool).ErrorCases.Single(candidate => candidate.Name == caseName);
        var lastEventId = await LastEventIdAsync(Flow);

        var result = await ToolScene.CallAsync(
            world.ClientOf(ToolCaller.Manager), tool, errorCase.Arguments(Scene));

        Assert.True(result.IsError);
        Assert.Equal(errorCase.Code, ToolError.CodeOf(result));
        AssertRefused(Assert.Single(await EventsAfterAsync(Flow, lastEventId)), tool, errorCase.Code);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Every_string_input_refuses_a_control_or_bidi_character(string tool)
    {
        var row = RowOf(tool);
        var client = world.ClientOf(ToolCaller.Manager);
        var listed = Assert.Single(await client.ListToolsAsync(cancellationToken: Ct), found => found.Name == tool);
        var strings = listed.JsonSchema.GetProperty("properties").EnumerateObject()
            .Where(property => property.Value.GetProperty("type") is { ValueKind: JsonValueKind.String } type
                && type.GetString() == "string")
            .Select(property => property.Name)
            .ToList();
        Assert.NotEmpty(strings);

        foreach (var name in strings)
        {
            var arguments = row.ValidArguments(Scene);
            var valid = arguments[name]!.GetValue<string>();
            arguments[name] = valid[..1] + "\u202E" + valid[1..];

            var result = await ToolScene.CallAsync(client, tool, arguments);

            Assert.True(result.IsError, name);
            Assert.DoesNotContain(valid[1..], JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions));
        }
    }

    [Theory]
    [MemberData(nameof(ToolsAndTokenCases))]
    public async Task Every_tool_refuses_each_token_case_with_401(string tool, string tokenCase)
    {
        // The clock and the registration are the whole app's, so these two cases have one to themselves.
        await using var appOfItsOwn = tokenCase is "expired" or "removed registration" ? await StartAsync() : null;
        var flow = appOfItsOwn ?? Flow;
        var userId = await flow.AddUserAsync(UserRole.Manager);
        if (tokenCase == "wrong audience")
        {
            flow.TokenEdits.Queue(TokenEdit.AnotherAudience);
        }

        string? token = null;
        if (tokenCase != "no token")
        {
            using var browser = await flow.SignInAsync(userId);
            token = (await flow.ConnectAsync(browser)).AccessToken;
        }

        if (tokenCase == "expired")
        {
            flow.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        }

        await (tokenCase switch
        {
            "deactivated" => flow.DeactivateAsync(userId),
            "sign-in reset" => flow.ResetSignInAsync(userId),
            "made a worker" => flow.ChangeRoleAsync(userId, UserRole.Worker),
            "removed registration" => RemoveRegistrationAsync(flow),
            _ => Task.CompletedTask,
        });
        var body = AssistantFlow.ToolCall(tool, RowOf(tool).ValidArguments(Scene));

        using var response = await flow.PostMcpAsync(token, body: body);

        await AssistantFlow.AssertChallengeAsync(response, token is null ? null : Errors.InvalidToken);
    }

    private static TheoryData<string, string> ErrorCasesOfEveryTool()
    {
        var cases = new TheoryData<string, string>();
        foreach (var row in ToolMatrix.Rows)
        {
            row.ErrorCases.ToList().ForEach(errorCase => cases.Add(row.Name, errorCase.Name));
        }

        return cases;
    }

    private static ToolRow RowOf(string tool) => ToolMatrix.Rows.Single(row => row.Name == tool);

    private static async Task RemoveRegistrationAsync(AssistantFlow flow)
    {
        var removal = await flow.App.RunAdminCommandAsync(["mcp-client", "remove", AssistantFlow.ClientId]);
        Assert.Equal(0, removal.ExitCode);
    }

    // The IDs a result holds, under the key that the audit row records them under: a participantId is a participantIds.
    // Each task that adds a kind of ID to a result adds it here.
    private static Dictionary<string, List<Guid>> IdsOf(JsonElement result)
    {
        var ids = new Dictionary<string, List<Guid>>();
        Collect(result);
        return ids;

        void Collect(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var key = property.Name switch
                        {
                            "participantId" => "participantIds",
                            "goalId" => "goalIds",
                            _ => null,
                        };
                        if (key is not null && property.Value.ValueKind == JsonValueKind.String)
                        {
                            ids.TryAdd(key, []);
                            ids[key].Add(property.Value.GetGuid());
                        }
                        else
                        {
                            Collect(property.Value);
                        }
                    }

                    break;
                case JsonValueKind.Array:
                    element.EnumerateArray().ToList().ForEach(Collect);
                    break;
            }
        }
    }

    // The row holds exactly the keys that it records and the IDs that the result holds, whatever the tool: a key the
    // result has IDs for must be there, and one it has none for (a look-up that found nothing) is empty.
    private static void AssertAuditHoldsTheIdsReturned(
        AuditRow audit, string tool, Dictionary<string, List<Guid>> returned)
    {
        Assert.Equal(AuditEventTypes.Mcp.Read, audit.EventType);
        var details = JsonNode.Parse(Assert.IsType<string>(audit.Details))!.AsObject();
        string[] callKeys = ["count", "via", "client", "tool"];
        var recorded = details.Select(entry => entry.Key).Except(callKeys).ToList();
        Assert.Empty(returned.Keys.Except(recorded));

        AssertRead(
            audit,
            AssistantFlow.ClientId,
            tool,
            [.. recorded.Select(key => (key, returned.TryGetValue(key, out var ids) ? ids.ToArray() : []))]);
        var participants = returned.GetValueOrDefault("participantIds") ?? [];
        Assert.Equal(participants.Count == 1 ? participants[0] : null, audit.ParticipantId);
    }

    private Task<AssistantFlow> StartAsync() =>
        AssistantFlow.StartAsync(factory, sqlServer, "ToolMatrix", probes: false);
}
