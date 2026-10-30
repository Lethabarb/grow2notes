using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// <c>ops/mcp-monthly.sql</c>, which the operator runs each month for the provider's AI-assistant figures and the list
/// of the invites an assistant made (design.md §10.9, mcp-server.md §11.5). Each test runs it on a migrated database
/// of its own, with a second organisation whose rows must never be counted or listed, and with audit rows added through
/// the app's context at an explicit time.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class McpMonthlyScriptTests(SqlServerFixture sqlServer)
{
    private const string NoLongerOnRecord = "(no longer on record)";

    private const string Assistant = "Example assistant";

    // From the range reserved for documentation (RFC 5737).
    private const string IpAddress = "203.0.113.7";

    private const string MadeByAssistant = $$"""{"via":"mcp","client":"{{Assistant}}","tool":"list_users"}""";

    private static readonly JsonSerializerOptions WithoutNulls =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Fact]
    public async Task It_counts_the_months_calls_in_Melbourne_time_from_midnight_to_midnight_across_the_spring_change()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "McpMonthlySpring", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        // October 2026 starts at +10 (1 October 00:00 is 30 September 14:00 UTC) and ends at +11 (1 November 00:00 is
        // 31 October 13:00 UTC), because the clocks go forward on 4 October.
        await AddEventAsync(db, a.OrganisationId, At(2026, 9, 30, 13, 59, 59, 999), "mcp.read", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 9, 30, 14, 0), "mcp.read", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 1, 0), "mcp.refused", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 2, 0), "mcp.refused", "not json at all");
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 7, 3, 0), "user.deactivated", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 31, 12, 59, 59, 999), "mcp.read", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 10, 31, 13, 0), "mcp.read", MadeByAssistant);
        await AddLeftOutAsync(db, a.OrganisationId, b.OrganisationId, At(2026, 10, 7, 4, 0));

        var results = await RunAsync(db, Text(a.OrganisationId), "2026-10", ct);

        AssertFigures(results, calls: 5, refused: 2);
    }

    [Fact]
    public async Task It_counts_the_months_calls_in_Melbourne_time_from_midnight_to_midnight_across_the_autumn_change()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "McpMonthlyAutumn", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        // April 2026 starts at +11 (1 April 00:00 is 31 March 13:00 UTC) and ends at +10 (1 May 00:00 is 30 April
        // 14:00 UTC), because the clocks go back on 5 April.
        await AddEventAsync(db, a.OrganisationId, At(2026, 3, 31, 12, 59, 59, 999), "mcp.read", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 3, 31, 13, 0), "mcp.read", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 4, 15, 1, 0), "mcp.refused", MadeByAssistant);
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 4, 30, 13, 59, 59, 999), "user.deactivated", MadeByAssistant);
        await AddEventAsync(db, a.OrganisationId, At(2026, 4, 30, 14, 0), "mcp.read", MadeByAssistant);
        await AddLeftOutAsync(db, a.OrganisationId, b.OrganisationId, At(2026, 4, 15, 4, 0));

        var results = await RunAsync(db, Text(a.OrganisationId), "2026-04", ct);

        AssertFigures(results, calls: 3, refused: 1);
    }

    [Fact]
    public async Task A_month_with_no_calls_gives_one_row_of_zeros_and_an_empty_invite_list()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "McpMonthlyEmpty", ct);
        var a = await db.Services.AddOrganisationAsync();

        var results = await RunAsync(db, Text(a.OrganisationId), "2026-10", ct);

        AssertFigures(results, calls: 0, refused: 0);
        Assert.Empty(results[1].Rows);
    }

    [Fact]
    public async Task The_invite_list_shows_each_assistant_invite_of_the_month_and_marks_Support()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "McpMonthlyInvites", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        var gone = Guid.NewGuid();
        var byManager = await AddInviteAsync(db, a.OrganisationId, At(2026, 10, 7, 9, 0), a.ManagerId, "Wanda Worker");
        var bySupport = await AddInviteAsync(
            db, a.OrganisationId, At(2026, 10, 8, 9, 0), a.SupportId, "Mia Manager", role: "Manager",
            reason: "Covering a vacancy");
        var byGone = await AddInviteAsync(db, a.OrganisationId, At(2026, 10, 9, 9, 0), gone, "Gus Gone");
        // None of these is in the list: an invite made in the browser, one made by an assistant in the next month, one
        // of the other organisation, and an event of an assistant that is not an invite.
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 10, 9, 0), "user.invited",
            """{"new":{"displayName":"Cora Cookie","email":"cora@example.org","role":"Worker"}}""",
            actorId: a.ManagerId);
        await AddInviteAsync(db, a.OrganisationId, At(2026, 10, 31, 13, 0), a.ManagerId, "Nora November");
        await AddInviteAsync(db, b.OrganisationId, At(2026, 10, 11, 9, 0), b.ManagerId, "Olga Other");
        await AddEventAsync(
            db, a.OrganisationId, At(2026, 10, 12, 9, 0), "user.deactivated", MadeByAssistant, actorId: a.ManagerId);

        var results = await RunAsync(db, Text(a.OrganisationId), "2026-10", ct);

        Assert.Equal(2, results.Count);
        var invites = results[1];
        Assert.Equal(
            [
                "Time (Melbourne)", "Invited", "Email", "Role", "Reason", "Invited by", "Support", "Assistant", "Row",
            ],
            ColumnNames(invites));
        Assert.Equal(
            [
                $"Wanda Worker|wanda.worker@example.org|Worker||Manager||{Assistant}|{byManager}",
                $"Mia Manager|mia.manager@example.org|Manager|Covering a vacancy|Support|Yes|{Assistant}|{bySupport}",
                $"Gus Gone|gus.gone@example.org|Worker||{NoLongerOnRecord}||{Assistant}|{byGone}",
            ],
            Rows(invites, "Invited", "Email", "Role", "Reason", "Invited by", "Support", "Assistant", "Row"));
        AssertShown(invites.Rows[0], new(2026, 10, 7, 20, 0, 0, 0), offsetHours: 11);
    }

    [Fact]
    public async Task It_refuses_what_it_cannot_run_with_error_50000()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "McpMonthlyRefusals", ct);
        var a = await db.Services.AddOrganisationAsync();
        var organisation = Text(a.OrganisationId);
        (string Why, string Org, string Month)[] cases =
        [
            ("an unknown organisation", Text(Guid.NewGuid()), "2026-10"),
            ("an organisation that is not an ID", "not-a-guid", "2026-10"),
            ("an empty organisation", "", "2026-10"),
            ("a month with one digit", organisation, "2026-1"),
            ("a month that is a date", organisation, "2026-10-01"),
            ("a month that is not a number", organisation, "October"),
            ("a month 13", organisation, "2026-13"),
            ("a month 00", organisation, "2026-00"),
            ("an empty month", organisation, ""),
        ];

        foreach (var (why, org, month) in cases)
        {
            var refused = await Assert.ThrowsAsync<SqlException>(() => RunAsync(db, org, month, ct));
            Assert.True(refused.Number == 50000, $"{why} was not refused by the script: {refused.Message}");
        }
    }

    private static DateTime At(int year, int month, int day, int hour, int minute, int second = 0, int millis = 0) =>
        new(year, month, day, hour, minute, second, millis, DateTimeKind.Utc);

    // SQL Server prints an ID in capitals.
    private static string Text(Guid id) => id.ToString().ToUpperInvariant();

    private static string[] ColumnNames(DataTable table) =>
        [.. table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)];

    private static string[] Rows(DataTable table, params string[] columns) =>
        [.. table.Rows.Cast<DataRow>().Select(row => string.Join('|', columns.Select(column => row[column])))];

    // DateTimeOffset compares instants, so the offset the operator reads is asserted on its own.
    private static void AssertShown(DataRow row, DateTime local, int offsetHours)
    {
        var time = Assert.IsType<DateTimeOffset>(row["Time (Melbourne)"]);
        Assert.Equal(local, time.DateTime);
        Assert.Equal(TimeSpan.FromHours(offsetHours), time.Offset);
    }

    private static void AssertFigures(IReadOnlyList<DataTable> results, int calls, int refused)
    {
        Assert.Equal(2, results.Count);
        var figures = results[0];
        Assert.Equal(["MCP calls", "Refused"], ColumnNames(figures));
        var row = Assert.Single(figures.Rows.Cast<DataRow>());
        Assert.Equal(calls, (int)row["MCP calls"]);
        Assert.Equal(refused, (int)row["Refused"]);
    }

    private static Task<IReadOnlyList<DataTable>> RunAsync(
        OpsDatabase db, string organisation, string month, CancellationToken ct) =>
        OpsScripts.RunAsync(
            db.ConnectionString,
            "ops/mcp-monthly.sql",
            new Dictionary<string, string> { ["Organisation"] = organisation, ["Month"] = month },
            ct);

    // What the figures must not count: the app's own events, the browser's answers to a consent screen, an operator
    // command, an event made in the browser, and the other organisation's assistant events.
    private static async Task AddLeftOutAsync(OpsDatabase db, Guid organisationId, Guid otherId, DateTime at)
    {
        await AddEventAsync(db, organisationId, at, "note.submitted", """{"version":1}""");
        await AddEventAsync(db, organisationId, at, "mcp.authorised", $$"""{"client":"{{Assistant}}","scope":"mcp"}""");
        await AddEventAsync(
            db, organisationId, at, "mcp.authorisation_refused", $$"""{"client":"{{Assistant}}","why":"declined"}""");
        await AddEventAsync(
            db, organisationId, at, "admin.mcp_client_added", $$"""{"client":"{{Assistant}}"}""", actorId: null);
        await AddEventAsync(db, organisationId, at, "user.deactivated", """{"via":"cookie"}""");
        await AddEventAsync(db, organisationId, at, "note.submitted", null);
        await AddEventAsync(db, otherId, at, "mcp.read", MadeByAssistant);
        await AddEventAsync(db, otherId, at, "mcp.refused", MadeByAssistant);
        await AddEventAsync(db, otherId, at, "user.deactivated", MadeByAssistant);
    }

    // What UserInvite writes: the person as invited under "new", the reason when one was given, and the assistant.
    private static Task<long> AddInviteAsync(
        OpsDatabase db, Guid organisationId, DateTime at, Guid inviter, string displayName, string role = "Worker",
        string? reason = null)
    {
        var email = displayName.ToLowerInvariant().Replace(' ', '.') + "@example.org";
        var details = JsonSerializer.Serialize(
            new
            {
                @new = new { email, displayName, role },
                reason,
                via = "mcp",
                client = Assistant,
                tool = "invite_user",
            },
            WithoutNulls);
        return AddEventAsync(db, organisationId, at, "user.invited", details, actorId: inviter);
    }

    // Added through the app's context under the organisation's tenant, as the app adds one, but at a time the test
    // chooses. The table has no foreign key, so an ID with no row is allowed, as after a destruction.
    private static async Task<long> AddEventAsync(
        OpsDatabase db,
        Guid organisationId,
        DateTime occurredAtUtc,
        string eventType,
        string? details,
        Guid? actorId = null)
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
            EntityType = "User",
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
