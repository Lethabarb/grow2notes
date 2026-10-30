using System.Data;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// <c>ops/audit-extract.sql</c>, which the operator runs to answer a manager's question from the audit log (A28, A92).
/// Each test runs it on a migrated database of its own, with a second organisation whose rows must never come back,
/// and with audit rows added through the app's context at an explicit time.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class AuditExtractScriptTests(SqlServerFixture sqlServer)
{
    private const string NoLongerOnRecord = "(no longer on record)";

    // From the range reserved for documentation (RFC 5737).
    private const string IpAddress = "203.0.113.7";

    [Fact]
    public async Task The_participant_question_returns_that_participants_rows_in_the_organisation_by_time_then_row()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractParticipant", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        var p = await db.Services.AddParticipantAsync(a, givenName: "Pat", familyName: "Person");
        var other = await db.Services.AddParticipantAsync(a, givenName: "Olive", familyName: "Other");
        var later = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 10, 0), participantId: p.Id);
        var earlier = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 0), participantId: p.Id);
        var sameTime = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 0), participantId: p.Id);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 30), participantId: other.Id);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 45));
        await AddEventAsync(db, b.OrganisationId, At(2026, 10, 7, 9, 15), participantId: p.Id);

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", participant: Text(p.Id), ct: ct);

        Assert.Equal([earlier, sameTime, later], RowIds(Assert.Single(results)));
    }

    [Fact]
    public async Task The_user_question_returns_what_that_user_did_in_the_organisation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractUser", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        var u = await db.Services.AddUserAsync(a, UserRole.Worker, "Una User");
        var someoneElse = await db.Services.AddUserAsync(a, UserRole.Worker, "Sam Someone");
        var second = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 10, 0), actorId: u);
        var first = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 0), actorId: u);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 10), actorId: someoneElse);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 20));
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 30), entityType: "User", entityId: u);
        await AddEventAsync(db, b.OrganisationId, At(2026, 10, 7, 9, 40), actorId: u);

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", user: Text(u), ct: ct);

        Assert.Equal([first, second], RowIds(Assert.Single(results)));
    }

    [Fact]
    public async Task With_any_participant_and_any_user_it_returns_every_row_of_the_organisation_in_the_range()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractEverything", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        var p = await db.Services.AddParticipantAsync(a);
        var third = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 11, 0), actorId: a.WorkerId);
        var first = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 0), participantId: p.Id);
        var second = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 10, 0));
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 8, 10, 0));
        await AddEventAsync(db, b.OrganisationId, At(2026, 10, 7, 10, 0));

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", ct: ct);

        Assert.Equal([first, second, third], RowIds(Assert.Single(results)));
    }

    [Fact]
    public async Task A_range_across_the_spring_clock_change_runs_from_Melbourne_midnight_to_Melbourne_midnight()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractSpring", ct);
        var a = await db.Services.AddOrganisationAsync();
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 2, 13, 59, 59, 999));
        var first = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 2, 14, 0));
        var last = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 4, 12, 59, 59, 999));
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 4, 13, 0));

        var results = await RunAsync(db, a.OrganisationId, "2026-10-03", "2026-10-04", ct: ct);

        var table = Assert.Single(results);
        Assert.Equal([first, last], RowIds(table));
        AssertShown(table.Rows[0], new(2026, 10, 3, 0, 0, 0, 0), offsetHours: 10);
        AssertShown(table.Rows[1], new(2026, 10, 4, 23, 59, 59, 999), offsetHours: 11);
    }

    [Fact]
    public async Task A_range_across_the_autumn_clock_change_runs_from_Melbourne_midnight_to_Melbourne_midnight()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractAutumn", ct);
        var a = await db.Services.AddOrganisationAsync();
        await AddEventAsync(db, a.OrganisationId, At(2026, 4, 4, 12, 59, 59, 999));
        var first = await AddEventAsync(db, a.OrganisationId, At(2026, 4, 4, 13, 0));
        var last = await AddEventAsync(db, a.OrganisationId, At(2026, 4, 5, 13, 59, 59, 999));
        await AddEventAsync(db, a.OrganisationId, At(2026, 4, 5, 14, 0));

        var results = await RunAsync(db, a.OrganisationId, "2026-04-05", "2026-04-05", ct: ct);

        var table = Assert.Single(results);
        Assert.Equal([first, last], RowIds(table));
        AssertShown(table.Rows[0], new(2026, 4, 5, 0, 0, 0, 0), offsetHours: 11);
        AssertShown(table.Rows[1], new(2026, 4, 5, 23, 59, 59, 999), offsetHours: 10);
    }

    [Fact]
    public async Task It_shows_names_beside_IDs_and_the_two_placeholders_where_there_is_no_name()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractNames", ct);
        var a = await db.Services.AddOrganisationAsync();
        var p = await db.Services.AddParticipantAsync(a, givenName: "Pat", familyName: "Person");
        var u = await db.Services.AddUserAsync(a, UserRole.Worker, "Una User");
        var gone = Guid.NewGuid();
        var goneParticipant = Guid.NewGuid();
        var entityNote = Guid.NewGuid();
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 1, 0), "participant.updated", actorId: u, participantId: p.Id,
            entityType: "Participant", entityId: p.Id);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 2, 0), "admin.signout_all", entityType: "Organisation");
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 3, 0), actorId: gone, participantId: goneParticipant,
            entityId: entityNote);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 4, 0), "user.updated", actorId: a.ManagerId, entityType: "User",
            entityId: u);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 5, 0), "user.updated", actorId: a.ManagerId, entityType: "User",
            entityId: gone);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 6, 0), actorId: a.ManagerId, entityType: "Note", entityId: u);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 7, 0), "x.empty", entityType: null, details: null);

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", ct: ct);

        var m = Text(a.ManagerId);
        Assert.Equal(
            [
                $"participant.updated|Una User|{Text(u)}|Pat Person|{Text(p.Id)}|Participant|{Text(p.Id)}|",
                "admin.signout_all|Operator command||||Organisation||",
                $"note.submitted|{NoLongerOnRecord}|{Text(gone)}|{NoLongerOnRecord}|{Text(goneParticipant)}|Note|" +
                    $"{Text(entityNote)}|",
                $"user.updated|Manager|{m}|||User|{Text(u)}|Una User",
                $"user.updated|Manager|{m}|||User|{Text(gone)}|{NoLongerOnRecord}",
                $"note.submitted|Manager|{m}|||Note|{Text(u)}|",
                "x.empty|Operator command||||||",
            ],
            Rows(Assert.Single(results), "Event", "By", "By ID", "Participant", "Participant ID", "Entity", "Entity ID",
                "Entity name"));
    }

    [Fact]
    public async Task It_returns_one_result_set_of_these_columns_with_Details_as_stored_and_without_the_IP_address()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractColumns", ct);
        var a = await db.Services.AddOrganisationAsync();
        const string details = """{"givenName":"Pat","goal":"Say \"hello\"\tdaily","version":2}""";
        var id = await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 0), details: details);

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", ct: ct);

        var table = Assert.Single(results);
        Assert.Equal(
            [
                "Time (Melbourne)", "Event", "By", "By ID", "Participant", "Participant ID", "Entity", "Entity ID",
                "Entity name", "Details", "Row",
            ],
            ColumnNames(table));
        var row = Assert.Single(table.Rows.Cast<DataRow>());
        Assert.Equal(details, (string)row["Details"]);
        Assert.Equal(id, (long)row["Row"]);
        Assert.DoesNotContain(
            row.ItemArray, cell => cell?.ToString()?.Contains(IpAddress, StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Two_identical_rows_one_millisecond_apart_both_come_back()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractDuplicates", ct);
        var a = await db.Services.AddOrganisationAsync();
        var first = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 9, 0, 0, 0), actorId: a.WorkerId, details: """{"version":1}""");
        var second = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 9, 0, 0, 1), actorId: a.WorkerId, details: """{"version":1}""");

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", ct: ct);

        Assert.Equal([first, second], RowIds(Assert.Single(results)));
    }

    [Fact]
    public async Task It_refuses_what_it_cannot_run_with_error_50000()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractRefusals", ct);
        var a = await db.Services.AddOrganisationAsync();
        var organisation = Text(a.OrganisationId);
        (string Why, string Org, string FromDate, string ToDate, string Participant, string User)[] cases =
        [
            ("an unknown organisation", Text(Guid.NewGuid()), "2026-10-01", "2026-10-02", "any", "any"),
            ("an organisation that is not an ID", "not-a-guid", "2026-10-01", "2026-10-02", "any", "any"),
            ("an empty organisation", "", "2026-10-01", "2026-10-02", "any", "any"),
            ("From after To", organisation, "2026-10-05", "2026-10-04", "any", "any"),
            ("a From that is not yyyy-mm-dd", organisation, "2026-1-3", "2026-10-04", "any", "any"),
            ("a To that is not a date", organisation, "2026-10-01", "tomorrow", "any", "any"),
            ("a date that does not exist", organisation, "2026-02-30", "2026-03-01", "any", "any"),
            ("an empty From", organisation, "", "2026-10-04", "any", "any"),
            ("a Participant that is not an ID or any", organisation, "2026-10-01", "2026-10-02", "everyone", "any"),
            ("a User that is not an ID or any", organisation, "2026-10-01", "2026-10-02", "any", "everyone"),
            ("an empty Participant", organisation, "2026-10-01", "2026-10-02", "", "any"),
            ("an empty User", organisation, "2026-10-01", "2026-10-02", "any", ""),
        ];

        foreach (var (why, org, fromDate, toDate, participant, user) in cases)
        {
            var refused = await Assert.ThrowsAsync<SqlException>(() =>
                RunWithTextAsync(db, org, fromDate, toDate, participant, user, "any", ct));
            Assert.True(refused.Number == 50000, $"{why} was not refused by the script: {refused.Message}");
        }
    }

    [Theory]
    [InlineData("everything")]
    [InlineData("")]
    public async Task A_Via_that_is_neither_mcp_nor_any_is_refused_with_its_message(string via)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractViaRefusal", ct);
        var a = await db.Services.AddOrganisationAsync();

        var refused = await Assert.ThrowsAsync<SqlException>(() =>
            RunAsync(db, a.OrganisationId, "2026-10-01", "2026-10-02", via: via, ct: ct));

        Assert.Equal(50000, refused.Number);
        Assert.Equal("Via is neither mcp nor any.", refused.Message);
    }

    [Fact]
    public async Task Via_mcp_keeps_what_an_assistant_did_and_the_mcp_events_and_leaves_out_the_rest()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractViaMcp", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        const string client = "Example assistant";
        var appEvent = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 1, 0), "user.invited", actorId: a.ManagerId,
            details: $$"""{"new":{"email":"x"},"via":"mcp","client":"{{client}}","tool":"invite_user"}""");
        var read = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 2, 0), "mcp.read", actorId: a.ManagerId,
            details: $$"""{"via":"mcp","client":"{{client}}","tool":"list_users","count":3}""");
        var refusal = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 3, 0), "mcp.refused", actorId: a.ManagerId,
            details: $$"""{"via":"mcp","client":"{{client}}","tool":"invite_user","code":"rate_limited"}""");
        var authorised = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 4, 0), "mcp.authorised", actorId: a.ManagerId,
            details: $$"""{"client":"{{client}}","scope":"mcp"}""");
        var notAuthorised = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 5, 0), "mcp.authorisation_refused", actorId: a.ManagerId,
            details: $$"""{"client":"{{client}}","why":"declined"}""");
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 6, 0), "user.invited", actorId: a.ManagerId,
            details: """{"new":{"email":"y"}}""");
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 7, 0), "user.invited", actorId: a.ManagerId,
            details: """{"new":{"email":"z"},"via":"cookie"}""");
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 8, 0), "admin.mcp_client_added", entityType: "McpClient",
            details: $$"""{"client":"{{client}}"}""");
        await AddEventAsync(
            db, b.OrganisationId, At(2026, 10, 7, 9, 0), "mcp.read", details: """{"via":"mcp","client":"c"}""");

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", via: "mcp", ct: ct);

        var table = Assert.Single(results);
        Assert.Equal([appEvent, read, refusal, authorised, notAuthorised], RowIds(table));
        Assert.All(
            table.Rows.Cast<DataRow>(),
            row => Assert.Contains(client, (string)row["Details"], StringComparison.Ordinal));
    }

    [Fact]
    public async Task Via_mcp_combines_with_Participant_and_User_and_keeps_the_columns()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractViaCombined", ct);
        var a = await db.Services.AddOrganisationAsync();
        const string viaMcp = """{"via":"mcp","client":"Example assistant","tool":"get_participant"}""";
        var wanted = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 1, 0), "mcp.read", actorId: a.ManagerId,
            participantId: a.ParticipantId, details: viaMcp);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 2, 0), "mcp.read", actorId: a.WorkerId,
            participantId: a.ParticipantId, details: viaMcp);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 3, 0), "mcp.read", actorId: a.ManagerId, details: viaMcp);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 4, 0), actorId: a.ManagerId, participantId: a.ParticipantId);

        var results = await RunAsync(
            db, a.OrganisationId, "2026-10-07", "2026-10-07", participant: Text(a.ParticipantId),
            user: Text(a.ManagerId), via: "mcp", ct: ct);

        var table = Assert.Single(results);
        Assert.Equal([wanted], RowIds(table));
        Assert.Equal(
            [
                "Time (Melbourne)", "Event", "By", "By ID", "Participant", "Participant ID", "Entity", "Entity ID",
                "Entity name", "Details", "Row",
            ],
            ColumnNames(table));
    }

    [Fact]
    public async Task Via_mcp_is_not_broken_by_a_row_with_no_Details_or_Details_that_are_not_JSON()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "AuditExtractViaNotJson", ct);
        var a = await db.Services.AddOrganisationAsync();
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 1, 0), details: null);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 2, 0), details: "not json at all");
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 3, 0), details: """["via","mcp"]""");
        var refusal = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 4, 0), "mcp.refused", details: "not json at all");
        var viaMcp = await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 7, 5, 0), details: """{"via":"mcp"}""");

        var results = await RunAsync(db, a.OrganisationId, "2026-10-07", "2026-10-07", via: "mcp", ct: ct);

        Assert.Equal([refusal, viaMcp], RowIds(Assert.Single(results)));
    }

    private static DateTime At(int year, int month, int day, int hour, int minute, int second = 0, int millis = 0) =>
        new(year, month, day, hour, minute, second, millis, DateTimeKind.Utc);

    // SQL Server prints an ID in capitals.
    private static string Text(Guid id) => id.ToString().ToUpperInvariant();

    private static string[] ColumnNames(DataTable table) =>
        [.. table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)];

    private static long[] RowIds(DataTable table) =>
        [.. table.Rows.Cast<DataRow>().Select(row => (long)row["Row"])];

    private static string[] Rows(DataTable table, params string[] columns) =>
        [.. table.Rows.Cast<DataRow>().Select(row => string.Join('|', columns.Select(column => row[column])))];

    // DateTimeOffset compares instants, so the offset the operator reads is asserted on its own.
    private static void AssertShown(DataRow row, DateTime local, int offsetHours)
    {
        var time = Assert.IsType<DateTimeOffset>(row["Time (Melbourne)"]);
        Assert.Equal(local, time.DateTime);
        Assert.Equal(TimeSpan.FromHours(offsetHours), time.Offset);
    }

    private static Task<IReadOnlyList<DataTable>> RunAsync(
        OpsDatabase db, Guid organisationId, string fromDate, string toDate, string participant = "any",
        string user = "any", string via = "any", CancellationToken ct = default) =>
        RunWithTextAsync(db, Text(organisationId), fromDate, toDate, participant, user, via, ct);

    private static Task<IReadOnlyList<DataTable>> RunWithTextAsync(
        OpsDatabase db, string organisation, string fromDate, string toDate, string participant, string user,
        string via, CancellationToken ct) =>
        OpsScripts.RunAsync(
            db.ConnectionString,
            "ops/audit-extract.sql",
            new Dictionary<string, string>
            {
                ["Organisation"] = organisation,
                ["From"] = fromDate,
                ["To"] = toDate,
                ["Participant"] = participant,
                ["User"] = user,
                ["Via"] = via,
            },
            ct);

    // Added through the app's context under the organisation's tenant, as the app adds one, but at a time the test
    // chooses. The table has no foreign key, so an ID with no row is allowed, as after a destruction.
    private static async Task<long> AddEventAsync(
        OpsDatabase db,
        Guid organisationId,
        DateTime occurredAtUtc,
        string eventType = "note.submitted",
        Guid? actorId = null,
        Guid? participantId = null,
        string? entityType = "Note",
        Guid? entityId = null,
        string? details = """{"version":1}""")
    {
        await using var scope = db.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var auditEvent = new AuditEvent
        {
            OrganisationId = organisationId,
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = actorId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            ParticipantId = participantId,
            Details = details,
            IpAddress = IpAddress,
        };
        context.AuditEvents.Add(auditEvent);
        using (tenant.Use(organisationId))
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return auditEvent.Id;
    }
}
