using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Grow2Notes.Web.Platform.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using static Grow2Notes.Tests.Features.Mcp.McpAuditRows;

namespace Grow2Notes.Tests.Features.Mcp;

/// <summary>
/// <c>add_participant</c>, <c>update_participant</c>, <c>add_goal</c>, <c>update_goal</c>, <c>reorder_goals</c>,
/// <c>archive_goal</c> and <c>restore_goal</c> (mcp-server.md §5.2, S07.03.01 to S07.03.03) through the real
/// <c>/mcp</c>, with a token from the real flow: what each returns and stores, the event or the one refusal row that
/// each call writes, and the order of the checks. The guards that every tool shares, including who may call each and
/// that another organisation's rows are not found, are in <see cref="ToolMatrixTests"/>. The cases share the app of
/// <see cref="ToolWorld"/>, so each adds the participant and the goals it changes, with names that do not hold the
/// scene's token, and never changes a row of the scene.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantChangeToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Add = "add_participant";
    private const string Update = "update_participant";
    private const string AddGoal = "add_goal";
    private const string UpdateGoal = "update_goal";
    private const string Reorder = "reorder_goals";
    private const string ArchiveGoal = "archive_goal";
    private const string RestoreGoal = "restore_goal";

    private const string HelpText =
        "Changes apply to notes started from now on. Notes already started or submitted keep the wording they were " +
        "written with.";

    private const string GoalNotFound = "No goal has that ID in this organisation. Find it with get_participant.";

    private const string GoalChanged =
        "This goal was changed since you looked it up. Look it up again with get_participant, then retry.";

    private const string OrderMismatch =
        "goalIds must hold every active goal of the participant exactly once. " +
        "Look them up again with get_participant, then retry.";

    // A mark in a refused value, which no refusal may repeat.
    private const string Mark = "Zq9x";

    private const string NoFieldToChange = "Send at least one of givenName, familyName and dateOfBirth.";

    // The field, the value (with {BEL} and {RLO} for a control and a bidirectional character, which a test's name
    // cannot hold), the app's message for it, and the text of the value that the result must not hold.
    public static TheoryData<string, string, string, string> Refusals { get; } = new()
    {
        { "givenName", Mark + new string('a', 97), "Given name must be 100 characters or less", Mark },
        { "givenName", Mark + "{BEL}", "Given name has a control or formatting character", Mark },
        { "familyName", Mark + new string('a', 97), "Family name must be 100 characters or less", Mark },
        { "familyName", Mark + "{RLO}", "Family name has a control or formatting character", Mark },
        { "givenName", "", "Enter the given name", "" },
        { "familyName", "", "Enter the family name", "" },
        { "dateOfBirth", " ", "Enter the date of birth", "" },
        { "dateOfBirth", "2012-5-3", "Date of birth must be a real date", "2012" },
        { "dateOfBirth", " 2012-05-03", "Date of birth must be a real date", "2012" },
        { "dateOfBirth", "2012/05/03", "Date of birth must be a real date", "2012" },
        { "dateOfBirth", "2999-01-01", "Date of birth must be in the past", "2999" },
    };

    // The same for a goal's text: the value, the app's message, and the text that the result must not hold.
    public static TheoryData<string, string, string> GoalTextRefusals { get; } = new()
    {
        { Mark + new string('a', 197), "Goal must be 200 characters or less", Mark },
        { "Goal{LF}" + Mark, "Goal has a control or formatting character", Mark },
        { Mark + "{BEL}", "Goal has a control or formatting character", Mark },
        { "", "Enter the goal", "" },
    };

    // The lists of goalIds that are not the participant's active goals, each once.
    public static TheoryData<string> WrongOrders { get; } =
        ["a goal missing", "an unknown goal", "a goal repeated", "an archived goal", "an ID that is not a GUID"];

    // The tool and the version that it sends in a call that changes nothing.
    public static TheoryData<string, string> GoalCallsThatChangeNothing { get; } = new()
    {
        { UpdateGoal, "stale" },
        { UpdateGoal, "missing" },
        { ArchiveGoal, "stale" },
        { ArchiveGoal, "missing" },
        { RestoreGoal, "stale" },
        { RestoreGoal, "missing" },
    };

    public static TheoryData<string> Versions { get; } = ["current", "stale", "missing"];

    public static TheoryData<string> WrongVersions { get; } = ["stale", "missing"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AssistantFlow Flow => world.Flow;

    private ToolScene Scene => world.Scene;

    private McpClient Client => world.ClientOf(ToolCaller.Manager);

    [Fact]
    public async Task A_participant_is_added_active_with_trimmed_names_and_the_version_that_is_stored()
    {
        var unique = Unique();

        var (result, row) = await CallAsync(Add, NewParticipant($"  Ann{unique} ", $"Rowe{unique}  ", "1987-04-09"));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        var id = content.GetProperty("participantId").GetGuid();
        Assert.Equal($"Ann{unique}", content.GetProperty("givenName").GetString());
        Assert.Equal($"Rowe{unique}", content.GetProperty("familyName").GetString());
        Assert.Equal("active", content.GetProperty("status").GetString());
        var stored = await StoredAsync(id);
        Assert.Equal(
            ($"Ann{unique}", $"Rowe{unique}", new DateOnly(1987, 4, 9), ParticipantStatus.Active),
            (stored.GivenName, stored.FamilyName, stored.DateOfBirth, stored.Status));
        Assert.Equal(ToolScene.ExpectedVersion(RowVersionToken.ETag(stored)), ExpectedVersionOf(content));
        AssertChanged(row, AuditEventTypes.Participant.Created, Add, Scene.Organisation.ManagerId);
        Assert.Equal(id, row.ParticipantId);
    }

    [Fact]
    public async Task A_zero_width_joiner_and_non_joiner_in_a_name_are_kept()
    {
        var unique = Unique();
        var given = $"Ann\u200D{unique}";
        var family = $"Rowe\u200C{unique}";

        var (result, _) = await CallAsync(Add, NewParticipant(given, family, "1987-04-09"));

        var content = Content(result);
        Assert.Equal(given, content.GetProperty("givenName").GetString());
        Assert.Equal(family, content.GetProperty("familyName").GetString());
        var stored = await StoredAsync(content.GetProperty("participantId").GetGuid());
        Assert.Equal((given, family), (stored.GivenName, stored.FamilyName));
    }

    [Fact]
    public async Task No_result_has_a_date_of_birth_in_any_form()
    {
        var unique = Unique();
        var added = await CallAsync(Add, NewParticipant($"Ann{unique}", $"Rowe{unique}", "1987-04-09"));
        var participant = await Scene.AddParticipantAsync();
        var updated = await CallAsync(
            Update, With(With(Change(participant, "current"), "dateOfBirth", "1987-04-09"), "givenName", "Bea"));
        string[] forms = ["1987-04-09", "09/04/1987", "9/4/1987", "04/09/1987", "4/9/1987"];

        foreach (var (result, _) in new[] { added, updated })
        {
            Assert.NotEqual(true, result.IsError);
            Assert.DoesNotContain(
                PropertyNames(Content(result)), name => name.Contains("birth", StringComparison.OrdinalIgnoreCase));
            Assert.All(forms, form => Assert.DoesNotContain(form, Body(result), StringComparison.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task An_added_participant_with_a_refused_field_gets_the_apps_words_and_no_echo(
        string field, string value, string message, string echo)
    {
        var unique = Unique();
        var arguments = With(NewParticipant($"Ann{unique}", $"Rowe{unique}", "1987-04-09"), field, Sent(value));

        var (result, row) = await CallAsync(Add, arguments);

        AssertValidationFailed(result, field, message);
        AssertNoEcho(result, echo);
        AssertRefused(row, Add, "validation.failed");
        Assert.Empty(await FoundAsync($"Rowe{unique}"));
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_corrected_participant_with_a_refused_field_gets_the_apps_words_and_is_not_changed(
        string field, string value, string message, string echo)
    {
        var participant = await Scene.AddParticipantAsync();
        var stored = await StoredAsync(participant.Id);

        var (result, row) = await CallAsync(Update, With(Change(participant, "current"), field, Sent(value)));

        AssertValidationFailed(result, field, message);
        AssertNoEcho(result, echo);
        AssertRefused(row, Update, "validation.failed");
        await AssertUnchangedAsync(participant.Id, stored);
    }

    [Fact]
    public async Task A_correction_of_the_given_name_alone_keeps_the_rest_and_records_only_that_name()
    {
        var participant = await Scene.AddParticipantAsync();
        var before = await StoredAsync(participant.Id);
        var renamed = $"Eve{Unique()}";

        var (result, row) = await CallAsync(Update, With(Change(participant, "current"), "givenName", renamed));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(renamed, content.GetProperty("givenName").GetString());
        Assert.Equal(before.FamilyName, content.GetProperty("familyName").GetString());
        Assert.Equal("active", content.GetProperty("status").GetString());
        Assert.True(content.GetProperty("changed").GetBoolean());
        var after = await StoredAsync(participant.Id);
        Assert.Equal(
            (renamed, before.FamilyName, before.DateOfBirth), (after.GivenName, after.FamilyName, after.DateOfBirth));
        Assert.Equal(ToolScene.ExpectedVersion(RowVersionToken.ETag(after)), ExpectedVersionOf(content));
        Assert.NotEqual(ToolScene.ExpectedVersion(participant.ETag), ExpectedVersionOf(content));
        AssertChanged(row, AuditEventTypes.Participant.Updated, Update, Scene.Organisation.ManagerId);
        Assert.Equal(participant.Id, row.ParticipantId);
        var details = JsonNode.Parse(Assert.IsType<string>(row.Details))!;
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["givenName"] = before.GivenName }, details["old"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["givenName"] = renamed }, details["new"]));
    }

    [Fact]
    public async Task A_correction_with_none_of_the_three_fields_is_refused_and_changes_nothing()
    {
        var participant = await Scene.AddParticipantAsync();
        var stored = await StoredAsync(participant.Id);

        var (result, row) = await CallAsync(Update, Change(participant, "current"));

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("validation.failed", error.GetProperty("code").GetString());
        Assert.Equal(NoFieldToChange, error.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, error.GetProperty("fields").ValueKind);
        AssertRefused(row, Update, "validation.failed");
        await AssertUnchangedAsync(participant.Id, stored);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task An_archived_participant_is_refused_whatever_version_is_sent(string version)
    {
        var archived = Scene.Archived;
        var arguments = new JsonObject
        {
            ["participantId"] = archived.Id.ToString(),
            ["givenName"] = $"Rename{Unique()}",
        };
        VersionIs(arguments, version, archived.ETag);
        var before = await StoredAsync(archived.Id);

        var (result, row) = await CallAsync(Update, arguments);

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("participant.archived", error.GetProperty("code").GetString());
        Assert.Equal(
            "This participant is archived. Restore the participant first.",
            error.GetProperty("message").GetString());
        AssertRefused(row, Update, "participant.archived");
        await AssertUnchangedAsync(archived.Id, before);
    }

    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_wrong_version_is_refused_when_a_value_would_change_and_the_row_is_not_changed(string version)
    {
        var participant = await Scene.AddParticipantAsync();
        var stored = await StoredAsync(participant.Id);
        var arguments = With(Change(participant, version), "givenName", $"Eve{Unique()}");

        var (result, row) = await CallAsync(Update, arguments);

        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        var (code, message) = version == "stale"
            ? ("precondition.failed",
                "This participant was changed since you looked them up. Look them up again with get_participant, " +
                "then retry.")
            : ("precondition.required", "Send the expectedVersion that the look-up returned.");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
        AssertRefused(row, Update, code);
        await AssertUnchangedAsync(participant.Id, stored);
    }

    // Both orders (R13): the tool answers a call that changes nothing before it looks at the version, and the app
    // refuses the same stale version (412) or a missing one (428) at once.
    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_call_that_changes_nothing_answers_changed_false_whatever_the_version_but_the_app_refuses(
        string version)
    {
        var participant = await Scene.AddParticipantAsync();
        var renamed = $"Eve{Unique()}";
        var (first, _) = await CallAsync(Update, With(Change(participant, "current"), "givenName", renamed));
        var current = ExpectedVersionOf(Content(first));
        var stored = await StoredAsync(participant.Id);
        // The version the row had before the first call is stale now.
        var arguments = With(Change(participant, version == "stale" ? "current" : "missing"), "givenName", renamed);

        var (result, row) = await CallAsync(Update, arguments);

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("changed").GetBoolean());
        Assert.Equal(renamed, content.GetProperty("givenName").GetString());
        Assert.Equal(current, ExpectedVersionOf(content));
        await AssertUnchangedAsync(participant.Id, stored);
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Equal(participant.Id, row.ParticipantId);
        AssertRead(row, Update, participant.Id);

        using var browser = await Flow.SignInAsync(Scene.Organisation.ManagerId);
        using var response = await browser.PutWithTokenAsync(
            $"/api/admin/participants/{participant.Id}",
            await browser.FetchTokenAsync(),
            new
            {
                givenName = stored.GivenName,
                familyName = stored.FamilyName,
                dateOfBirth = stored.DateOfBirth.ToString("yyyy-MM-dd"),
            },
            version == "stale" ? participant.ETag : null);
        Assert.Equal(
            version == "stale" ? HttpStatusCode.PreconditionFailed : HttpStatusCode.PreconditionRequired,
            response.StatusCode);
        await AssertUnchangedAsync(participant.Id, stored);
    }

    [Fact]
    public async Task A_goal_is_added_after_the_highest_active_one_despite_a_gap_and_an_archived_goal()
    {
        var participant = await Scene.AddParticipantAsync();
        await Flow.App.Services.AddGoalsAsync(
            Scene.Organisation,
            participant.Id,
            new TestGoal("First", 0),
            new TestGoal("Second", 5),
            new TestGoal("Archived", 9, ArchivedAtUtc: TestGoals.AddedAtUtc));
        var text = $"Goal {Unique()}";

        var (result, row) = await CallAsync(AddGoal, AddGoalCall(participant.Id, $"  {text} "));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        var goalId = content.GetProperty("goalId").GetGuid();
        Assert.Equal(text, content.GetProperty("text").GetString());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        var stored = await StoredGoalAsync(goalId);
        Assert.Equal((text, 6, (DateTime?)null), (stored.Text, stored.SortOrder, stored.ArchivedAtUtc));
        Assert.Equal(ToolScene.ExpectedVersion(RowVersionToken.ETag(stored)), ExpectedVersionOf(content));
        AssertChanged(row, AuditEventTypes.Goal.Created, AddGoal, Scene.Organisation.ManagerId);
        Assert.Equal(participant.Id, row.ParticipantId);
    }

    [Theory]
    [MemberData(nameof(GoalTextRefusals))]
    public async Task An_added_goal_with_a_refused_text_gets_the_apps_words_and_no_echo(
        string value, string message, string echo)
    {
        var participant = await Scene.AddParticipantAsync();

        var (result, row) = await CallAsync(AddGoal, AddGoalCall(participant.Id, Sent(value)));

        AssertValidationFailed(result, "text", message);
        AssertNoEcho(result, echo);
        AssertRefused(row, AddGoal, "validation.failed");
        Assert.Empty(await GoalStatesAsync(participant.Id));
    }

    [Fact]
    public async Task A_goal_is_not_added_to_an_archived_participant()
    {
        var archived = Scene.ArchivedParticipant;
        var before = await GoalStatesAsync(archived.Id);

        var (result, row) = await CallAsync(AddGoal, AddGoalCall(archived.Id, $"Goal {Unique()}"));

        AssertError(result, "participant.archived", "This participant is archived. Restore the participant first.");
        AssertRefused(row, AddGoal, "participant.archived");
        Assert.Equal(before, await GoalStatesAsync(archived.Id));
    }

    [Fact]
    public async Task A_goal_is_reworded_and_the_stored_text_and_new_version_are_returned()
    {
        var participant = await Scene.AddParticipantAsync();
        var goal = await Scene.AddGoalAsync(participant.Id, archived: false);
        var text = $"Goal {Unique()}";

        var (result, row) = await CallAsync(UpdateGoal, GoalCall(goal, $" {text}  ", "current"));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.Equal(text, content.GetProperty("text").GetString());
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        var stored = await StoredGoalAsync(goal.Id);
        Assert.Equal(text, stored.Text);
        Assert.Equal(ToolScene.ExpectedVersion(RowVersionToken.ETag(stored)), ExpectedVersionOf(content));
        Assert.NotEqual(ToolScene.ExpectedVersion(goal.ETag), ExpectedVersionOf(content));
        AssertChanged(row, AuditEventTypes.Goal.Updated, UpdateGoal, Scene.Organisation.ManagerId);
        Assert.Equal(participant.Id, row.ParticipantId);
    }

    [Fact]
    public async Task An_archived_goal_of_an_active_participant_is_reworded()
    {
        var participant = await Scene.AddParticipantAsync();
        var goal = await Scene.AddGoalAsync(participant.Id, archived: true);
        var text = $"Goal {Unique()}";

        var (result, _) = await CallAsync(UpdateGoal, GoalCall(goal, text, "current"));

        Assert.NotEqual(true, result.IsError);
        Assert.True(Content(result).GetProperty("changed").GetBoolean());
        var stored = await StoredGoalAsync(goal.Id);
        Assert.Equal(text, stored.Text);
        Assert.NotNull(stored.ArchivedAtUtc);
    }

    [Theory]
    [MemberData(nameof(GoalTextRefusals))]
    public async Task A_reworded_goal_with_a_refused_text_gets_the_apps_words_and_is_not_changed(
        string value, string message, string echo)
    {
        var participant = await Scene.AddParticipantAsync();
        var goal = await Scene.AddGoalAsync(participant.Id, archived: false);
        var before = await GoalStateAsync(goal.Id);

        var (result, row) = await CallAsync(UpdateGoal, GoalCall(goal, Sent(value), "current"));

        AssertValidationFailed(result, "text", message);
        AssertNoEcho(result, echo);
        AssertRefused(row, UpdateGoal, "validation.failed");
        Assert.Equal(before, await GoalStateAsync(goal.Id));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public async Task A_goal_of_an_archived_participant_is_refused_whatever_version_is_sent(string version)
    {
        var goal = Scene.ArchivedGoal;
        var before = await GoalStateAsync(goal.Id);

        var (result, row) = await CallAsync(UpdateGoal, GoalCall(goal, $"Reword {Unique()}", version));

        AssertError(result, "participant.archived", "This participant is archived. Restore the participant first.");
        AssertRefused(row, UpdateGoal, "participant.archived");
        Assert.Equal(before, await GoalStateAsync(goal.Id));
    }

    [Theory]
    [MemberData(nameof(WrongVersions))]
    public async Task A_wrong_goal_version_is_refused_when_a_change_would_be_made_and_the_goal_is_not_changed(
        string version)
    {
        var participant = await Scene.AddParticipantAsync();
        var goal = await Scene.AddGoalAsync(participant.Id, archived: false);
        var before = await GoalStateAsync(goal.Id);

        var (result, row) = await CallAsync(UpdateGoal, GoalCall(goal, $"Goal {Unique()}", version));

        var (code, message) = version == "stale"
            ? ("precondition.failed", GoalChanged)
            : ("precondition.required", "Send the expectedVersion that the look-up returned.");
        AssertError(result, code, message);
        AssertRefused(row, UpdateGoal, code);
        Assert.Equal(before, await GoalStateAsync(goal.Id));
    }

    [Fact]
    public async Task A_goal_that_does_not_exist_is_not_found_in_the_words_of_the_tool()
    {
        var (result, row) = await CallAsync(
            UpdateGoal, new() { ["goalId"] = Guid.NewGuid().ToString(), ["text"] = "Goal x" });

        AssertError(result, "not_found", GoalNotFound);
        AssertRefused(row, UpdateGoal, "not_found");
    }

    [Fact]
    public async Task Goals_are_reordered_and_all_of_them_are_returned_with_their_current_versions()
    {
        var (participant, actives, archived) = await ParticipantWithGoalsAsync();
        Guid[] reversed = [.. actives.Select(goal => goal.Id).Reverse()];

        var (result, row) = await CallAsync(Reorder, ReorderCall(participant.Id, reversed));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        var goals = content.GetProperty("goals").EnumerateArray().ToList();
        Assert.Equal(
            [.. reversed, archived.Id], goals.Select(goal => goal.GetProperty("goalId").GetGuid()));
        Assert.Equal(
            [false, false, false, true], goals.Select(goal => goal.GetProperty("archived").GetBoolean()));
        var stored = await GoalStatesAsync(participant.Id);
        Assert.All(
            goals,
            goal => Assert.Equal(
                ToolScene.ExpectedVersion(stored[goal.GetProperty("goalId").GetGuid()].ETag),
                goal.GetProperty("expectedVersion").GetString()));
        Assert.Equal(
            reversed, stored.Where(goal => goal.Value.ArchivedAtUtc is null).OrderBy(goal => goal.Value.SortOrder)
                .Select(goal => goal.Key));
        AssertChanged(row, AuditEventTypes.Goal.Reordered, Reorder, Scene.Organisation.ManagerId);
        Assert.Equal(participant.Id, row.ParticipantId);
    }

    [Fact]
    public async Task The_order_that_is_stored_answers_changed_false_and_an_mcp_read_row_names_what_was_returned()
    {
        var (participant, actives, archived) = await ParticipantWithGoalsAsync();
        var before = await GoalStatesAsync(participant.Id);
        var ordered = actives.Select(goal => goal.Id);

        var (result, row) = await CallAsync(Reorder, ReorderCall(participant.Id, ordered));

        Assert.NotEqual(true, result.IsError);
        Assert.False(Content(result).GetProperty("changed").GetBoolean());
        Assert.Equal(before, await GoalStatesAsync(participant.Id));
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        Assert.Equal(participant.Id, row.ParticipantId);
        AssertRead(
            row,
            AssistantFlow.ClientId,
            Reorder,
            ("participantIds", [participant.Id]),
            ("goalIds", [.. actives.Select(goal => goal.Id), archived.Id]));
    }

    [Theory]
    [MemberData(nameof(WrongOrders))]
    public async Task A_list_that_is_not_the_active_goals_once_each_is_refused_and_nothing_is_changed(string wrong)
    {
        var (participant, actives, archived) = await ParticipantWithGoalsAsync();
        var before = await GoalStatesAsync(participant.Id);
        List<string> ids = [.. actives.Select(goal => goal.Id.ToString())];
        switch (wrong)
        {
            case "a goal missing":
                ids.RemoveAt(2);
                break;
            case "an unknown goal":
                ids.Add(Guid.NewGuid().ToString());
                break;
            case "a goal repeated":
                ids.Add(ids[0]);
                break;
            case "an archived goal":
                ids.Add(archived.Id.ToString());
                break;
            default:
                ids[2] = "not-a-guid";
                break;
        }

        var (result, row) = await CallAsync(Reorder, ReorderCallOf(participant.Id, ids));

        AssertError(result, "config.order_mismatch", OrderMismatch);
        AssertRefused(row, Reorder, "config.order_mismatch");
        Assert.Equal(before, await GoalStatesAsync(participant.Id));
    }

    [Fact]
    public async Task The_goals_of_an_archived_participant_are_refused_even_when_the_list_is_wrong()
    {
        var archived = Scene.ArchivedParticipant;
        var before = await GoalStatesAsync(archived.Id);

        var (result, row) = await CallAsync(Reorder, ReorderCall(archived.Id, [Guid.NewGuid()]));

        AssertError(result, "participant.archived", "This participant is archived. Restore the participant first.");
        AssertRefused(row, Reorder, "participant.archived");
        Assert.Equal(before, await GoalStatesAsync(archived.Id));
    }

    [Fact]
    public async Task A_goal_is_archived_on_the_first_call()
    {
        var participant = await Scene.AddParticipantAsync();
        var goal = await Scene.AddGoalAsync(participant.Id, archived: false);

        var (result, row) = await CallAsync(ArchiveGoal, GoalCall(goal, null, "current"));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        Assert.False(content.TryGetProperty("expectedVersion", out _));
        Assert.NotNull((await StoredGoalAsync(goal.Id)).ArchivedAtUtc);
        AssertChanged(row, AuditEventTypes.Goal.Archived, ArchiveGoal, Scene.Organisation.ManagerId);
        Assert.Equal(participant.Id, row.ParticipantId);
    }

    [Fact]
    public async Task A_goal_is_restored_on_the_first_call_and_goes_to_the_end()
    {
        var participant = await Scene.AddParticipantAsync();
        var active = await Scene.AddGoalsAsync(participant.Id, 2);
        var goal = await Scene.AddGoalAsync(participant.Id, archived: true);

        var (result, row) = await CallAsync(RestoreGoal, GoalCall(goal, null, "current"));

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.True(content.GetProperty("changed").GetBoolean());
        Assert.Equal(HelpText, content.GetProperty("helpText").GetString());
        Assert.False(content.TryGetProperty("expectedVersion", out _));
        var restored = await StoredGoalAsync(goal.Id);
        Assert.Null(restored.ArchivedAtUtc);
        var others = await GoalStatesAsync(participant.Id);
        Assert.All(
            others.Where(other => active.Any(a => a.Id == other.Key)),
            other => Assert.True(restored.SortOrder > other.Value.SortOrder));
        AssertChanged(row, AuditEventTypes.Goal.Restored, RestoreGoal, Scene.Organisation.ManagerId);
        Assert.Equal(participant.Id, row.ParticipantId);
    }

    [Theory]
    [InlineData(ArchiveGoal)]
    [InlineData(RestoreGoal)]
    public async Task A_goal_of_an_archived_participant_is_not_archived_or_restored(string tool)
    {
        var goal = Scene.ArchivedGoal;
        var before = await GoalStateAsync(goal.Id);

        var (result, row) = await CallAsync(tool, GoalCall(goal, null, "current"));

        AssertError(result, "participant.archived", "This participant is archived. Restore the participant first.");
        AssertRefused(row, tool, "participant.archived");
        Assert.Equal(before, await GoalStateAsync(goal.Id));
    }

    [Theory]
    [InlineData(ArchiveGoal)]
    [InlineData(RestoreGoal)]
    public async Task A_wrong_goal_version_is_refused_for_an_archive_or_a_restore_that_would_change_the_goal(
        string tool)
    {
        var participant = await Scene.AddParticipantAsync();
        var goal = await Scene.AddGoalAsync(participant.Id, archived: tool == RestoreGoal);
        var before = await GoalStateAsync(goal.Id);

        var (result, row) = await CallAsync(tool, GoalCall(goal, null, "stale"));

        AssertError(result, "precondition.failed", GoalChanged);
        AssertRefused(row, tool, "precondition.failed");
        Assert.Equal(before, await GoalStateAsync(goal.Id));
    }

    // Both orders (R13), for a goal: the tool answers a call that changes nothing before it looks at the version, and
    // the app refuses the same stale version (412) or a missing one (428) at once.
    [Theory]
    [MemberData(nameof(GoalCallsThatChangeNothing))]
    public async Task A_goal_call_that_changes_nothing_answers_changed_false_whatever_the_version_but_the_app_refuses(
        string tool, string version)
    {
        var participant = await Scene.AddParticipantAsync();
        var goal = await Scene.AddGoalAsync(participant.Id, archived: tool == ArchiveGoal);
        var reworded = $"Goal {Unique()}";
        // A change of the text, so that the version the goal was added with is stale, and the goal is in the state
        // that the tool is to find it in.
        var (first, _) = await CallAsync(UpdateGoal, GoalCall(goal, reworded, "current"));
        Assert.NotEqual(true, first.IsError);
        var before = await GoalStateAsync(goal.Id);
        var sent = version == "stale" ? "current" : "missing";
        var arguments = GoalCall(goal, tool == UpdateGoal ? reworded : null, sent);

        var (result, row) = await CallAsync(tool, arguments);

        Assert.NotEqual(true, result.IsError);
        var content = Content(result);
        Assert.False(content.GetProperty("changed").GetBoolean());
        if (tool == UpdateGoal)
        {
            Assert.Equal(reworded, content.GetProperty("text").GetString());
            Assert.Equal(ToolScene.ExpectedVersion(before.ETag), ExpectedVersionOf(content));
        }

        Assert.Equal(before, await GoalStateAsync(goal.Id));
        Assert.Equal(AuditEventTypes.Mcp.Read, row.EventType);
        AssertRead(row, AssistantFlow.ClientId, tool, ("goalIds", [goal.Id]));

        using var browser = await Flow.SignInAsync(Scene.Organisation.ManagerId);
        var token = await browser.FetchTokenAsync();
        var ifMatch = version == "stale" ? goal.ETag : null;
        var path = $"/api/admin/goals/{goal.Id}";
        using var response = tool switch
        {
            UpdateGoal => await browser.PutWithTokenAsync(path, token, new { text = reworded }, ifMatch),
            ArchiveGoal => await browser.PostWithTokenAsync(path + "/archive", token, ifMatch: ifMatch),
            _ => await browser.PostWithTokenAsync(path + "/restore", token, ifMatch: ifMatch),
        };
        Assert.Equal(
            version == "stale" ? HttpStatusCode.PreconditionFailed : HttpStatusCode.PreconditionRequired,
            response.StatusCode);
        Assert.Equal(before, await GoalStateAsync(goal.Id));
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static string Sent(string value) =>
        value.Replace("{BEL}", "\u0007").Replace("{RLO}", "\u202E").Replace("{LF}", "\n");

    private static JsonObject NewParticipant(string givenName, string familyName, string dateOfBirth) => new()
    {
        ["givenName"] = givenName,
        ["familyName"] = familyName,
        ["dateOfBirth"] = dateOfBirth,
    };

    // A correction of the participant that sends no field yet, with the version as asked: "current" (the one the
    // participant was added with), "stale" (one that no row has) or "missing".
    private static JsonObject Change(TestParticipant participant, string version)
    {
        var arguments = new JsonObject { ["participantId"] = participant.Id.ToString() };
        VersionIs(arguments, version, participant.ETag);
        return arguments;
    }

    private static void VersionIs(JsonObject arguments, string version, string currentETag)
    {
        if (version != "missing")
        {
            arguments["expectedVersion"] = version == "current"
                ? ToolScene.ExpectedVersion(currentETag)
                : "AAAAAAAAAAA=";
        }
    }

    private static JsonObject With(JsonObject arguments, string name, string value)
    {
        arguments[name] = value;
        return arguments;
    }

    private static JsonElement Content(CallToolResult result) => Assert.NotNull(result.StructuredContent);

    private static string ExpectedVersionOf(JsonElement content) => content.GetProperty("expectedVersion").GetString()!;

    private static string Body(CallToolResult result) =>
        JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions);

    private static void AssertNoEcho(CallToolResult result, string echo)
    {
        if (echo.Length > 0)
        {
            Assert.DoesNotContain(echo, Body(result), StringComparison.Ordinal);
        }
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .SelectMany(property => PropertyNames(property.Value).Prepend(property.Name)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(PropertyNames),
        _ => [],
    };

    private static void AssertValidationFailed(CallToolResult result, string field, string message)
    {
        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal("validation.failed", error.GetProperty("code").GetString());
        Assert.Equal("Some arguments are not valid. See fields.", error.GetProperty("message").GetString());
        Assert.Equal(
            [message], error.GetProperty("fields").GetProperty(field).EnumerateArray().Select(m => m.GetString()));
    }

    private async Task<(CallToolResult Result, AuditRow Row)> CallAsync(string tool, JsonObject arguments)
    {
        var lastEventId = await LastEventIdAsync(Flow);
        var result = await ToolScene.CallAsync(Client, tool, arguments);
        return (result, Assert.Single(await EventsAfterAsync(Flow, lastEventId)));
    }

    // The participants that find_participants gives for a family name, without an audit row of interest to the caller.
    private async Task<List<Guid>> FoundAsync(string familyName)
    {
        var (result, _) = await CallAsync("find_participants", new() { ["search"] = familyName });
        return
        [
            .. Content(result).GetProperty("participants").EnumerateArray()
                .Select(match => match.GetProperty("participantId").GetGuid()),
        ];
    }

    // The participant as the row holds them, through the app's context under the first organisation's tenant.
    private Task<Participant> StoredAsync(Guid participantId) =>
        ReadAsync(db => db.Participants.AsNoTracking().SingleAsync(p => p.Id == participantId, Ct));

    private Task<Goal> StoredGoalAsync(Guid goalId) =>
        ReadAsync(db => db.Goals.AsNoTracking().SingleAsync(g => g.Id == goalId, Ct));

    private async Task<GoalState> GoalStateAsync(Guid goalId) => StateOf(await StoredGoalAsync(goalId));

    // Every goal of the participant, by its ID, as the rows hold them.
    private async Task<Dictionary<Guid, GoalState>> GoalStatesAsync(Guid participantId) =>
        (await ReadAsync(db => db.Goals.AsNoTracking().Where(g => g.ParticipantId == participantId).ToListAsync(Ct)))
            .ToDictionary(goal => goal.Id, StateOf);

    private static GoalState StateOf(Goal goal) =>
        new(goal.Text, goal.SortOrder, goal.ArchivedAtUtc, RowVersionToken.ETag(goal));

    private async Task<T> ReadAsync<T>(Func<Grow2NotesDbContext, Task<T>> read)
    {
        await using var scope = Flow.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Scene.Organisation.OrganisationId))
        {
            return await read(db);
        }
    }

    // A participant of the test's own with three active goals, in order, and an archived one.
    private async Task<(TestParticipant Participant, IReadOnlyList<SceneGoal> Actives, SceneGoal Archived)>
        ParticipantWithGoalsAsync()
    {
        var participant = await Scene.AddParticipantAsync();
        var actives = await Scene.AddGoalsAsync(participant.Id, 3);
        return (participant, actives, await Scene.AddGoalAsync(participant.Id, archived: true));
    }

    private static JsonObject AddGoalCall(Guid participantId, string text) => new()
    {
        ["participantId"] = participantId.ToString(),
        ["text"] = text,
    };

    // A change to a goal with the text if one is given, and the version as asked: "current" (the one the goal was
    // added with), "stale" (one that no row has) or "missing".
    private static JsonObject GoalCall(SceneGoal goal, string? text, string version)
    {
        var arguments = new JsonObject { ["goalId"] = goal.Id.ToString() };
        if (text is not null)
        {
            arguments["text"] = text;
        }

        VersionIs(arguments, version, goal.ETag);
        return arguments;
    }

    private static JsonObject ReorderCall(Guid participantId, IEnumerable<Guid> goalIds) =>
        ReorderCallOf(participantId, goalIds.Select(id => id.ToString()));

    private static JsonObject ReorderCallOf(Guid participantId, IEnumerable<string> goalIds) => new()
    {
        ["participantId"] = participantId.ToString(),
        ["goalIds"] = new JsonArray([.. goalIds.Select(id => (JsonNode?)JsonValue.Create(id))]),
    };

    private static void AssertError(CallToolResult result, string code, string message)
    {
        Assert.True(result.IsError);
        var error = Content(result).GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
    }

    // The row has the values and the version that it had, and nothing was written about the participant but the
    // refusal or the read that the call itself wrote.
    private async Task AssertUnchangedAsync(Guid participantId, Participant before)
    {
        var stored = await StoredAsync(participantId);
        Assert.Equal(
            (before.GivenName, before.FamilyName, before.DateOfBirth, before.Status, RowVersionToken.ETag(before)),
            (stored.GivenName, stored.FamilyName, stored.DateOfBirth, stored.Status, RowVersionToken.ETag(stored)));
    }
}

/// <summary>What a goal's row holds that a call may change: its text, place and state, and its <c>ETag</c>.</summary>
internal sealed record GoalState(string Text, int SortOrder, DateTime? ArchivedAtUtc, string ETag);
