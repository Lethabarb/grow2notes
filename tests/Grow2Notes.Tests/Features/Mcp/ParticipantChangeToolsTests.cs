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
/// <c>add_participant</c> and <c>update_participant</c> (mcp-server.md §5.2, S07.03.01 and S07.03.02) through the real
/// <c>/mcp</c>, with a token from the real flow: what each returns and stores, the event or the one refusal row that
/// each call writes, and the order of the checks. The guards that every tool shares are in
/// <see cref="ToolMatrixTests"/>. The cases share the app of <see cref="ToolWorld"/>, so each adds the participant it
/// changes, with names that do not hold the scene's token, and never changes a row of the scene.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantChangeToolsTests(ToolWorld world) : IClassFixture<ToolWorld>
{
    private const string Add = "add_participant";
    private const string Update = "update_participant";

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

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static string Sent(string value) => value.Replace("{BEL}", "\u0007").Replace("{RLO}", "\u202E");

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
    private async Task<Participant> StoredAsync(Guid participantId)
    {
        await using var scope = Flow.App.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(Scene.Organisation.OrganisationId))
        {
            return await db.Participants.AsNoTracking().SingleAsync(p => p.Id == participantId, Ct);
        }
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
