using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Email;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.ConfirmationCalls;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>reset_user_sign_in</c> (mcp-server.md §5.2, §5.3, A136 to A138) through the real <c>/mcp</c>: the preview that a
/// client without forms is given, the form that a client with forms is asked by and every answer to it, Support's
/// limits, the order the checks run in, the refusals that only a confirmed call meets, and the one audit row each call
/// writes. The guards that every tool shares are in <see cref="ToolMatrixTests"/>. The cases share the app of
/// <see cref="ToolWorld"/>; each that changes a person adds a fresh account to change, never one of the scene's.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ResetUserSignInToolTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Tool = "reset_user_sign_in";
    private const string Reason = "Sam asked by phone";

    private const string AccountChanged =
        "This account changed after the person was asked. Nothing was changed. " +
        "Call the tool again to ask them afresh.";

    private const string LastManager =
        "This is the organisation's only active manager, so their sign-in cannot be reset. Nothing was changed.";

    private const string NotActive =
        "This account is deactivated. Reactivate it in Grow2Notes first. Nothing was changed.";

    private const string SupportCannot =
        "Support can act on worker accounts only. A manager can do this in Grow2Notes. Nothing was changed.";

    private const string SupportMustSayWhy =
        "Support must send a reason: why, and who asked. Nothing was changed.";

    public static TheoryData<string> StatesThatAreNotForTheCall { get; } =
        ["flipped", "garbage", "the manager's, retried by Support", "another email", "another reason"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_preview_has_the_wording_for_the_account_and_its_version_and_changes_nothing(bool invited)
    {
        var status = invited ? UserStatus.Invited : UserStatus.Active;
        var user = await Scene.AddUserAsync(UserRole.Worker, status);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Preview(user, status), Content(result));
        await AssertUnchangedAsync(user, status);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Fact]
    public async Task Confirming_resets_an_active_account_as_the_app_does()
    {
        var added = await Scene.AddUserAsync(UserRole.Manager);
        using var browser = await Flow.SignInAsync(added.Id);
        await Flow.ConnectAsync(browser);
        Assert.Equal([Statuses.Valid], await Flow.AuthorisationStatusesAsync(added.Id));

        // Signing in and connecting may have moved the version on.
        var before = await Flow.UserAsync(added.Id);
        var user = added with { Version = before.ConcurrencyStamp! };

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: user.Version));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Done(user, "manager"), Content(result));
        var after = await Flow.UserAsync(user.Id);
        Assert.Equal(UserStatus.Invited, after.Status);
        Assert.Null(after.PasswordHash);
        Assert.False(after.TwoFactorEnabled);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal([Statuses.Revoked], await Flow.AuthorisationStatusesAsync(user.Id));
        var message = Assert.Single(SentTo(user.Email));
        Assert.Equal(user.Email, SetupLinks.FromEmail(message).Email);
        AssertChanged(row, AuditEventTypes.User.SetupLinkSent, Tool, Scene.UserOf(ToolCaller.Manager));
        var details = JsonNode.Parse(row.Details!)!.AsObject();
        Assert.Equal(["client", "tool", "via"], details.Select(detail => detail.Key).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(user.Email, row.Details!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Confirming_resends_an_invited_account_a_new_link()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker, UserStatus.Invited);
        var before = await Flow.UserAsync(user.Id);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: user.Version));

        AssertJson(Done(user, "worker"), Content(result));
        var after = await Flow.UserAsync(user.Id);
        Assert.Equal(UserStatus.Invited, after.Status);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.Single(SentTo(user.Email));
        AssertChanged(row, AuditEventTypes.User.SetupLinkSent, Tool, Scene.UserOf(ToolCaller.Manager));
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
        await AssertUnchangedAsync(user, UserStatus.Active);
    }

    [Fact]
    public async Task Confirming_with_a_stale_version_is_refused_as_a_changed_account()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: StaleVersion));

        AssertError(result, "precondition.failed", AccountChanged);
        AssertRefused(row, Tool, "precondition.failed");
        await AssertUnchangedAsync(user, UserStatus.Active);
    }

    [Fact]
    public async Task A_deactivated_account_is_previewed_and_refused_only_when_confirmed()
    {
        var user = await Scene.AddUserAsync(UserRole.Worker, UserStatus.Deactivated);
        var token = await world.TokenOf(ToolCaller.Manager);

        var (preview, previewRow) = await world.CallAsync(Tool, Args(user.Email));
        var (withVersion, withVersionRow) = await world.CallAsync(
            Tool, Args(user.Email, confirm: true, version: user.Version));
        var (withoutVersion, withoutVersionRow) = await world.CallAsync(Tool, Args(user.Email, confirm: true));
        var state = await AskAsync(Flow, Tool, token, Args(user.Email), FormCapabilities);
        var (accepted, acceptedRow) = await RawAsync(token, Args(user.Email), FormCapabilities, state, AcceptedYes);

        AssertJson(Preview(user, UserStatus.Deactivated), Content(preview));
        AssertRead(previewRow, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
        AssertError(withVersion, "user.not_active", NotActive);
        AssertRefused(withVersionRow, Tool, "user.not_active");
        AssertError(withoutVersion, "user.not_active", NotActive);
        AssertRefused(withoutVersionRow, Tool, "user.not_active");
        AssertRawError(accepted, "user.not_active", NotActive);
        AssertRefused(acceptedRow, Tool, "user.not_active");
        await AssertUnchangedAsync(user, UserStatus.Deactivated);
    }

    [Fact]
    public async Task The_only_active_manager_is_previewed_and_refused_only_when_confirmed()
    {
        const ToolCaller caller = ToolCaller.ManagerOfAnotherOrganisation;
        var token = await world.TokenOf(caller);
        var self = await Flow.UserAsync(Scene.OtherOrganisation.ManagerId);
        var email = self.Email!;
        var version = self.ConcurrencyStamp!;

        var (preview, _) = await world.CallAsync(Tool, Args(email), caller);
        var (current, currentRow) = await world.CallAsync(Tool, Args(email, confirm: true, version: version), caller);
        var (noVersion, noVersionRow) = await world.CallAsync(Tool, Args(email, confirm: true), caller);
        var (stale, staleRow) = await world.CallAsync(
            Tool, Args(email, confirm: true, version: StaleVersion), caller);
        var state = await AskAsync(Flow, Tool, token, Args(email), FormCapabilities);
        var (accepted, acceptedRow) = await RawAsync(token, Args(email), FormCapabilities, state, AcceptedYes);

        Assert.Equal("preview", Content(preview).GetProperty("status").GetString());
        Assert.Equal(version, Content(preview).GetProperty("expectedVersion").GetString());
        AssertError(current, "user.last_manager", LastManager);
        AssertRefused(currentRow, Tool, "user.last_manager");
        AssertError(noVersion, "user.last_manager", LastManager);
        AssertRefused(noVersionRow, Tool, "user.last_manager");
        AssertError(stale, "user.last_manager", LastManager);
        AssertRefused(staleRow, Tool, "user.last_manager");
        AssertRawError(accepted, "user.last_manager", LastManager);
        AssertRefused(acceptedRow, Tool, "user.last_manager");
        var after = await Flow.UserAsync(Scene.OtherOrganisation.ManagerId);
        Assert.Equal(UserStatus.Active, after.Status);
        Assert.Equal(version, after.ConcurrencyStamp);
        Assert.Equal(self.SecurityStamp, after.SecurityStamp);
        Assert.Empty(SentTo(email));
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetThePreview), MemberType = typeof(ConfirmationCalls))]
    public async Task A_2026_client_without_form_elicitation_is_given_the_preview(string capabilities)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await RawAsync(await world.TokenOf(ToolCaller.Manager), Args(user.Email), capabilities);

        Assert.Null(result["inputRequests"]);
        AssertJson(Preview(user, UserStatus.Active), result["structuredContent"]);
        await AssertUnchangedAsync(user, UserStatus.Active);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task A_client_that_shows_forms_is_asked_by_one_with_the_wording_for_the_account(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        foreach (var status in new[] { UserStatus.Active, UserStatus.Invited })
        {
            var user = await Scene.AddUserAsync(UserRole.Worker, status);

            var (result, row) = await RawAsync(
                token, Args(user.Email, confirm: true, version: user.Version), capabilities);

            Assert.Equal("input_required", result["resultType"]!.GetValue<string>());
            var request = Assert.Single(result["inputRequests"]!.AsObject());
            var form = request.Value!["params"]!;
            Assert.Equal("form", form["mode"]!.GetValue<string>());
            Assert.Equal(FormMessage(user, status), form["message"]!.GetValue<string>());
            var box = Assert.Single(form["requestedSchema"]!["properties"]!.AsObject());
            Assert.Equal("boolean", box.Value!["type"]!.GetValue<string>());
            Assert.Equal(
                status == UserStatus.Invited
                    ? $"Yes, send {user.DisplayName} a new link"
                    : $"Yes, reset {user.DisplayName}'s sign-in",
                box.Value["title"]!.GetValue<string>());
            Assert.DoesNotContain(
                user.Email, result["requestState"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            await AssertUnchangedAsync(user, status);
            AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
        }
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task Accepting_the_form_resets_with_the_version_from_the_state(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(Flow, Tool, token, Args(user.Email), capabilities);

        var (result, row) = await RawAsync(token, Args(user.Email), capabilities, state, AcceptedYes);

        Assert.Null(result["isError"]);
        AssertJson(Done(user, "worker"), result["structuredContent"]);
        AssertChanged(row, AuditEventTypes.User.SetupLinkSent, Tool, Scene.UserOf(ToolCaller.Manager));
        Assert.Equal(UserStatus.Invited, (await Flow.UserAsync(user.Id)).Status);
        Assert.Single(SentTo(user.Email));
    }

    [Theory]
    [MemberData(nameof(AnswersThatAreNotYes), MemberType = typeof(ConfirmationCalls))]
    public async Task Any_answer_but_an_accepted_true_declines_and_changes_nothing(string answer)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(Flow, Tool, token, Args(user.Email), FormCapabilities);

        var (result, row) = await RawAsync(
            token, Args(user.Email), FormCapabilities, state, answer.Length == 0 ? null : answer);

        AssertJson(Declined(), result["structuredContent"]);
        await AssertUnchangedAsync(user, UserStatus.Active);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.UserIds, [user.Id]));
    }

    [Theory]
    [MemberData(nameof(StatesThatAreNotForTheCall))]
    public async Task A_state_that_is_not_for_the_call_is_refused_and_changes_nothing(string kind)
    {
        var managerToken = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var other = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(Flow, Tool, managerToken, Args(user.Email, Reason), FormCapabilities);
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
        await AssertUnchangedAsync(user, UserStatus.Active);
        await AssertUnchangedAsync(other, UserStatus.Active);
    }

    [Fact]
    public async Task A_row_that_changed_between_the_form_and_the_answer_is_refused_as_a_changed_account()
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(Flow, Tool, token, Args(user.Email), FormCapabilities);
        await Flow.ResetSignInAsync(user.Id);

        var (result, row) = await RawAsync(token, Args(user.Email), FormCapabilities, state, AcceptedYes);

        AssertRawError(result, "precondition.failed", AccountChanged);
        AssertRefused(row, Tool, "precondition.failed");
        Assert.Empty(SentTo(user.Email));
    }

    [Fact]
    public async Task An_account_deactivated_between_the_form_and_the_answer_is_refused_as_not_active()
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var user = await Scene.AddUserAsync(UserRole.Worker);
        var state = await AskAsync(Flow, Tool, token, Args(user.Email), FormCapabilities);
        await Flow.DeactivateAsync(user.Id);

        var (result, row) = await RawAsync(token, Args(user.Email), FormCapabilities, state, AcceptedYes);

        AssertRawError(result, "user.not_active", NotActive);
        AssertRefused(row, Tool, "user.not_active");
        Assert.Empty(SentTo(user.Email));
    }

    [Fact]
    public async Task An_sdk_client_with_a_form_handler_is_asked_once_and_its_accept_resets()
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

        Assert.Equal([FormMessage(user, UserStatus.Active)], asked);
        AssertJson(Done(user, "worker"), Content(result));
        Assert.Equal(UserStatus.Invited, (await Flow.UserAsync(user.Id)).Status);
    }

    [Theory]
    [InlineData(ToolCaller.Manager)]
    [InlineData(ToolCaller.Support)]
    public async Task The_reason_is_trimmed_and_kept_in_the_audit_event(ToolCaller caller)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker);

        var (result, row) = await world.CallAsync(
            Tool, Args(user.Email, $"  {Reason}  ", confirm: true, version: user.Version), caller);

        Assert.NotEqual(true, result.IsError);
        AssertChanged(row, AuditEventTypes.User.SetupLinkSent, Tool, Scene.UserOf(caller));
        Assert.Equal(Reason, JsonNode.Parse(row.Details!)!["reason"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Support_resets_or_resends_for_a_worker_with_a_reason(bool invited)
    {
        var user = await Scene.AddUserAsync(UserRole.Worker, invited ? UserStatus.Invited : UserStatus.Active);

        var (preview, _) = await world.CallAsync(Tool, Args(user.Email, Reason), ToolCaller.Support);
        var version = Content(preview).GetProperty("expectedVersion").GetString();
        var (done, row) = await world.CallAsync(
            Tool, Args(user.Email, Reason, confirm: true, version: version), ToolCaller.Support);

        AssertJson(Done(user, "worker"), Content(done));
        AssertChanged(row, AuditEventTypes.User.SetupLinkSent, Tool, Scene.UserOf(ToolCaller.Support));
        Assert.Equal(UserStatus.Invited, (await Flow.UserAsync(user.Id)).Status);
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
        await AssertUnchangedAsync(user, UserStatus.Active);
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
        await AssertUnchangedAsync(user, UserStatus.Active);
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

    [Fact]
    public async Task A_manager_can_reset_a_manager_or_a_support_account_who_is_not_the_only_manager()
    {
        foreach (var role in new[] { UserRole.Manager, UserRole.Support })
        {
            var user = await Scene.AddUserAsync(role);

            var (result, _) = await world.CallAsync(Tool, Args(user.Email, confirm: true, version: user.Version));

            AssertJson(Done(user, role.ToString().ToLowerInvariant()), Content(result));
            Assert.Equal(UserStatus.Invited, (await Flow.UserAsync(user.Id)).Status);
        }
    }

    [Fact]
    public async Task The_tool_is_annotated_destructive_and_open_world_and_tells_the_model_to_ask_who_is_asking()
    {
        var tools = (await world.ClientOf(ToolCaller.Manager).ListToolsAsync(cancellationToken: Ct))
            .ToDictionary(tool => tool.Name);

        var annotations = Assert.IsType<ToolAnnotations>(tools[Tool].ProtocolTool.Annotations);
        Assert.Equal("Reset a user's sign-in or resend their invite", tools[Tool].ProtocolTool.Title);
        Assert.False(annotations.ReadOnlyHint);
        Assert.True(annotations.DestructiveHint);
        Assert.False(annotations.IdempotentHint);
        Assert.True(annotations.OpenWorldHint);
        Assert.Contains("confirmed who is asking", tools[Tool].Description);
        Assert.Contains("data typed by people", tools[Tool].Description);
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

    private static string Effect(SceneUser user, UserStatus status) => status == UserStatus.Invited
        ? $"This sends {user.DisplayName} a new setup link at {user.Email}. The old link stops working."
        : $"This removes {user.DisplayName}'s password, authenticator and passkeys, signs them out everywhere and " +
            $"emails a new setup link to {user.Email}. Confirm who is asking first, by phone or in person.";

    private static string FormMessage(SceneUser user, UserStatus status) => status == UserStatus.Invited
        ? $"Resend the invite to {user.DisplayName} ({user.Email}, worker)? {Effect(user, status)}"
        : $"Reset sign-in for {user.DisplayName} ({user.Email}, worker)? {Effect(user, status)}";

    private static JsonObject Preview(SceneUser user, UserStatus status) => new()
    {
        ["status"] = "preview",
        ["action"] = Tool,
        ["user"] = UserJson(user, "worker", status.ToString().ToLowerInvariant()),
        ["effect"] = Effect(user, status),
        ["expectedVersion"] = user.Version,
        ["toConfirm"] = "Show this to the person. If they say yes, call reset_user_sign_in again with confirm: true "
            + "and this expectedVersion.",
    };

    private static JsonObject Done(SceneUser user, string role) => new()
    {
        ["status"] = "done",
        ["action"] = Tool,
        ["user"] = UserJson(user, role, "invited"),
        ["changed"] = true,
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

    // The setup emails sent to the address; the app of the world keeps every one it sent.
    private List<EmailMessage> SentTo(string address) =>
        [.. Flow.App.Services.GetRequiredService<RecordingEmailSender>().Sent
            .Where(message => string.Equals(message.To, address, StringComparison.OrdinalIgnoreCase))];

    // The user is as they were added: nothing was changed, not even the version, and nothing was sent.
    private async Task AssertUnchangedAsync(SceneUser user, UserStatus status)
    {
        var row = await Flow.UserAsync(user.Id);
        Assert.Equal(status, row.Status);
        Assert.Equal(user.Version, row.ConcurrencyStamp);
        Assert.Empty(SentTo(user.Email));
    }

    private Task<(JsonObject Result, AuditRow Row)> RawAsync(
        string token, JsonObject arguments, string capabilities, string? state = null, string? answer = null) =>
        ConfirmationCalls.RawAsync(Flow, Tool, token, arguments, capabilities, state, answer);
}
