using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Mcp.Contracts;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.ConfirmationCalls;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;
using static Grow2Notes.Tests.Features.Mcp.ToolCalls;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>archive_participant</c> and <c>restore_participant</c> (mcp-server.md §5.2, S07.04.06, A136, A137) through the
/// real <c>/mcp</c>: archive asks the person first, by the preview that a client without forms is given or the form
/// that a client with forms is asked by, and restore acts on the first call. The guards that every tool shares are in
/// <see cref="ToolMatrixTests"/>, and the confirmation itself is proved on its stand-in in
/// <see cref="ToolConfirmationTests"/>. The cases share the app of <see cref="ToolWorld"/>; each adds a participant of
/// its own to change, never one of the scene's.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantArchiveToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Tool = "archive_participant";
    private const string Restore = "restore_participant";

    private const string ParticipantChanged =
        "This participant changed after the person was asked. Nothing was changed. " +
        "Call the tool again to ask them afresh.";

    private const string ParticipantWasChanged =
        "This participant was changed since you looked them up. Look them up again with get_participant, then retry.";

    public static TheoryData<string> StatesThatAreNotForTheCall { get; } =
        ["flipped", "garbage", "the manager's, retried by Support", "another participant"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    [Fact]
    public async Task The_preview_names_the_participant_and_its_version_and_changes_nothing()
    {
        var added = await AddAsync();

        var (result, row) = await world.CallAsync(Tool, Args(added));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Preview(added), Content(result));
        await AssertStoredAsync(added, ParticipantStatus.Active);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
        Assert.Equal(added.Id, row.ParticipantId);
    }

    [Fact]
    public async Task Confirming_with_the_current_version_archives_without_an_earlier_preview()
    {
        var added = await AddAsync();

        var (result, row) = await world.CallAsync(Tool, Args(added, confirm: true, version: added.Version));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Done(added, changed: true), Content(result));
        AssertChanged(row, AuditEventTypes.Participant.Archived, Tool, Scene.UserOf(ToolCaller.Manager));
        Assert.Equal(added.Id, row.ParticipantId);
        var details = JsonNode.Parse(row.Details!)!;
        Assert.Equal("Active", details["old"]!["status"]!.GetValue<string>());
        Assert.Equal("Archived", details["new"]!["status"]!.GetValue<string>());
        var stored = await StoredAsync(added.Id);
        Assert.Equal(ParticipantStatus.Archived, stored.Status);
        Assert.NotNull(stored.ArchivedAtUtc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Confirming_with_no_version_is_refused_for_it(string? version)
    {
        var added = await AddAsync();

        var (result, row) = await world.CallAsync(Tool, Args(added, confirm: true, version: version));

        Assert.Equal("precondition.required", ToolError.CodeOf(result));
        AssertRefused(row, Tool, "precondition.required");
        await AssertStoredAsync(added, ParticipantStatus.Active);
    }

    [Fact]
    public async Task Confirming_with_a_stale_version_is_refused_as_a_changed_participant()
    {
        var added = await AddAsync();

        var (result, row) = await world.CallAsync(Tool, Args(added, confirm: true, version: StaleVersion));

        AssertError(result, "precondition.failed", ParticipantChanged);
        AssertRefused(row, Tool, "precondition.failed");
        await AssertStoredAsync(added, ParticipantStatus.Active);
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetThePreview), MemberType = typeof(ConfirmationCalls))]
    public async Task A_2026_client_without_form_elicitation_is_given_the_preview(string capabilities)
    {
        var added = await AddAsync();

        var (result, row) = await RawAsync(await world.TokenOf(ToolCaller.Manager), Args(added), capabilities);

        Assert.Null(result["inputRequests"]);
        AssertJson(Preview(added), result["structuredContent"]);
        await AssertStoredAsync(added, ParticipantStatus.Active);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task A_client_that_shows_forms_is_asked_by_one_even_when_it_sent_confirm_true(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        foreach (var confirmed in new[] { false, true })
        {
            var added = await AddAsync();
            var arguments = confirmed ? Args(added, confirm: true, version: added.Version) : Args(added);

            var (result, row) = await RawAsync(token, arguments, capabilities);

            Assert.Equal("input_required", result["resultType"]!.GetValue<string>());
            var request = Assert.Single(result["inputRequests"]!.AsObject());
            Assert.Equal("confirm", request.Key);
            var form = request.Value!["params"]!;
            Assert.Equal("form", form["mode"]!.GetValue<string>());
            Assert.Equal(FormMessage(added), form["message"]!.GetValue<string>());
            var box = Assert.Single(form["requestedSchema"]!["properties"]!.AsObject());
            Assert.Equal("confirm", box.Key);
            Assert.Equal(
                $"Yes, archive {added.GivenName} {added.FamilyName}", box.Value!["title"]!.GetValue<string>());
            await AssertStoredAsync(added, ParticipantStatus.Active);
            AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
        }
    }

    [Theory]
    [MemberData(nameof(CapabilitiesThatGetTheForm), MemberType = typeof(ConfirmationCalls))]
    public async Task Accepting_the_form_archives_with_the_version_from_the_state(string capabilities)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var added = await AddAsync();
        var state = await AskAsync(token, added, capabilities);

        var (result, row) = await RawAsync(token, Args(added), capabilities, state, AcceptedYes);

        Assert.Null(result["isError"]);
        AssertJson(Done(added, changed: true), result["structuredContent"]);
        AssertChanged(row, AuditEventTypes.Participant.Archived, Tool, Scene.UserOf(ToolCaller.Manager));
        await AssertStoredAsync(added, ParticipantStatus.Archived, versionChanged: true);
    }

    [Theory]
    [MemberData(nameof(AnswersThatAreNotYes), MemberType = typeof(ConfirmationCalls))]
    public async Task Any_answer_but_an_accepted_true_declines_and_changes_nothing(string answer)
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var added = await AddAsync();
        var state = await AskAsync(token, added, FormCapabilities);

        var (result, row) = await RawAsync(
            token, Args(added), FormCapabilities, state, answer.Length == 0 ? null : answer);

        AssertJson(Declined(), result["structuredContent"]);
        await AssertStoredAsync(added, ParticipantStatus.Active);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
    }

    [Theory]
    [MemberData(nameof(StatesThatAreNotForTheCall))]
    public async Task A_state_that_is_not_for_the_call_is_refused_and_changes_nothing(string kind)
    {
        var managerToken = await world.TokenOf(ToolCaller.Manager);
        var added = await AddAsync();
        var other = await AddAsync();
        var state = await AskAsync(managerToken, added, FormCapabilities);
        var (token, arguments, sent) = kind switch
        {
            "flipped" => (managerToken, Args(added), Flip(state)),
            "garbage" => (managerToken, Args(added), "garbage"),
            "another participant" => (managerToken, Args(other), state),
            _ => (await world.TokenOf(ToolCaller.Support), Args(added), state),
        };

        var (result, row) = await RawAsync(token, arguments, FormCapabilities, sent, AcceptedYes);

        AssertRawError(result, "precondition.failed", StateRefused);
        AssertRefused(row, Tool, "precondition.failed");
        await AssertStoredAsync(added, ParticipantStatus.Active);
        await AssertStoredAsync(other, ParticipantStatus.Active);
    }

    [Fact]
    public async Task A_participant_changed_between_the_form_and_the_answer_is_refused_as_changed()
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var added = await AddAsync();
        var state = await AskAsync(token, added, FormCapabilities);
        var (renamed, _) = await world.CallAsync(
            "update_participant", new()
            {
                ["participantId"] = added.Id.ToString(),
                ["expectedVersion"] = added.Version,
                ["givenName"] = $"Eve{Guid.NewGuid():N}",
            });
        Assert.NotEqual(true, renamed.IsError);

        var (result, row) = await RawAsync(token, Args(added), FormCapabilities, state, AcceptedYes);

        AssertRawError(result, "precondition.failed", ParticipantChanged);
        AssertRefused(row, Tool, "precondition.failed");
        Assert.Equal(ParticipantStatus.Active, (await StoredAsync(added.Id)).Status);
    }

    [Fact]
    public async Task A_participant_archived_between_the_form_and_the_answer_answers_unchanged()
    {
        var token = await world.TokenOf(ToolCaller.Manager);
        var added = await AddAsync();
        var state = await AskAsync(token, added, FormCapabilities);
        var (archived, _) = await world.CallAsync(Tool, Args(added, confirm: true, version: added.Version));
        Assert.NotEqual(true, archived.IsError);

        var (result, row) = await RawAsync(token, Args(added), FormCapabilities, state, AcceptedYes);

        AssertJson(Done(added, changed: false), result["structuredContent"]);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
    }

    [Fact]
    public async Task An_sdk_client_with_a_form_handler_is_asked_once_and_its_accept_archives()
    {
        var added = await AddAsync();
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

        var result = await ToolScene.CallAsync(client, Tool, Args(added));

        Assert.Equal([FormMessage(added)], asked);
        AssertJson(Done(added, changed: true), Content(result));
        Assert.Equal(ParticipantStatus.Archived, (await StoredAsync(added.Id)).Status);
    }

    [Fact]
    public async Task An_sdk_client_with_a_form_handler_that_declines_changes_nothing()
    {
        var added = await AddAsync();
        await using var client = await Flow.McpClientAsync(
            await world.TokenOf(ToolCaller.Manager),
            "2026-07-28",
            elicitation: (_, _) => ValueTask.FromResult(new ElicitResult { Action = "decline" }));

        var result = await ToolScene.CallAsync(client, Tool, Args(added));

        AssertJson(Declined(), Content(result));
        await AssertStoredAsync(added, ParticipantStatus.Active);
    }

    [Fact]
    public async Task Support_archives_like_a_manager_with_no_reason()
    {
        var added = await AddAsync();

        var (preview, _) = await world.CallAsync(Tool, Args(added), ToolCaller.Support);
        var (result, row) = await world.CallAsync(
            Tool, Args(added, confirm: true, version: added.Version), ToolCaller.Support);

        AssertJson(Preview(added), Content(preview));
        AssertJson(Done(added, changed: true), Content(result));
        AssertChanged(row, AuditEventTypes.Participant.Archived, Tool, Scene.UserOf(ToolCaller.Support));
        Assert.Null(JsonNode.Parse(row.Details!)!["reason"]);
        Assert.Equal(ParticipantStatus.Archived, (await StoredAsync(added.Id)).Status);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, StaleVersion)]
    public async Task An_archived_participant_answers_unchanged_before_any_preview_or_version_is_looked_at(
        bool confirm, string? version)
    {
        var added = await AddAsync(archived: true);

        var (result, row) = await world.CallAsync(Tool, Args(added, confirm, version));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Done(added, changed: false), Content(result));
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
        await AssertStoredAsync(added, ParticipantStatus.Archived);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    public async Task An_archived_participant_is_never_asked_about_by_a_form(string? state)
    {
        var added = await AddAsync(archived: true);

        var token = await world.TokenOf(ToolCaller.Manager);

        var (result, row) = await RawAsync(
            token, Args(added), FormCapabilities, state, state is null ? null : AcceptedYes);

        Assert.Null(result["inputRequests"]);
        AssertJson(Done(added, changed: false), result["structuredContent"]);
        AssertRead(row, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
    }

    [Fact]
    public async Task A_restore_acts_on_the_first_call_and_returns_the_new_version()
    {
        var added = await AddAsync(archived: true);

        var (result, row) = await world.CallAsync(Restore, RestoreArgs(added, added.Version));

        Assert.NotEqual(true, result.IsError);
        var stored = await StoredAsync(added.Id);
        Assert.Equal(ParticipantStatus.Active, stored.Status);
        Assert.Null(stored.ArchivedAtUtc);
        var version = ToolScene.ExpectedVersion(RowVersionToken.ETag(stored));
        Assert.NotEqual(added.Version, version);
        AssertJson(Restored(added, changed: true, version), Content(result));
        AssertChanged(row, AuditEventTypes.Participant.Restored, Restore, Scene.UserOf(ToolCaller.Manager));
        Assert.Equal(added.Id, row.ParticipantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_restore_with_no_version_is_refused_for_it(string? version)
    {
        var added = await AddAsync(archived: true);

        var (result, row) = await world.CallAsync(Restore, RestoreArgs(added, version));

        Assert.Equal("precondition.required", ToolError.CodeOf(result));
        AssertRefused(row, Restore, "precondition.required");
        await AssertStoredAsync(added, ParticipantStatus.Archived);
    }

    [Fact]
    public async Task A_restore_with_a_stale_version_is_refused_as_a_changed_participant()
    {
        var added = await AddAsync(archived: true);

        var (result, row) = await world.CallAsync(Restore, RestoreArgs(added, StaleVersion));

        AssertError(result, "precondition.failed", ParticipantWasChanged);
        AssertRefused(row, Restore, "precondition.failed");
        await AssertStoredAsync(added, ParticipantStatus.Archived);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(StaleVersion)]
    [InlineData("current")]
    public async Task An_active_participant_answers_unchanged_to_a_restore_whatever_the_version(string? version)
    {
        var added = await AddAsync();

        var (result, row) = await world.CallAsync(
            Restore, RestoreArgs(added, version == "current" ? added.Version : version));

        Assert.NotEqual(true, result.IsError);
        AssertJson(Restored(added, changed: false, added.Version), Content(result));
        AssertRead(row, AssistantFlow.ClientId, Restore, (McpCall.ParticipantIds, [added.Id]));
        await AssertStoredAsync(added, ParticipantStatus.Active);
    }

    [Fact]
    public async Task Archiving_again_and_restoring_again_answer_unchanged()
    {
        var added = await AddAsync();

        var (archived, _) = await world.CallAsync(Tool, Args(added, confirm: true, version: added.Version));
        var (archivedAgain, archivedAgainRow) = await world.CallAsync(
            Tool, Args(added, confirm: true, version: added.Version));
        var afterArchive = ToolScene.ExpectedVersion(RowVersionToken.ETag(await StoredAsync(added.Id)));
        var (restored, _) = await world.CallAsync(Restore, RestoreArgs(added, afterArchive));
        var afterRestore = ToolScene.ExpectedVersion(RowVersionToken.ETag(await StoredAsync(added.Id)));
        var (restoredAgain, restoredAgainRow) = await world.CallAsync(Restore, RestoreArgs(added, afterRestore));

        Assert.True(Content(archived).GetProperty("changed").GetBoolean());
        Assert.False(Content(archivedAgain).GetProperty("changed").GetBoolean());
        AssertRead(archivedAgainRow, AssistantFlow.ClientId, Tool, (McpCall.ParticipantIds, [added.Id]));
        Assert.True(Content(restored).GetProperty("changed").GetBoolean());
        Assert.False(Content(restoredAgain).GetProperty("changed").GetBoolean());
        AssertRead(restoredAgainRow, AssistantFlow.ClientId, Restore, (McpCall.ParticipantIds, [added.Id]));
    }

    [Fact]
    public async Task An_archived_participant_is_looked_up_again_and_restored_with_the_version_found()
    {
        var added = await AddAsync();
        var (archived, _) = await world.CallAsync(Tool, Args(added, confirm: true, version: added.Version));
        Assert.False(Content(archived).TryGetProperty("expectedVersion", out _));

        var (got, _) = await world.CallAsync("get_participant", new() { ["participantId"] = added.Id.ToString() });
        var (found, _) = await world.CallAsync(
            "find_participants", new() { ["search"] = added.FamilyName, ["includeArchived"] = true });
        var version = Content(got).GetProperty("expectedVersion").GetString();
        var (restored, _) = await world.CallAsync(Restore, RestoreArgs(added, version));

        Assert.Equal("archived", Content(got).GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(version));
        var match = Assert.Single(Content(found).GetProperty("participants").EnumerateArray());
        Assert.Equal(added.Id, match.GetProperty("participantId").GetGuid());
        Assert.Equal("archived", match.GetProperty("status").GetString());
        Assert.True(Content(restored).GetProperty("changed").GetBoolean());
        Assert.Equal(ParticipantStatus.Active, (await StoredAsync(added.Id)).Status);
    }

    [Fact]
    public async Task The_goal_tools_refuse_while_a_participant_is_archived_and_work_after_a_restore()
    {
        var added = await AddAsync();
        var goal = new JsonObject { ["participantId"] = added.Id.ToString(), ["text"] = $"Goal {Guid.NewGuid():N}" };
        await world.CallAsync(Tool, Args(added, confirm: true, version: added.Version));

        var (refused, refusedRow) = await world.CallAsync("add_goal", goal);
        var version = ToolScene.ExpectedVersion(RowVersionToken.ETag(await StoredAsync(added.Id)));
        await world.CallAsync(Restore, RestoreArgs(added, version));
        var (goalAdded, addedRow) = await world.CallAsync("add_goal", goal);

        AssertError(refused, "participant.archived", "This participant is archived. Restore the participant first.");
        AssertRefused(refusedRow, "add_goal", "participant.archived");
        Assert.NotEqual(true, goalAdded.IsError);
        Assert.Equal(AuditEventTypes.Goal.Created, addedRow.EventType);
    }

    private static string Effect(Added added) =>
        $"{added.GivenName} {added.FamilyName} will be removed from Today and search, and no new notes can be " +
        "started for them. Their notes, reports and exports are unchanged. You can restore them later.";

    private static string FormMessage(Added added) => $"Archive {added.GivenName} {added.FamilyName}? {Effect(added)}";

    private static JsonObject ParticipantJson(Added added, string status) => new()
    {
        ["participantId"] = added.Id.ToString(),
        ["givenName"] = added.GivenName,
        ["familyName"] = added.FamilyName,
        ["status"] = status,
    };

    private static JsonObject Preview(Added added) => new()
    {
        ["status"] = "preview",
        ["action"] = Tool,
        ["participant"] = ParticipantJson(added, "active"),
        ["effect"] = Effect(added),
        ["expectedVersion"] = added.Version,
        ["toConfirm"] = "Show this to the person. If they say yes, call archive_participant again with confirm: " +
            "true and this expectedVersion.",
    };

    private static JsonObject Done(Added added, bool changed) => new()
    {
        ["status"] = "done",
        ["action"] = Tool,
        ["participant"] = ParticipantJson(added, "archived"),
        ["changed"] = changed,
    };

    private static JsonObject Declined() => new()
    {
        ["status"] = "declined",
        ["action"] = Tool,
        ["message"] = "Nothing was changed.",
    };

    private static JsonObject Restored(Added added, bool changed, string version) => new()
    {
        ["givenName"] = added.GivenName,
        ["familyName"] = added.FamilyName,
        ["status"] = "active",
        ["changed"] = changed,
        ["expectedVersion"] = version,
    };

    // The arguments of archive_participant; a version is sent when one is given, an empty one included.
    private static JsonObject Args(Added added, bool confirm = false, string? version = null)
    {
        var arguments = new JsonObject { ["participantId"] = added.Id.ToString() };
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

    private static JsonObject RestoreArgs(Added added, string? version)
    {
        var arguments = new JsonObject { ["participantId"] = added.Id.ToString() };
        if (version is not null)
        {
            arguments["expectedVersion"] = version;
        }

        return arguments;
    }

    // A participant of the test's own, with names that are made up and do not hold the scene's token.
    private async Task<Added> AddAsync(bool archived = false)
    {
        var unique = Guid.NewGuid().ToString("N")[..12];
        var givenName = $"Zed{unique}";
        var familyName = $"Mott{unique}";
        var participant = await Flow.App.Services.AddParticipantAsync(
            Scene.Organisation, archived, givenName, familyName);
        return new(participant.Id, givenName, familyName, ToolScene.ExpectedVersion(participant.ETag));
    }

    // The participant as the row holds them, through the app's context under the first organisation's tenant.
    private async Task<Participant> StoredAsync(Guid participantId)
    {
        await using var scope = Flow.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Scene.Organisation.OrganisationId))
        {
            return await db.Participants.AsNoTracking().SingleAsync(p => p.Id == participantId, Ct);
        }
    }

    // The participant is in the status given, and, unless the call changed them, still has the version they were added
    // with.
    private async Task AssertStoredAsync(Added added, ParticipantStatus status, bool versionChanged = false)
    {
        var stored = await StoredAsync(added.Id);
        Assert.Equal(status, stored.Status);
        Assert.Equal(status == ParticipantStatus.Archived, stored.ArchivedAtUtc is not null);
        var version = ToolScene.ExpectedVersion(RowVersionToken.ETag(stored));
        Assert.Equal(!versionChanged, version == added.Version);
    }

    private Task<(JsonObject Result, AuditRow Row)> RawAsync(
        string token, JsonObject arguments, string capabilities, string? state = null, string? answer = null) =>
        ConfirmationCalls.RawAsync(Flow, Tool, token, arguments, capabilities, state, answer);

    // The first call of a form: the state that comes back with it.
    private Task<string> AskAsync(string token, Added added, string capabilities) =>
        ConfirmationCalls.AskAsync(Flow, Tool, token, Args(added), capabilities);

    private sealed record Added(Guid Id, string GivenName, string FamilyName, string Version);
}
