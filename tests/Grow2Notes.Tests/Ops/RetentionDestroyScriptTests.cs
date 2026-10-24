using System.Data;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// <c>ops/retention-destroy.sql</c>, which destroys one archived participant's records (A29, design.md §12, HPP 4.3),
/// and the scope guard that makes a new table a decision about it. Each test runs the script on a migrated database
/// of its own, so it knows every row in every table.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RetentionDestroyScriptTests(SqlServerFixture sqlServer)
{
    private const string DateOfBirth = "2001-02-03";

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);

    // In the order that the script deletes, which the foreign keys fix.
    private static readonly string[] Destroyed =
    [
        "NoteVersionItem", "NoteVersionGroup", "NoteVersion", "NoteReview", "NoteDraft", "Note", "Goal", "Participant",
    ];

    private static readonly string[] Kept =
    [
        "Organisation", "AspNetUsers", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens",
        "AspNetUserPasskeys", "CommonItemGroup", "CommonItem", "AuditEvent", "DataProtectionKeys",
        "__EFMigrationsHistory",
    ];

    // What the scenario gives participant P in each table: see SeedAsync.
    private static readonly Dictionary<string, int> Expected = new()
    {
        ["NoteVersionItem"] = 3,
        ["NoteVersionGroup"] = 4,
        ["NoteVersion"] = 2,
        ["NoteReview"] = 1,
        ["NoteDraft"] = 3,
        ["Note"] = 3,
        ["Goal"] = 2,
        ["Participant"] = 1,
    };

    [Fact]
    public async Task Destroy_deletes_every_row_of_the_participant_in_the_eight_tables_and_prints_the_log_values()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionDestroyDestroys", ct);
        var scenario = await SeedAsync(db);
        var counts = await CountsAsync(db, ct);
        var audit = await AuditRowsAsync(db, ct);
        var todayBefore = MelbourneClock.DateOf(DateTime.UtcNow);

        var results = await RunAsync(db, scenario, DateOfBirth, "destroy", ct);

        var todayAfter = MelbourneClock.DateOf(DateTime.UtcNow);
        AssertResults(results, "Destroyed", todayBefore, todayAfter);
        var after = await CountsAsync(db, ct);
        foreach (var table in Destroyed.Concat(Kept))
        {
            Assert.True(
                counts[table] - Expected.GetValueOrDefault(table) == after[table],
                $"{table} had {counts[table]} rows and now has {after[table]}.");
        }

        Assert.Equal(0, await CountAsync(db, "Participant", "Id", scenario.Participant, ct));
        Assert.Equal(0, await CountAsync(db, "Note", "ParticipantId", scenario.Participant, ct));
        Assert.Equal(0, await CountAsync(db, "Goal", "ParticipantId", scenario.Participant, ct));
        Assert.Equal(audit, await AuditRowsAsync(db, ct));
    }

    [Fact]
    public async Task Preview_prints_the_same_values_and_counts_and_changes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionDestroyPreviews", ct);
        var scenario = await SeedAsync(db);
        var counts = await CountsAsync(db, ct);
        var audit = await AuditRowsAsync(db, ct);
        var todayBefore = MelbourneClock.DateOf(DateTime.UtcNow);

        var results = await RunAsync(db, scenario, DateOfBirth, "preview", ct);

        var todayAfter = MelbourneClock.DateOf(DateTime.UtcNow);
        AssertResults(results, "Preview: nothing was deleted", todayBefore, todayAfter);
        Assert.Equal(counts, await CountsAsync(db, ct));
        Assert.Equal(audit, await AuditRowsAsync(db, ct));
    }

    [Fact]
    public async Task A_participant_with_no_notes_has_no_period()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionDestroyNoNotes", ct);
        var organisation = await db.Services.AddOrganisationAsync();
        var participant = await db.Services.AddParticipantAsync(
            organisation, archived: true, givenName: "Nina", familyName: "Nolan",
            dateOfBirth: new DateOnly(2001, 2, 3));

        var results = await OpsScripts.RunAsync(
            db.ConnectionString,
            "ops/retention-destroy.sql",
            Variables(organisation.OrganisationId, participant.Id, DateOfBirth, "destroy"),
            ct);

        var summary = Assert.Single(results[0].Rows.Cast<DataRow>());
        Assert.Equal("Nina Nolan", summary["Name"]);
        Assert.True(summary.IsNull("Period from"));
        Assert.True(summary.IsNull("Period to"));
        Assert.Equal(
            ["NoteVersionItem|0", "NoteVersionGroup|0", "NoteVersion|0", "NoteReview|0", "NoteDraft|0", "Note|0",
                "Goal|0", "Participant|1"],
            Table(results[1]));
    }

    [Fact]
    public async Task It_refuses_what_it_should_and_changes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionDestroyRefuses", ct);
        var scenario = await SeedAsync(db);
        var good = Variables(scenario.A.OrganisationId, scenario.Participant, DateOfBirth, "destroy");
        var refusals = new (string Case, string Name, string Value)[]
        {
            ("not archived", "Participant", scenario.ActiveParticipant.ToString()),
            ("other organisation", "Organisation", scenario.B.OrganisationId.ToString()),
            ("unknown organisation", "Organisation", Guid.NewGuid().ToString()),
            ("unknown participant", "Participant", Guid.NewGuid().ToString()),
            ("wrong date of birth", "DateOfBirth", "2001-02-04"),
            ("mode delete", "Mode", "delete"),
            ("empty mode", "Mode", ""),
            ("malformed participant", "Participant", "not-an-id"),
            ("empty participant", "Participant", ""),
            ("empty date of birth", "DateOfBirth", ""),
            ("date not in full", "DateOfBirth", "2001-2-3"),
        };
        var counts = await CountsAsync(db, ct);
        var audit = await AuditRowsAsync(db, ct);

        foreach (var (name, variable, value) in refusals)
        {
            var variables = new Dictionary<string, string>(good) { [variable] = value };

            var refused = await Assert.ThrowsAsync<SqlException>(
                () => OpsScripts.RunAsync(db.ConnectionString, "ops/retention-destroy.sql", variables, ct));

            Assert.True(refused.Number == 50000, $"Refusing {name}: error {refused.Number}, {refused.Message}");
            Assert.Equal(counts, await CountsAsync(db, ct));
        }

        Assert.Equal(audit, await AuditRowsAsync(db, ct));
    }

    [Fact]
    public async Task It_runs_in_one_transaction_so_a_failure_at_the_last_delete_undoes_the_rest()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionDestroyOneTransaction", ct);
        var scenario = await SeedAsync(db);
        await using (var connection = new SqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE TABLE dbo.HoldsParticipant (
                    Id int NOT NULL PRIMARY KEY,
                    ParticipantId uniqueidentifier NOT NULL REFERENCES dbo.Participant (Id));
                INSERT dbo.HoldsParticipant (Id, ParticipantId) VALUES (1, '{scenario.Participant}');
                """;
            await command.ExecuteNonQueryAsync(ct);
        }

        var counts = await CountsAsync(db, ct);

        var failed = await Assert.ThrowsAsync<SqlException>(
            () => RunAsync(db, scenario, DateOfBirth, "destroy", ct));

        Assert.Equal(547, failed.Number);
        Assert.Equal(counts, await CountsAsync(db, ct));
    }

    [Fact]
    public async Task Every_table_is_listed_as_destroyed_by_the_script_or_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await sqlServer.CreateDatabaseAsync("RetentionDestroyScope", ct);
        var tables = new List<string>();
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND is_ms_shipped = 0 ORDER BY name
                """;
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }
        }

        var unlisted = tables.Except(Destroyed).Except(Kept).ToList();
        Assert.True(
            unlisted.Count == 0,
            $"New table {string.Join(", ", unlisted)}: decide whether ops/retention-destroy.sql must delete from it, "
            + "then add it to one list.");
        Assert.Empty(Destroyed.Concat(Kept).Except(tables));
    }

    private static void AssertResults(
        IReadOnlyList<DataTable> results, string outcome, DateOnly todayBefore, DateOnly todayAfter)
    {
        Assert.Equal(2, results.Count);
        Assert.Equal(
            ["Outcome", "Name", "Period from", "Period to", "Date of deletion"],
            results[0].Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray());
        var summary = Assert.Single(results[0].Rows.Cast<DataRow>());
        Assert.Equal(outcome, summary["Outcome"]);
        Assert.Equal("Pat Archer", summary["Name"]);
        Assert.Equal(new DateTime(2020, 5, 4), summary["Period from"]);
        Assert.Equal(new DateTime(2020, 5, 6), summary["Period to"]);
        Assert.Contains(
            DateOnly.FromDateTime((DateTime)summary["Date of deletion"]), new[] { todayBefore, todayAfter });

        Assert.Equal(["Table", "Rows"], results[1].Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
        Assert.Equal(Destroyed.Select(table => $"{table}|{Expected[table]}").ToArray(), Table(results[1]));
    }

    private static string[] Table(DataTable table) =>
        [.. table.Rows.Cast<DataRow>().Select(row => string.Join('|', row.ItemArray))];

    private static Dictionary<string, string> Variables(
        Guid organisation, Guid participant, string dateOfBirth, string mode) =>
        new()
        {
            ["Organisation"] = organisation.ToString(),
            ["Participant"] = participant.ToString(),
            ["DateOfBirth"] = dateOfBirth,
            ["Mode"] = mode,
        };

    private static Task<IReadOnlyList<DataTable>> RunAsync(
        OpsDatabase db, Scenario scenario, string dateOfBirth, string mode, CancellationToken cancellationToken) =>
        OpsScripts.RunAsync(
            db.ConnectionString,
            "ops/retention-destroy.sql",
            Variables(scenario.A.OrganisationId, scenario.Participant, dateOfBirth, mode),
            cancellationToken);

    // Every table's rows, counted in SQL from a connection of its own, so that nothing of the app decides what is seen.
    private static async Task<Dictionary<string, long>> CountsAsync(OpsDatabase db, CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, long>();
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var table in Destroyed.Concat(Kept))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{table}]";
            counts[table] = (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }

        return counts;
    }

    private static async Task<long> CountAsync(
        OpsDatabase db, string table, string column, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{table}] WHERE [{column}] = @Id";
        command.Parameters.AddWithValue("@Id", id);
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task<List<string>> AuditRowsAsync(OpsDatabase db, CancellationToken cancellationToken)
    {
        var rows = new List<string>();
        await using var connection = new SqlConnection(db.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CONCAT(Id, '|', OrganisationId, '|', CONVERT(varchar(30), OccurredAtUtc, 121), '|', ActorUserId, '|',
                          EventType, '|', EntityType, '|', EntityId, '|', ParticipantId, '|', Details, '|', IpAddress)
            FROM dbo.AuditEvent ORDER BY Id
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    /// <summary>
    /// Organisation A with participant P, archived, born on <see cref="DateOfBirth"/>, and everything that can hang
    /// off a participant; another active participant of A; and organisation B with a note of its own. P has two goals;
    /// a submitted note with version 1 and version 2, whose group and item rows point at P's goal and at A's common
    /// items, a review and a working copy; a draft note; a discarded note; and audit rows. The note dates run from
    /// 4 to 6 May 2020.
    /// </summary>
    private static async Task<Scenario> SeedAsync(OpsDatabase db)
    {
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        var p = await db.Services.AddParticipantAsync(
            a, archived: true, givenName: "Pat", familyName: "Archer", dateOfBirth: new DateOnly(2001, 2, 3));
        var other = await db.Services.AddParticipantAsync(a, givenName: "Quinn", familyName: "Other");
        var group = await db.Services.AddGroupAsync(a.OrganisationId, new("Mornings", 1));
        var item = await db.Services.AddItemAsync(a.OrganisationId, group.Id, new("Make tea", 1));
        var goals = await db.Services.AddGoalsAsync(a, p.Id, new TestGoal("Goal one", 1), new TestGoal("Goal two", 2));
        var otherGoals = await db.Services.AddGoalsAsync(a, other.Id, new TestGoal("Their goal", 1));
        TestVersionGroup[] groups =
        [
            new(a.EveryNoteGroupId, "Every note", true, false, []),
            new(group.Id, "Mornings", false, true, [new(item.Id, "Make tea", true)]),
        ];

        var noteId = await db.Services.AddSubmittedAsync(
            a,
            new(p.Id, new(2020, 5, 4), a.WorkerId, StartedAtUtc, SubmittedAtUtc, IsFlagged: true, FlagReason: "Check",
                Goals: [new(goals[0], "Goal one", true)], Groups: groups));
        await db.Services.AddVersion2Async(
            a, noteId, SubmittedAtUtc.AddHours(1), flagReason: null, flaggedVersionNumber: 1, groups: groups);
        await db.Services.AddReviewAsync(a, noteId, 1, a.ManagerId, SubmittedAtUtc.AddDays(1));
        await db.Services.AddPendingEditAsync(a, noteId, a.WorkerId, SubmittedAtUtc.AddDays(2));
        await db.Services.AddDraftAsync(a, new(p.Id, new(2020, 5, 5), a.WorkerId, StartedAtUtc));
        var discarded = await db.Services.AddDraftAsync(a, new(p.Id, new(2020, 5, 6), a.WorkerId, StartedAtUtc));
        await db.Services.DiscardAsync(a, discarded, SubmittedAtUtc);

        await db.Services.AddSubmittedAsync(
            a,
            new(other.Id, new(2020, 5, 4), a.WorkerId, StartedAtUtc, SubmittedAtUtc,
                Goals: [new(otherGoals[0], "Their goal", true)]));
        await db.Services.AddSubmittedAsync(
            b, new(b.ParticipantId, new(2020, 5, 4), b.WorkerId, StartedAtUtc, SubmittedAtUtc));

        await AddAuditAsync(db, a, p.Id);
        await AddAuditAsync(db, a, p.Id);
        await AddAuditAsync(db, a, other.Id);
        await AddAuditAsync(db, b, b.ParticipantId);
        return new(a, b, p.Id, other.Id);
    }

    private static async Task AddAuditAsync(OpsDatabase db, SeededOrganisation organisation, Guid participantId)
    {
        await using var scope = db.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        context.AuditEvents.Add(new AuditEvent
        {
            OrganisationId = organisation.OrganisationId,
            OccurredAtUtc = new DateTime(2026, 10, 9, 1, 2, 3, 456, DateTimeKind.Utc),
            ActorUserId = organisation.ManagerId,
            EventType = "participant.updated",
            EntityType = "Participant",
            EntityId = participantId,
            ParticipantId = participantId,
            Details = """{"givenName":"Pat"}""",
        });
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private sealed record Scenario(
        SeededOrganisation A, SeededOrganisation B, Guid Participant, Guid ActiveParticipant);
}
