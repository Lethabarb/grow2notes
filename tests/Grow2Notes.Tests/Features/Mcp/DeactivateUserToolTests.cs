using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform.Audit;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.ConfirmationCalls;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>deactivate_user</c> (mcp-server.md §5.2, §5.3, A136, A137) through the real <c>/mcp</c>: the preview that a
/// client without forms is given, the form that a client with forms is asked by and every answer to it, Support's
/// limits, the order the checks run in, and the one audit row each call writes. The guards that every tool shares are
/// in <see cref="ToolMatrixTests"/>, and the confirmation itself is proved on its stand-in in
/// <see cref="ToolConfirmationTests"/>. The cases share the app of <see cref="ToolWorld"/>; each that changes a
/// person adds a fresh account to change, never one of the scene's.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class DeactivateUserToolTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Tool = "deactivate_user";
    private const string Reason = "Sam asked by phone";

    private const string AccountChanged =
        "This account changed after the person was asked. Nothing was changed. " +
        "Call the tool again to ask them afresh.";

    private const string NoSuchUser =
        "No user has that email in this organisation. Nothing was changed. Find them with list_users.";

    private const string LastManager =
        "This is the organisation's only active manager, so they cannot be deactivated. Nothing was changed.";

    private const string SupportCannot =
        "Support can act on worker accounts only. A manager can do this in Grow2Notes. Nothing was changed.";

    private const string SupportMustSayWhy =
        "Support must send a reason: why, and who asked. Nothing was changed.";

    public static TheoryData<string> StatesThatAreNotForTheCall { get; } =
        ["flipped", "garbage", "the manager's, retried by Support", "another email", "another reason"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task The_preview_names_the_account_and_its_version_and_changes_nothing()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Preview(user, "worker", "active"), Content(result));
        await AssertUnchangedAsync(user);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Confirming_with_no_version_is_refused_for_it(string? version)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: version));

        Assert.Equal("precondition.required", ToolError.CodeOf(result));
        AssertRefused(row, Tool, "precondition.required");
        await AssertUnchangedAsync(user);
    }

    [Fact]
    public async Task Confirming_with_a_stale_version_is_refused_as_a_changed_account()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: StaleVersion));

        AssertError(result, "precondition.failed", AccountChanged);
        AssertRefused(row, Tool, "precondition.failed");
        await AssertUnchangedAsync(user);
    }

    [Fact]
    public async Task Confirming_with_the_current_version_deactivates_without_an_earlier_preview()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: user.Version));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Done(user, "worker", changed: true), Content(result));
        AssertChanged(row, AuditEventTypes.User.Deactivated, Tool, Scene.UserOf(ToolCaller.Manager));
        Assert.Null(JsonNode.Parse(row.Details!)!["reason"]);
        Assert.Equal(UserStatus.Deactivated, (await Flow.UserAsync(user.Id)).Status);
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetThePreview), MemberType = typeof(ConfirmationCalls))]
    public async Task A_2026_client_without_form_elicitation_is_given_the_preview(string capabilities)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await RawAsync(await world.TokenOf(ToolCaller.Manager), Args(user.Email), capabilities);

        Assert.Null(result["inputRequests"]);
        AssertJson(Preview(user, "worker", "active"), result["structuredContent"]);
        await AssertUnchangedAsync(user);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Fact]
    public async Task A_2025_client_with_a_form_handler_is_given_the_preview_and_never_asked()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var asked = 0;
        await using var client = await Flow.McpClientAsync(
            await world.TokenOf(ToolCaller.Manager),
            ToolWorld.Revision,
            elicitation: (_, _) =>
            {
                asked++;
                return ValueTask.FromResult(new ElicitResult { Action = "accept" });
            });

        var result = await ToolScene.CallAsync(client, Tool, Args(user.Email));

        Assert.Equal(0, asked);
        AssertJson(Preview(user, "worker", "active"), Content(result));
        await AssertUnchangedAsync(user);
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task A_client_that_shows_forms_is_asked_by_one_even_when_it_sent_confirm_true(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        foreach (var confirmed in new[] { false, true })
        {
            var user = await Scene.AddUserAsync(UserRole.Worker);
            var arguments = confirmed ? Args(user.Email, confirm: true, version: user.Version) : Args(user.Email);

            var (result, row) = await RawAsync(token, arguments, capabilities);

            Assert.Equal("input_required", result["resultType"]!.GetValue<string>());
            var request = Assert.Single(result["inputRequests"]!.AsObject());
            Assert.Equal("confirm", request.Key);
            Assert.Equal("elicitation/create", request.Value!["method"]!.GetValue<string>());
            var form = request.Value["params"]!;
            Assert.Equal("form", form["mode"]!.GetValue<string>());
            Assert.Equal(FormMessage(user, "worker"), form["message"]!.GetValue<string>());
            var schema = form["requestedSchema"]!;
            var box = Assert.Single(schema["properties"]!.AsObject());
            Assert.Equal("confirm", box.Key);
            Assert.Equal("boolean", box.Value!["type"]!.GetValue<string>());
            Assert.Equal($"Yes, deactivate {user.DisplayName}", box.Value["title"]!.GetValue<string>());
            Assert.Equal(["confirm"], schema["required"]!.AsArray().Select(name => name!.GetValue<string>()));
            Assert.DoesNotContain(
                user.Email, result["requestState"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            await AssertUnchangedAsync(user);
            AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
        }
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task Accepting_the_form_deactivates_with_the_version_from_the_state(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(token, user, capabilities);

        var (result, row) = await RawAsync(token, Args(user.Email), capabilities, state, AcceptedYes);

        Assert.Null(result["isError"]);
        AssertJson(Done(user, "worker", changed: true), result["structuredContent"]);
        AssertChanged(row, AuditEventTypes.User.Deactivated, Tool, Scene.UserOf(ToolCaller.Manager));
        Assert.Equal(UserStatus.Deactivated, (await Flow.UserAsync(user.Id)).Status);
    }

    [Theory]
    [MemberData(nameof(AnswersThatAreNotYes), MemberType = typeof(ConfirmationCalls))]
    public async Task Any_answer_but_an_accepted_true_declines_and_changes_nothing(string answer)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(token, user, FormCapabilities);

        var (result, row) = await RawAsync(
            token, Args(user.Email), FormCapabilities, state, answer.Length == 0 ? null : answer);

        AssertJson(Declined(), result["structuredContent"]);
        await AssertUnchangedAsync(user);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Theory]
    [MemberData(nameof(StatesThatAreNotForTheCall))]
    public async Task A_state_that_is_not_for_the_call_is_refused_and_changes_nothing(string kind)
    {
        var managerToken = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var other = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(managerToken, user, FormCapabilities, Reason);
        var (token, arguments, sent) = kind switch
        {
            "flipped" => (managerToken, Args(user.Email, Reason), Flip(state)),
            "garbage" => (managerToken, Args(user.Email, Reason), "garbage"),
            "another email" => (managerToken, Args(other.Email, Reason), state),
            "another reason" => (managerToken, Args(user.Email, "Asked by someone else"), state),
            _ => (await world.TokenOf(ToolCaller.Support), Args(user.Email, Reason), state),
        };

        var (result, row) = await RawAsync(token, arguments, FormCapabilities, sent, AcceptedYes);

        AssertRawError(result, "precondition.failed", StateRefused);
        AssertRefused(row, Tool, "precondition.failed");
        await AssertUnchangedAsync(user);
        await AssertUnchangedAsync(other);
    }

    [Fact]
    public async Task A_row_that_changed_between_the_form_and_the_answer_is_refused_as_a_changed_account()
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(token, user, FormCapabilities);
        await Flow.ResetSignInAsync(user.Id);

        var (result, row) = await RawAsync(token, Args(user.Email), FormCapabilities, state, AcceptedYes);

        AssertRawError(result, "precondition.failed", AccountChanged);
        AssertRefused(row, Tool, "precondition.failed");
        Assert.Equal(UserStatus.Active, (await Flow.UserAsync(user.Id)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_account_deactivated_between_the_form_and_the_answer_answers_unchanged(bool stateTampered)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(token, user, FormCapabilities);
        await Flow.DeactivateAsync(user.Id);

        var (result, row) = await RawAsync(
            token, Args(user.Email), FormCapabilities, stateTampered ? "garbage" : state, AcceptedYes);

        AssertJson(Done(user, "worker", changed: false), result["structuredContent"]);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Fact]
    public async Task A_reason_that_is_absent_and_then_null_is_the_same_reason()
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(token, user, FormCapabilities);
        var retried = Args(user.Email);
        retried["reason"] = null;

        var (result, row) = await RawAsync(token, retried, FormCapabilities, state, AcceptedYes);

        AssertJson(Done(user, "worker", changed: true), result["structuredContent"]);
        Assert.Equal(AuditEventTypes.User.Deactivated, row.EventType);
    }

    [Fact]
    public async Task An_sdk_client_with_a_form_handler_is_asked_once_and_its_accept_deactivates()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var asked = new List<string?>();
        await using var client = await Flow.McpClientAsync(
            await world.TokenOf(ToolCaller.Manager),
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

        var result = await ToolScene.CallAsync(client, Tool, Args(user.Email));

        Assert.Equal([FormMessage(user, "worker")], asked);
        AssertJson(Done(user, "worker", changed: true), Content(result));
        Assert.Equal(UserStatus.Deactivated, (await Flow.UserAsync(user.Id)).Status);
    }

    [Fact]
    public async Task An_sdk_client_with_a_form_handler_that_declines_changes_nothing()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);
        await using var client = await Flow.McpClientAsync(
            await world.TokenOf(ToolCaller.Manager),
            "2026-07-28",
            elicitation: (_, _) => ValueTask.FromResult(new ElicitResult { Action = "decline" }));

        var result = await ToolScene.CallAsync(client, Tool, Args(user.Email));

        AssertJson(Declined(), Content(result));
        await AssertUnchangedAsync(user);
    }

    [Theory]
    [InlineData(ToolCaller.Manager)]
    [InlineData(ToolCaller.Support)]
    public async Task The_reason_is_trimmed_and_kept_in_the_audit_event(ToolCaller caller)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(
            Tool, Args(user.Email, "  Sam asked by phone  ", confirm: true, version: user.Version), caller);

        Assert.NotEqual(true, result.IsError);
        AssertChanged(row, AuditEventTypes.User.Deactivated, Tool, Scene.UserOf(caller));
        Assert.Equal("Sam asked by phone", JsonNode.Parse(row.Details!)!["reason"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("   ", false)]
    [InlineData("   ", true)]
    public async Task Support_must_send_a_reason_at_the_preview_and_with_the_confirmation(
        string? reason, bool confirm)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(
            Tool, Args(user.Email, reason, confirm, confirm ? user.Version : null), ToolCaller.Support);

        AssertError(result, "reason.required", SupportMustSayWhy);
        AssertRefused(row, Tool, "reason.required");
        await AssertUnchangedAsync(user);
    }

    [Theory]
    [InlineData("Manager", false)]
    [InlineData("Manager", true)]
    [InlineData("Support", false)]
    [InlineData("Support", true)]
    public async Task Support_cannot_act_on_a_manager_or_a_support_account_even_with_a_reason(
        string roleName, bool confirm)
    {
        var user = await Scene.AddUserAsync(Enum.Parse<UserRole>(roleName));

        var (result, row) = await world.CallAsync(
            Tool, Args(user.Email, Reason, confirm, confirm ? user.Version : null), ToolCaller.Support);

        AssertError(result, "role.not_allowed", SupportCannot);
        AssertRefused(row, Tool, "role.not_allowed");
        await AssertUnchangedAsync(user);
    }

    [Fact]
    public async Task Support_is_refused_for_the_role_before_the_reason()
    {
        var manager = await Scene.AddUserAsync(UserRole.Manager);

        var (result, row) = await world.CallAsync(Tool, Args(manager.Email), ToolCaller.Support);

        AssertError(result, "role.not_allowed", SupportCannot);
        AssertRefused(row, Tool, "role.not_allowed");
    }

    [Fact]
    public async Task Support_is_refused_for_the_role_of_an_account_that_is_already_deactivated()
    {
        var manager = await Scene.AddUserAsync(UserRole.Manager, UserStatus.Deactivated);

        var (result, row) = await world.CallAsync(Tool, Args(manager.Email, Reason), ToolCaller.Support);

        AssertError(result, "role.not_allowed", SupportCannot);
        AssertRefused(row, Tool, "role.not_allowed");
    }

    [Fact]
    public async Task Support_is_refused_for_the_reason_before_an_account_that_is_already_deactivated_answers()
    {
        var worker = await Scene.AddUserAsync(UserRole.Worker, UserStatus.Deactivated);

        var (result, row) = await world.CallAsync(Tool, Args(worker.Email), ToolCaller.Support);

        AssertError(result, "reason.required", SupportMustSayWhy);
        AssertRefused(row, Tool, "reason.required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(StaleVersion)]
    public async Task An_account_already_deactivated_answers_unchanged_before_the_version_is_looked_at(
        string? version)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker, UserStatus.Deactivated);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: version));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Done(user, "worker", changed: false), Content(result));
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Fact]
    public async Task An_account_already_deactivated_answers_unchanged_to_a_call_with_no_confirmation()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker, UserStatus.Deactivated);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email));

        AssertJson(Done(user, "worker", changed: false), Content(result));
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Fact]
    public async Task An_account_already_deactivated_is_never_asked_about_by_a_form()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker, UserStatus.Deactivated);

        var (result, row) = await RawAsync(await world.TokenOf(ToolCaller.Manager), Args(user.Email), FormCapabilities);

        Assert.Null(result["inputRequests"]);
        AssertJson(Done(user, "worker", changed: false), result["structuredContent"]);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Fact]
    public async Task The_only_active_manager_is_previewed_and_refused_only_when_confirmed()
    {
        const ToolCaller caller = ToolCaller.ManagerOfAnotherOrganisation;
        var self = await Flow.UserAsync(Scene.OtherOrganisation.ManagerId);
        var email = self.Email!;
        var version = self.ConcurrencyStamp!;

        var (preview, _) = await world.CallAsync(Tool, Args(email), caller);
        var (noVersion, noVersionRow) = await world.CallAsync(Tool, Args(email, confirm: true), caller);
        var (stale, staleRow) = await world.CallAsync(
            Tool, Args(email, confirm: true, version: StaleVersion), caller);
        var token = await world.TokenOf(caller);
        var state = await AskAsync(token, email, FormCapabilities);
        var (accepted, acceptedRow) = await RawAsync(token, Args(email), FormCapabilities, state, AcceptedYes);

        Assert.Equal("preview", Content(preview).GetProperty("status").GetString());
        Assert.Equal(version, Content(preview).GetProperty("expectedVersion").GetString());
        AssertError(noVersion, "user.last_manager", LastManager);
        AssertRefused(noVersionRow, Tool, "user.last_manager");
        AssertError(stale, "user.last_manager", LastManager);
        AssertRefused(staleRow, Tool, "user.last_manager");
        AssertRawError(accepted, "user.last_manager", LastManager);
        AssertRefused(acceptedRow, Tool, "user.last_manager");
        var after = await Flow.UserAsync(Scene.OtherOrganisation.ManagerId);
        Assert.Equal(UserStatus.Active, after.Status);
        Assert.Equal(version, after.ConcurrencyStamp);
    }

    [Fact]
    public async Task An_unknown_email_and_another_organisations_email_are_the_same_not_found()
    {
        var elsewhere = (await Flow.UserAsync(Scene.OtherOrganisation.ManagerId)).Email!;
        var unknown = $"nobody.{Guid.NewGuid():N}@example.test";

        foreach (var email in new[] { unknown, elsewhere })
        {
            var (result, row) = await world.CallAsync(Tool, Args(email, confirm: true, version: StaleVersion));

            AssertError(result, "not_found", NoSuchUser);
            Assert.DoesNotContain(email, Body(result), StringComparison.OrdinalIgnoreCase);
            AssertRefused(row, Tool, "not_found");
        }

        Assert.Equal(
            UserStatus.Active, (await Flow.UserAsync(Scene.OtherOrganisation.ManagerId)).Status);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Support")]
    public async Task A_manager_can_deactivate_a_manager_or_a_support_account(string roleName)
    {
        var user = await Scene.AddUserAsync(Enum.Parse<UserRole>(roleName));

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: user.Version));

        AssertJson(Done(user, roleName.ToLowerInvariant(), changed: true), Content(result));
        AssertChanged(row, AuditEventTypes.User.Deactivated, Tool, Scene.UserOf(ToolCaller.Manager));
        Assert.Equal(UserStatus.Deactivated, (await Flow.UserAsync(user.Id)).Status);
    }

    [Fact]
    public async Task A_worker_who_became_a_manager_after_the_preview_is_refused_to_Support()
    {
        var worker = await Scene.AddUserAsync(UserRole.Worker);
        var (preview, _) = await world.CallAsync(Tool, Args(worker.Email, Reason), ToolCaller.Support);
        var version = Content(preview).GetProperty("expectedVersion").GetString()!;
        await Flow.ChangeRoleAsync(worker.Id, UserRole.Manager);

        var (result, row) = await world.CallAsync(
            Tool, Args(worker.Email, Reason, confirm: true, version: version), ToolCaller.Support);

        AssertError(result, "role.not_allowed", SupportCannot);
        AssertRefused(row, Tool, "role.not_allowed");
        var after = await Flow.UserAsync(worker.Id);
        Assert.Equal(UserStatus.Active, after.Status);
        Assert.Equal(UserRole.Manager, after.Role);
    }

    private static JsonObject Args(
        string email, string? reason = null, bool confirm = false, string? version = null)
    {
        var arguments = new JsonObject { ["email"] = email };
        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        if (confirm)
        {
            arguments["confirm"] = true;
        }

        if (version is not null)
        {
            arguments["expectedVersion"] = version;
        }

        return arguments;
    }

    private static string Effect(SceneUser user) =>
        $"{user.DisplayName} will be signed out everywhere now and can't sign in. Their notes stay.";

    private static string FormMessage(SceneUser user, string role) =>
        $"Deactivate {user.DisplayName} ({user.Email}, {role})? {Effect(user)}";

    private static JsonObject Preview(SceneUser user, string role, string status) => new()
    {
        ["status"] = "preview",
        ["action"] = Tool,
        ["user"] = UserJson(user, role, status),
        ["effect"] = Effect(user),
        ["expectedVersion"] = user.Version,
        ["toConfirm"] = "Show this to the person. If they say yes, call deactivate_user again with confirm: true "
            + "and this expectedVersion.",
    };

    private static JsonObject Done(SceneUser user, string role, bool changed) => new()
    {
        ["status"] = "done",
        ["action"] = Tool,
        ["user"] = UserJson(user, role, "deactivated"),
        ["changed"] = changed,
    };

    private static JsonObject Declined() => new()
    {
        ["status"] = "declined",
        ["action"] = Tool,
        ["message"] = "Nothing was changed.",
    };

    private static JsonObject UserJson(SceneUser user, string role, string status) => new()
    {
        ["email"] = user.Email,
        ["displayName"] = user.DisplayName,
        ["role"] = role,
        ["status"] = status,
    };

    // The user is as they were added: nothing was changed, not even the version.
    private async Task AssertUnchangedAsync(SceneUser user)
    {
        var row = await Flow.UserAsync(user.Id);
        Assert.Equal(UserStatus.Active, row.Status);
        Assert.Equal(user.Version, row.ConcurrencyStamp);
        Assert.Null(row.LockoutEnd);
    }

    private Task<(JsonObject Result, AuditRow Row)> RawAsync(
        string token, JsonObject arguments, string capabilities, string? state = null, string? answer = null) =>
        ConfirmationCalls.RawAsync(Flow, Tool, token, arguments, capabilities, state, answer);

    // The first call of a form: the state that comes back with it.
    private Task<string> AskAsync(string token, SceneUser user, string capabilities, string? reason = null) =>
        AskAsync(token, user.Email, capabilities, reason);

    private Task<string> AskAsync(string token, string email, string capabilities, string? reason = null) =>
        ConfirmationCalls.AskAsync(Flow, Tool, token, Args(email, reason), capabilities);
}
