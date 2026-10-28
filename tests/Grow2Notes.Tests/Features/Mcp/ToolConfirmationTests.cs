using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Protocol;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// The confirmation that stands before a change to a person's access (mcp-server.md §5.3, A136), proved on the
/// stand-in tool <c>probe_confirm</c> before any real tool uses it: which clients are asked by a form and which are
/// given the preview, what each answer to the form means, and the state that comes back with it. The cases share the
/// app of <see cref="ConfirmationWorld"/>; each connects afresh, because the clock moves.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ToolConfirmationTests(ConfirmationWorld world) : IClassFixture<ConfirmationWorld>
{
    private const string Tool = "probe_confirm";
    private const string Message = "Confirm the change?";
    private const string YesTitle = "Yes, change it";
    private const string FormCapabilities = """{"elicitation":{"form":{}}}""";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static TheoryData<string> CapabilitiesThatGetThePreview { get; } = ["{}", """{"elicitation":{"url":{}}}"""];

    // A blank elicitation counts as the form: the SDK reads it so.
    public static TheoryData<string> CapabilitiesThatGetTheForm { get; } =
        [FormCapabilities, """{"elicitation":{}}"""];

    // The last sends no answer at all.
    public static TheoryData<string> AnswersThatAreNotYes { get; } =
    [
        """{"confirm":{"action":"decline"}}""",
        """{"confirm":{"action":"cancel"}}""",
        """{"confirm":{"action":"accept","content":{"confirm":false}}}""",
        """{"confirm":{"action":"accept","content":{"confirm":"true"}}}""",
        """{"confirm":{"action":"accept"}}""",
        "",
    ];

    private AssistantFlow Flow => world.Flow;

    [Fact]
    public async Task A_client_of_the_old_revision_is_given_the_preview_and_confirms_with_the_version()
    {
        var email = NewEmail();
        await using var client = await Flow.McpClientAsync(
            await world.TokenOfAsync(world.ManagerId), ToolWorld.Revision);

        var asked = await client.CallToolAsync(
            Tool, new Dictionary<string, object?> { ["email"] = email }, cancellationToken: Ct);
        var confirmed = await client.CallToolAsync(
            Tool,
            new Dictionary<string, object?> { ["email"] = email, ["confirm"] = true, ["expectedVersion"] = "1" },
            cancellationToken: Ct);

        Assert.Equal(new ConfirmStep("Ask", null), StepOf(asked));
        Assert.Equal(new ConfirmStep("Confirmed", "1"), StepOf(confirmed));
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetThePreview))]
    public async Task A_client_without_form_elicitation_is_given_the_preview_and_never_a_form(string capabilities)
    {
        var token = await world.TokenOfAsync(world.ManagerId);

        var result = await Flow.PostToolCallAsync(token, Tool, Arguments(NewEmail()), Capabilities(capabilities));

        Assert.Null(result["inputRequests"]);
        Assert.Equal(new ConfirmStep("Ask", null), StepOf(result));
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm))]
    public async Task A_client_that_shows_forms_is_asked_by_one_even_when_it_sent_confirm_true(string capabilities)
    {
        var token = await world.TokenOfAsync(world.ManagerId);
        foreach (var confirm in new[] { false, true })
        {
            var email = NewEmail();
            var lastEventId = await McpAuditRows.LastEventIdAsync(Flow);

            var asked = await Flow.PostToolCallAsync(
                token, Tool, Arguments(email, confirm), Capabilities(capabilities));

            Assert.Equal("input_required", asked["resultType"]!.GetValue<string>());
            var request = Assert.Single(asked["inputRequests"]!.AsObject());
            Assert.Equal("confirm", request.Key);
            Assert.Equal("elicitation/create", request.Value!["method"]!.GetValue<string>());
            var form = request.Value["params"]!;
            Assert.Equal("form", form["mode"]!.GetValue<string>());
            Assert.Equal(Message, form["message"]!.GetValue<string>());
            var schema = form["requestedSchema"]!;
            var box = Assert.Single(schema["properties"]!.AsObject());
            Assert.Equal("confirm", box.Key);
            Assert.Equal("boolean", box.Value!["type"]!.GetValue<string>());
            Assert.Equal(YesTitle, box.Value["title"]!.GetValue<string>());
            Assert.Equal(["confirm"], schema["required"]!.AsArray().Select(name => name!.GetValue<string>()));
            Assert.DoesNotContain(
                email, asked["requestState"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

            // The form is the call's one success row, written before the SDK sent it.
            var row = Assert.Single(await McpAuditRows.EventsAfterAsync(Flow, lastEventId));
            Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
            McpAuditRows.AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, []));
        }
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm))]
    public async Task Accepting_the_form_with_yes_confirms_with_the_version_from_the_state(string capabilities)
    {
        var token = await world.TokenOfAsync(world.ManagerId);
        var email = NewEmail();
        var state = await AskAsync(token, email, capabilities);

        var result = await Flow.PostToolCallAsync(
            token, Tool, Arguments(email), Capabilities(capabilities), state, Answer(AcceptedYes));

        Assert.Null(result["isError"]);
        Assert.Equal(new ConfirmStep("Confirmed", "1"), StepOf(result));
    }

    [Theory]
    [MemberData(nameof(AnswersThatAreNotYes))]
    public async Task Any_answer_but_an_accepted_true_declines(string answer)
    {
        var token = await world.TokenOfAsync(world.ManagerId);
        var email = NewEmail();
        var state = await AskAsync(token, email, FormCapabilities);

        var result = await Flow.PostToolCallAsync(
            token,
            Tool,
            Arguments(email),
            Capabilities(FormCapabilities),
            state,
            answer.Length == 0 ? null : Capabilities(answer));

        Assert.Equal(new ConfirmStep("Declined", null), StepOf(result));
    }

    [Theory]
    [InlineData("flipped")]
    [InlineData("garbage")]
    [InlineData("another email")]
    [InlineData("another caller")]
    public async Task A_state_that_is_not_for_this_call_is_refused(string kind)
    {
        var token = await world.TokenOfAsync(world.ManagerId);
        var email = NewEmail();
        var state = await AskAsync(token, email, FormCapabilities);
        var (caller, retried, sent) = kind switch
        {
            "flipped" => (token, email, Flip(state)),
            "garbage" => (token, email, "garbage"),
            "another email" => (token, NewEmail(), state),
            _ => (await world.TokenOfAsync(world.SupportId), email, state),
        };

        var result = await Flow.PostToolCallAsync(
            caller, Tool, Arguments(retried), Capabilities(FormCapabilities), sent, Answer(AcceptedYes));

        AssertStateRefused(result);
    }

    [Fact]
    public async Task A_state_answered_at_exactly_five_minutes_is_live_and_one_second_later_is_not()
    {
        var token = await world.TokenOfAsync(world.ManagerId);
        var email = NewEmail();
        var state = await AskAsync(token, email, FormCapabilities);
        Flow.Clock.Advance(ToolConfirmation.Lifetime);

        var live = await Flow.PostToolCallAsync(
            token, Tool, Arguments(email), Capabilities(FormCapabilities), state, Answer(AcceptedYes));
        Flow.Clock.Advance(TimeSpan.FromSeconds(1));
        var lapsed = await Flow.PostToolCallAsync(
            token, Tool, Arguments(email), Capabilities(FormCapabilities), state, Answer(AcceptedYes));

        Assert.Equal(new ConfirmStep("Confirmed", "1"), StepOf(live));
        AssertStateRefused(lapsed);
    }

    [Fact]
    public async Task A_client_with_an_elicitation_handler_is_asked_once_and_its_accept_confirms()
    {
        var asked = new List<string?>();
        await using var client = await Flow.McpClientAsync(
            await world.TokenOfAsync(world.ManagerId),
            "2026-07-28",
            elicitation: (form, _) =>
            {
                asked.Add(form?.Message);
                return ValueTask.FromResult(new ElicitResult
                {
                    Action = "accept",
                    Content = new Dictionary<string, JsonElement>
                    {
                        ["confirm"] = JsonSerializer.SerializeToElement(true),
                    },
                });
            });

        var result = await client.CallToolAsync(
            Tool, new Dictionary<string, object?> { ["email"] = NewEmail() }, cancellationToken: Ct);

        Assert.Equal([Message], asked);
        Assert.Equal(new ConfirmStep("Confirmed", "1"), StepOf(result));
    }

    [Fact]
    public async Task A_client_with_an_elicitation_handler_that_declines_changes_nothing()
    {
        await using var client = await Flow.McpClientAsync(
            await world.TokenOfAsync(world.ManagerId),
            "2026-07-28",
            elicitation: (_, _) => ValueTask.FromResult(new ElicitResult { Action = "decline" }));

        var result = await client.CallToolAsync(
            Tool, new Dictionary<string, object?> { ["email"] = NewEmail() }, cancellationToken: Ct);

        Assert.Equal(new ConfirmStep("Declined", null), StepOf(result));
    }

    private static string AcceptedYes => """{"confirm":{"action":"accept","content":{"confirm":true}}}""";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.org";

    // A node has one parent, so each call is given a node of its own.
    private static JsonObject Capabilities(string json) => JsonNode.Parse(json)!.AsObject();

    private static JsonObject Answer(string json) => Capabilities(json);

    private static JsonObject Arguments(string email, bool? confirm = null)
    {
        var arguments = new JsonObject { ["email"] = email };
        if (confirm is { } value)
        {
            arguments["confirm"] = value;
        }

        return arguments;
    }

    private static ConfirmStep StepOf(JsonObject result) =>
        JsonSerializer.Deserialize<ConfirmStep>(result["content"]![0]!["text"]!.GetValue<string>(), Json)!;

    private static ConfirmStep StepOf(CallToolResult result) =>
        JsonSerializer.Deserialize<ConfirmStep>(
            Assert.Single(result.Content.OfType<TextContentBlock>()).Text, Json)!;

    private static string Flip(string state)
    {
        var at = state.Length / 2;
        return state[..at] + (state[at] == 'A' ? 'B' : 'A') + state[(at + 1)..];
    }

    private static void AssertStateRefused(JsonObject result)
    {
        Assert.True(result["isError"]!.GetValue<bool>());
        var error = result["structuredContent"]!["error"]!;
        Assert.Equal("precondition.failed", error["code"]!.GetValue<string>());
        Assert.Equal(ToolConfirmation.StateRefused.Message, error["message"]!.GetValue<string>());
    }

    private async Task<string> AskAsync(string token, string email, string capabilities)
    {
        var asked = await Flow.PostToolCallAsync(token, Tool, Arguments(email), Capabilities(capabilities));
        Assert.Equal("input_required", asked["resultType"]!.GetValue<string>());
        return asked["requestState"]!.GetValue<string>();
    }
}

/// <summary>
/// One app with the stand-in tools, a manager and a Support person, for the cases of
/// <see cref="ToolConfirmationTests"/>. Each case takes a token of its own, as the clock moves.
/// </summary>
public sealed class ConfirmationWorld(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private Grow2NotesFactory? factory;

    internal AssistantFlow Flow { get; private set; } = null!;

    internal Guid ManagerId { get; private set; }

    internal Guid SupportId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        factory = new(sqlServer);
        Flow = await AssistantFlow.StartAsync(factory, sqlServer, "ToolConfirmation");
        ManagerId = await Flow.AddUserAsync(UserRole.Manager);
        SupportId = await Flow.AddUserAsync(UserRole.Support);
    }

    public async ValueTask DisposeAsync()
    {
        if (Flow is not null)
        {
            await Flow.DisposeAsync();
        }

        factory?.Dispose();
    }

    /// <summary>The access token of an assistant that <paramref name="userId"/> has just connected.</summary>
    internal async Task<string> TokenOfAsync(Guid userId)
    {
        using var browser = await Flow.SignInAsync(userId);
        return (await Flow.ConnectAsync(browser)).AccessToken;
    }
}
