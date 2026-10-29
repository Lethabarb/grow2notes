using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Features.Users;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.ConfirmationCalls;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>invite_user</c> (mcp-server.md §5.2, §5.3, A136 to A138) through the real <c>/mcp</c>: the preview that a client
/// without forms is given, the form that a client with forms is asked by and every answer to it, the order the checks
/// run in, Support's limits, the refusals that only a confirmed call meets, and the one audit row each call writes. The
/// guards that every tool shares are in <see cref="ToolMatrixTests"/>. The cases share the app of
/// <see cref="ToolWorld"/>; each invites a person with an address of its own.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class InviteUserToolTests(Grow2NotesFactory factory, SqlServerFixture sqlServer, ToolWorld world)
    : IClassFixture<Grow2NotesFactory>, IClassFixture<ToolWorld>
{
    private const string Tool = "invite_user";
    private const string Reason = "Sam asked by phone";

    private const string EmailInUse = "This email already has a Grow2Notes account. Nothing was changed.";

    private const string RoleNotAllowed =
        "Invite a worker or a manager. Support accounts are made in Grow2Notes. Nothing was changed.";

    private const string SupportCannot =
        "Support can act on worker accounts only. A manager can do this in Grow2Notes. Nothing was changed.";

    private const string SupportMustSayWhy =
        "Support must send a reason: why, and who asked. Nothing was changed.";

    public static TheoryData<string> RolesThatAreNotAllowed { get; } =
        ["support", "Support", "Worker", "MANAGER", "admin", " worker", "worker "];

    public static TheoryData<string> StatesThatAreNotForTheCall { get; } =
    [
        "flipped",
        "garbage",
        "the manager's, retried by Support",
        "another email",
        "another name",
        "another role",
        "another reason",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    [Theory]
    [InlineData("worker")]
    [InlineData("manager")]
    public async Task The_preview_names_the_account_it_would_make_and_creates_nothing(string role)
    {
        var (email, name) = Fresh();

        var (result, row) = await world.CallAsync(Tool, Args(email, name, role, Reason));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Preview(email, name, role), Content(result));
        Assert.Null(await FindAsync(email));
        Assert.Empty(SentTo(email));
        AssertRead(row, AssistantFlow.ClientId, Tool);
    }

    [Fact]
    public async Task The_preview_and_the_form_do_not_repeat_the_reason()
    {
        var (email, name) = Fresh();
        var token = await world.TokenOf(ToolCaller.Manager);

        var (preview, _) = await world.CallAsync(Tool, Args(email, name, "worker", Reason));
        var (form, _) = await RawAsync(token, Args(email, name, "worker", Reason), FormCapabilities);

        Assert.DoesNotContain(Reason, Body(preview));
        Assert.DoesNotContain(Reason, form.ToJsonString());
    }

    [Theory]
    [InlineData("worker", nameof(UserRole.Worker))]
    [InlineData("manager", nameof(UserRole.Manager))]
    public async Task Confirming_invites_as_the_users_screen_does(string role, string stored)
    {
        var (email, name) = Fresh();

        var (result, row) = await world.CallAsync(Tool, Args(email, name, role, confirm: true));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Done(email, name, role), Content(result));
        var user = Assert.IsType<ApplicationUser>(await FindAsync(email));
        Assert.Equal(Enum.Parse<UserRole>(stored), user.Role);
        Assert.Equal(UserStatus.Invited, user.Status);
        Assert.Equal(Scene.Organisation.OrganisationId, user.OrganisationId);
        Assert.Equal(Scene.UserOf(ToolCaller.Manager), user.InvitedByUserId);
        Assert.Equal(name, user.DisplayName);
        var message = Assert.Single(SentTo(email));
        Assert.Equal(email, SetupLinks.FromEmail(message).Email);
        AssertChanged(row, AuditEventTypes.User.Invited, Tool, Scene.UserOf(ToolCaller.Manager));
        var details = JsonNode.Parse(row.Details!)!.AsObject();
        var expected = new JsonObject { ["email"] = email, ["displayName"] = name, ["role"] = stored };
        Assert.True(JsonNode.DeepEquals(expected, details["new"]), $"Expected {expected}, got {details["new"]}.");
        Assert.Null(details["reason"]);
    }

    [Theory]
    [InlineData(ToolCaller.Manager)]
    [InlineData(ToolCaller.Support)]
    public async Task The_reason_is_trimmed_and_kept_in_the_audit_event(ToolCaller caller)
    {
        var (email, name) = Fresh();

        var (result, row) = await world.CallAsync(
            Tool, Args(email, name, "worker", $"  {Reason}  ", confirm: true), caller);

        Assert.NotEqual(true, result.IsError);
        AssertChanged(row, AuditEventTypes.User.Invited, Tool, Scene.UserOf(caller));
        Assert.Equal(Reason, JsonNode.Parse(row.Details!)!["reason"]!.GetValue<string>());
        Assert.Equal(Scene.UserOf(caller), (await FindAsync(email))!.InvitedByUserId);
    }

    [Fact]
    public async Task Support_invites_a_worker_with_a_reason_through_the_preview_and_then_the_confirmation()
    {
        var (email, name) = Fresh();
        var arguments = Args(email, name, "worker", Reason);

        var (preview, previewRow) = await world.CallAsync(Tool, arguments, ToolCaller.Support);
        arguments["confirm"] = true;
        var (done, doneRow) = await world.CallAsync(Tool, arguments, ToolCaller.Support);

        AssertJson(Preview(email, name, "worker"), Content(preview));
        AssertRead(previewRow, AssistantFlow.ClientId, Tool);
        AssertJson(Done(email, name, "worker"), Content(done));
        AssertChanged(doneRow, AuditEventTypes.User.Invited, Tool, Scene.UserOf(ToolCaller.Support));
        Assert.Equal(UserStatus.Invited, (await FindAsync(email))!.Status);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("", false)]
    [InlineData("   ", true)]
    public async Task Support_must_send_a_reason_at_the_preview_and_with_the_confirmation(
        string? reason, bool confirm)
    {
        var (email, name) = Fresh();

        var (result, row) = await world.CallAsync(
            Tool, Args(email, name, "worker", reason, confirm), ToolCaller.Support);

        AssertError(result, "reason.required", SupportMustSayWhy);
        AssertRefused(row, Tool, "reason.required");
        Assert.Null(await FindAsync(email));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Support_cannot_invite_a_manager_even_with_a_reason(bool confirm)
    {
        var (email, name) = Fresh();

        var (result, row) = await world.CallAsync(
            Tool, Args(email, name, "manager", Reason, confirm), ToolCaller.Support);

        AssertError(result, "role.not_allowed", SupportCannot);
        AssertRefused(row, Tool, "role.not_allowed");
        Assert.Null(await FindAsync(email));
    }

    [Theory]
    [MemberData(nameof(RolesThatAreNotAllowed))]
    public async Task A_role_that_is_not_exactly_worker_or_manager_is_refused_for_every_caller(string role)
    {
        foreach (var caller in new[] { ToolCaller.Manager, ToolCaller.Support })
        {
            var (email, name) = Fresh();

            var (result, row) = await world.CallAsync(
                Tool, Args(email, name, role, Reason, confirm: true), caller);

            AssertError(result, "role.not_allowed", RoleNotAllowed);
            AssertRefused(row, Tool, "role.not_allowed");
            Assert.Null(await FindAsync(email));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_role_is_a_field_error(string role)
    {
        var (email, name) = Fresh();

        var (result, row) = await world.CallAsync(Tool, Args(email, name, role));

        AssertValidationFailed(result, "role", "Choose worker or manager");
        AssertRefused(row, Tool, "validation.failed");
    }

    [Fact]
    public async Task A_blank_name_and_a_blank_email_are_refused_at_the_first_call_in_the_apps_words()
    {
        var (blankName, nameRow) = await world.CallAsync(Tool, Args("a@example.test", "   ", "worker"));
        var (blankEmail, emailRow) = await world.CallAsync(Tool, Args("   ", "Invitee Blank", "worker"));
        var (both, _) = await world.CallAsync(Tool, Args("", " ", ""));

        AssertValidationFailed(blankName, "displayName", UserDetailsChecks.NameMissing);
        AssertRefused(nameRow, Tool, "validation.failed");
        AssertValidationFailed(blankEmail, "email", UserDetailsChecks.EmailMissing);
        AssertRefused(emailRow, Tool, "validation.failed");
        var fields = Content(both).GetProperty("error").GetProperty("fields");
        Assert.Equal(
            ["displayName", "email", "role"], fields.EnumerateObject().Select(field => field.Name).Order());
    }

    [Fact]
    public async Task The_checks_run_in_the_order_the_design_pins()
    {
        var (email, name) = Fresh();

        var (adminNoName, _) = await world.CallAsync(Tool, Args(email, "  ", "admin"));
        var (supportNoName, _) = await world.CallAsync(
            Tool, Args(email, "  ", "manager"), ToolCaller.Support);
        var (supportManager, _) = await world.CallAsync(
            Tool, Args(email, name, "manager"), ToolCaller.Support);

        Assert.Equal("validation.failed", ToolError.CodeOf(adminNoName));
        Assert.Equal("validation.failed", ToolError.CodeOf(supportNoName));
        Assert.Equal("role.not_allowed", ToolError.CodeOf(supportManager));
    }

    [Fact]
    public async Task An_address_that_has_an_account_is_previewed_and_refused_only_when_confirmed_with_one_answer()
    {
        var elsewhere = (await Flow.UserAsync(Scene.OtherOrganisation.ManagerId)).Email!;
        var addresses = new[]
        {
            Scene.InvitedUser.Email,
            Scene.InvitedUser.Email.ToUpperInvariant(),
            elsewhere,
            Scene.DeactivatedUser.Email,
        };

        foreach (var address in addresses)
        {
            var (preview, previewRow) = await world.CallAsync(Tool, Args(address, "Invitee Twice", "worker"));
            var (confirmed, row) = await world.CallAsync(
                Tool, Args(address, "Invitee Twice", "worker", confirm: true));

            Assert.Equal("preview", Content(preview).GetProperty("status").GetString());
            AssertRead(previewRow, AssistantFlow.ClientId, Tool);
            AssertError(confirmed, "user.email_in_use", EmailInUse);
            Assert.DoesNotContain(address, Body(confirmed), StringComparison.OrdinalIgnoreCase);
            AssertRefused(row, Tool, "user.email_in_use");
            Assert.Empty(SentTo(address));
        }
    }

    [Fact]
    public async Task An_address_that_identity_refuses_is_refused_under_email_only_when_confirmed()
    {
        var email = $"zoë.{Guid.NewGuid():N}@example.test";

        var (preview, _) = await world.CallAsync(Tool, Args(email, "Invitee Letter", "worker"));
        var (confirmed, row) = await world.CallAsync(
            Tool, Args(email, "Invitee Letter", "worker", confirm: true));

        Assert.Equal("preview", Content(preview).GetProperty("status").GetString());
        AssertValidationFailed(confirmed, "email", UserDetailsChecks.EmailNotAnAddress);
        AssertRefused(row, Tool, "validation.failed");
        Assert.Empty(SentTo(email));
    }

    [Fact]
    public async Task The_name_and_the_address_are_trimmed()
    {
        var (email, name) = Fresh();

        var (result, _) = await world.CallAsync(
            Tool, Args($"  {email}  ", $"  {name}  ", "worker", confirm: true));

        AssertJson(Done(email, name, "worker"), Content(result));
        Assert.Equal(name, (await FindAsync(email))!.DisplayName);
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task A_client_that_shows_forms_is_asked_by_one_even_when_it_sent_confirm_true(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        foreach (var confirmed in new[] { false, true })
        {
            var (email, name) = Fresh();

            var (result, row) = await RawAsync(token, Args(email, name, "manager", confirm: confirmed), capabilities);

            Assert.Equal("input_required", result["resultType"]!.GetValue<string>());
            var request = Assert.Single(result["inputRequests"]!.AsObject());
            Assert.Equal("confirm", request.Key);
            var form = request.Value!["params"]!;
            Assert.Equal("form", form["mode"]!.GetValue<string>());
            Assert.Equal(FormMessage(email, name, "manager"), form["message"]!.GetValue<string>());
            var box = Assert.Single(form["requestedSchema"]!["properties"]!.AsObject());
            Assert.Equal("boolean", box.Value!["type"]!.GetValue<string>());
            Assert.Equal($"Yes, invite {name}", box.Value["title"]!.GetValue<string>());
            Assert.DoesNotContain(
                email, result["requestState"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
            Assert.Null(await FindAsync(email));
            AssertRead(row, AssistantFlow.ClientId, Tool);
        }
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetThePreview), MemberType = typeof(ConfirmationCalls))]
    public async Task A_2026_client_without_form_elicitation_is_given_the_preview(string capabilities)
    {
        var (email, name) = Fresh();

        var token = await world.TokenOf(ToolCaller.Manager);

        var (result, row) = await RawAsync(token, Args(email, name, "worker"), capabilities);

        Assert.Null(result["inputRequests"]);
        AssertJson(Preview(email, name, "worker"), result["structuredContent"]);
        Assert.Null(await FindAsync(email));
        AssertRead(row, AssistantFlow.ClientId, Tool);
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task Accepting_the_form_invites(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var (email, name) = Fresh();
        var arguments = Args(email, name, "worker", Reason);
        var state = await AskAsync(Flow, Tool, token, arguments, capabilities);

        var (result, row) = await RawAsync(token, arguments, capabilities, state, AcceptedYes);

        Assert.Null(result["isError"]);
        AssertJson(Done(email, name, "worker"), result["structuredContent"]);
        AssertChanged(row, AuditEventTypes.User.Invited, Tool, Scene.UserOf(ToolCaller.Manager));
        Assert.Equal(UserStatus.Invited, (await FindAsync(email))!.Status);
        Assert.Single(SentTo(email));
    }

    [Theory]
    [MemberData(nameof(AnswersThatAreNotYes), MemberType = typeof(ConfirmationCalls))]
    public async Task Any_answer_but_an_accepted_true_declines_and_creates_nothing(string answer)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var (email, name) = Fresh();
        var arguments = Args(email, name, "worker");
        var state = await AskAsync(Flow, Tool, token, arguments, FormCapabilities);

        var (result, row) = await RawAsync(
            token, arguments, FormCapabilities, state, answer.Length == 0 ? null : answer);

        AssertJson(Declined(), result["structuredContent"]);
        Assert.Null(await FindAsync(email));
        AssertRead(row, AssistantFlow.ClientId, Tool);
    }

    [Theory]
    [MemberData(nameof(StatesThatAreNotForTheCall))]
    public async Task A_state_that_is_not_for_the_call_is_refused_and_creates_nothing(string kind)
    {
        var managerToken = await world.TokenOf(ToolCaller.Manager);
        var (email, name) = Fresh();
        var (other, _) = Fresh();
        var state = await AskAsync(Flow, Tool, managerToken, Args(email, name, "worker", Reason), FormCapabilities);
        var (token, arguments, sent) = kind switch
        {
            "flipped" => (managerToken, Args(email, name, "worker", Reason), Flip(state)),
            "garbage" => (managerToken, Args(email, name, "worker", Reason), "garbage"),
            "another email" => (managerToken, Args(other, name, "worker", Reason), state),
            "another name" => (managerToken, Args(email, "Someone Else", "worker", Reason), state),
            "another role" => (managerToken, Args(email, name, "manager", Reason), state),
            "another reason" => (managerToken, Args(email, name, "worker", "Asked by someone else"), state),
            _ => (await world.TokenOf(ToolCaller.Support), Args(email, name, "worker", Reason), state),
        };

        var (result, row) = await RawAsync(token, arguments, FormCapabilities, sent, AcceptedYes);

        AssertRawError(result, "precondition.failed", StateRefused);
        AssertRefused(row, Tool, "precondition.failed");
        Assert.Null(await FindAsync(email));
        Assert.Null(await FindAsync(other));
    }

    [Fact]
    public async Task A_state_that_has_lapsed_is_refused_and_creates_nothing()
    {
        await using var flow = await AssistantFlow.StartAsync(factory, sqlServer, "InviteUserTool", probes: false);
        var scene = await ToolScene.CreateAsync(flow);
        var token = await scene.ConnectAsync(ToolCaller.Manager);
        var (email, name) = Fresh();
        var arguments = Args(email, name, "worker");
        var state = await AskAsync(flow, Tool, token, arguments, FormCapabilities);
        flow.Clock.Advance(ToolConfirmation.Lifetime + TimeSpan.FromSeconds(1));

        var (result, row) = await ConfirmationCalls.RawAsync(
            flow, Tool, token, arguments, FormCapabilities, state, AcceptedYes);

        AssertRawError(result, "precondition.failed", StateRefused);
        AssertRefused(row, Tool, "precondition.failed");
        Assert.Empty(flow.App.Services.GetRequiredService<RecordingEmailSender>().Sent);
    }

    [Fact]
    public async Task A_replayed_accept_meets_the_address_it_made_and_leaves_one_account()
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var (email, name) = Fresh();
        var arguments = Args(email, name, "worker");
        var state = await AskAsync(Flow, Tool, token, arguments, FormCapabilities);

        var (first, _) = await RawAsync(token, arguments, FormCapabilities, state, AcceptedYes);
        var (replay, row) = await RawAsync(token, arguments, FormCapabilities, state, AcceptedYes);

        AssertJson(Done(email, name, "worker"), first["structuredContent"]);
        AssertRawError(replay, "user.email_in_use", EmailInUse);
        AssertRefused(row, Tool, "user.email_in_use");
        Assert.Single(SentTo(email));
    }

    [Fact]
    public async Task An_sdk_client_with_a_form_handler_is_asked_once_and_its_accept_invites()
    {
        var (email, name) = Fresh();
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

        var result = await ToolScene.CallAsync(client, Tool, Args(email, name, "worker"));

        Assert.Equal([FormMessage(email, name, "worker")], asked);
        AssertJson(Done(email, name, "worker"), Content(result));
        Assert.Equal(UserStatus.Invited, (await FindAsync(email))!.Status);
    }

    [Fact]
    public async Task An_sdk_client_with_a_form_handler_that_declines_creates_nothing()
    {
        var (email, name) = Fresh();
        await using var client = await Flow.McpClientAsync(
            await world.TokenOf(ToolCaller.Manager),
            "2026-07-28",
            elicitation: (_, _) => ValueTask.FromResult(new ElicitResult { Action = "decline" }));

        var result = await ToolScene.CallAsync(client, Tool, Args(email, name, "worker"));

        AssertJson(Declined(), Content(result));
        Assert.Null(await FindAsync(email));
    }

    [Fact]
    public async Task The_tool_is_annotated_open_world_and_not_destructive()
    {
        var tools = (await world.ClientOf(ToolCaller.Manager).ListToolsAsync(cancellationToken: Ct))
            .ToDictionary(tool => tool.Name);

        var annotations = Assert.IsType<ToolAnnotations>(tools[Tool].ProtocolTool.Annotations);
        Assert.Equal("Invite a user", tools[Tool].ProtocolTool.Title);
        Assert.False(annotations.ReadOnlyHint);
        Assert.False(annotations.DestructiveHint);
        Assert.False(annotations.IdempotentHint);
        Assert.True(annotations.OpenWorldHint);
        Assert.Contains("data typed by people", tools[Tool].Description);
    }

    [Fact]
    public async Task The_tools_that_act_on_users_are_exactly_these_four()
    {
        var listed = await world.ClientOf(ToolCaller.Manager).ListToolsAsync(cancellationToken: Ct);

        Assert.Equal(
            ["deactivate_user", "invite_user", "list_users", "reset_user_sign_in"],
            listed.Select(tool => tool.Name).Where(name => name.Contains("user")).Order(StringComparer.Ordinal));
    }

    private static (string Email, string Name) Fresh()
    {
        var unique = Guid.NewGuid().ToString("N");
        return ($"invitee.{unique}@example.test", $"Invitee {unique[..12]}");
    }

    private static JsonObject Args(
        string email, string name, string role, string? reason = null, bool confirm = false)
    {
        var arguments = new JsonObject { ["email"] = email, ["displayName"] = name, ["role"] = role };
        if (reason is not null)
        {
            arguments["reason"] = reason;
        }

        if (confirm)
        {
            arguments["confirm"] = true;
        }

        return arguments;
    }

    private static string Effect(string email, string name, string role) =>
        $"Grow2Notes will email a setup link to {email}. It works once and lasts 7 days. " +
        $"As a {role}, {name} will be able to read every participant's notes.";

    private static string FormMessage(string email, string name, string role) =>
        $"Invite {name} ({email}, {role})? {Effect(email, name, role)}";

    private static JsonObject Preview(string email, string name, string role) => new()
    {
        ["status"] = "preview",
        ["action"] = Tool,
        ["user"] = UserJson(email, name, role),
        ["effect"] = Effect(email, name, role),
        ["toConfirm"] = "Show this to the person. If they say yes, call invite_user again with the same arguments "
            + "and confirm: true.",
    };

    private static JsonObject Done(string email, string name, string role) => new()
    {
        ["status"] = "done",
        ["action"] = Tool,
        ["user"] = UserJson(email, name, role),
        ["changed"] = true,
    };

    private static JsonObject Declined() => new()
    {
        ["status"] = "declined",
        ["action"] = Tool,
        ["message"] = "Nothing was changed.",
    };

    private static JsonObject UserJson(string email, string name, string role) => new()
    {
        ["email"] = email,
        ["displayName"] = name,
        ["role"] = role,
        ["status"] = "invited",
    };

    // The setup emails sent to the address; the app of the world keeps every one it sent.
    private List<EmailMessage> SentTo(string address) =>
        [.. Flow.App.Services.GetRequiredService<RecordingEmailSender>().Sent
            .Where(message => string.Equals(message.To, address, StringComparison.OrdinalIgnoreCase))];

    private async Task<ApplicationUser?> FindAsync(string email)
    {
        await using var scope = Flow.App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    private Task<(JsonObject Result, AuditRow Row)> RawAsync(
        string token, JsonObject arguments, string capabilities, string? state = null, string? answer = null) =>
        ConfirmationCalls.RawAsync(Flow, Tool, token, arguments, capabilities, state, answer);
}
