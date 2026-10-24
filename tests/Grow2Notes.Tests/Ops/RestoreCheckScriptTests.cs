using System.Data;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// <c>ops/restore-check.sql</c>, which the operator runs on a restored copy and on production (A90). Each test runs it
/// on a migrated database of its own, so it knows every row that the script counts.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RestoreCheckScriptTests(SqlServerFixture sqlServer)
{
    private const string Script = "ops/restore-check.sql";
    private const string None = "none yet";

    [Fact]
    public async Task It_writes_nothing_because_it_succeeds_on_a_database_that_is_read_only()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RestoreCheckReadOnly", ct);
        await SeedAsync(db);
        var before = await OpsScripts.RunAsync(db.ConnectionString, Script, cancellationToken: ct);
        var name = new SqlConnectionStringBuilder(db.ConnectionString).InitialCatalog;

        await SetReadOnlyAsync(name, true, ct);
        try
        {
            var after = await OpsScripts.RunAsync(db.ConnectionString, Script, cancellationToken: ct);

            Assert.Equal(Rows(before[0]), Rows(after[0]));
            Assert.Equal(Rows(before[1]), Rows(after[1]));
        }
        finally
        {
            await SetReadOnlyAsync(name, false, ct);
        }
    }

    [Fact]
    public async Task It_counts_every_table_of_the_model_and_gives_the_latest_values_of_a_seeded_database()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RestoreCheckSeeded", ct);
        var latestNote = await SeedAsync(db);

        var results = await OpsScripts.RunAsync(db.ConnectionString, Script, cancellationToken: ct);

        Assert.Equal(2, results.Count);
        Assert.Equal(["Check", "Value"], ColumnNames(results[0]));
        Assert.Equal(["Table", "Rows"], ColumnNames(results[1]));
        string[] migrations;
        string[] modelTables;
        await using (var scope = db.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
            migrations = [.. context.Database.GetMigrations()];
            modelTables = [.. context.Model.GetRelationalModel().Tables.Select(table => table.Name)];
        }

        Assert.Equal(
            [
                $"Latest migration|{migrations[^1]}",
                "Latest audit event (UTC)|2026-10-06 01:02:03.456",
                "Latest note version (UTC)|2026-10-05 06:07:08.123",
                $"Note with the latest version|{latestNote.ToString().ToUpperInvariant()} version 2",
            ],
            Rows(results[0]));
        var counts = results[1].Rows.Cast<DataRow>()
            .ToDictionary(row => (string)row["Table"], row => (long)row["Rows"]);
        Assert.Equal(
            modelTables.Append("__EFMigrationsHistory").Order(StringComparer.Ordinal),
            counts.Keys.Order(StringComparer.Ordinal));
        foreach (var (table, rows) in counts)
        {
            var actual = await CountAsync(db.ConnectionString, table, ct);
            Assert.True(rows == actual, $"The script counted {rows} rows in {table}, which has {actual}.");
        }

        Assert.Equal(2, counts["Note"]);
        Assert.Equal(3, counts["NoteVersion"]);
        Assert.Equal(2, counts["AuditEvent"]);
    }

    [Fact]
    public async Task It_runs_on_a_database_with_no_notes_or_audit_events_and_checks_the_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await sqlServer.CreateDatabaseAsync("RestoreCheckEmpty", ct);
        var started = await ServerTimeAsync(connectionString, "SELECT GETDATE()", ct);

        var results = await OpsScripts.RunAsync(connectionString, Script, cancellationToken: ct);

        Assert.Equal(2, results.Count);
        var checks = Rows(results[0]);
        Assert.StartsWith("Latest migration|", checks[0], StringComparison.Ordinal);
        Assert.DoesNotContain(None, checks[0], StringComparison.Ordinal);
        Assert.Equal(
            [
                $"Latest audit event (UTC)|{None}",
                $"Latest note version (UTC)|{None}",
                $"Note with the latest version|{None}",
            ],
            checks[1..]);
        Assert.Contains("Note|0", Rows(results[1]));

        // DBCC CHECKDB records when it last found the database sound, to the second.
        var lastSound = await ServerTimeAsync(
            connectionString, "SELECT DATABASEPROPERTYEX(DB_NAME(), 'LastGoodCheckDbTime')", ct);
        Assert.True(
            lastSound >= started.AddTicks(-(started.Ticks % TimeSpan.TicksPerSecond)),
            $"CHECKDB last found the database sound at {lastSound:O}, before the script ran at {started:O}.");
    }

    // Two organisations with a note each, and an audit event each. The note of organisation A is the one that gets the
    // latest version, its version 2; the script covers the whole database, so organisation B's rows count too. Gives
    // the ID of that note.
    private static async Task<Guid> SeedAsync(OpsDatabase db)
    {
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        var noteA = await db.Services.AddSubmittedAsync(a, Submitted(a, At(2026, 10, 5, 1, 2, 3, 111)));
        await db.Services.AddSubmittedAsync(b, Submitted(b, At(2026, 10, 5, 4, 5, 6, 222)));
        await db.Services.AddVersion2Async(
            a, noteA, At(2026, 10, 5, 6, 7, 8, 123), flagReason: null, flaggedVersionNumber: 1);
        await AddAuditEventAsync(db, a.OrganisationId, At(2026, 10, 6, 1, 2, 3, 456));
        await AddAuditEventAsync(db, b.OrganisationId, At(2026, 10, 3, 1, 2, 3, 456));
        return noteA;
    }

    private static TestSubmittedNote Submitted(SeededOrganisation organisation, DateTime submittedAtUtc) =>
        new(organisation.ParticipantId, new DateOnly(2026, 10, 5), organisation.WorkerId,
            submittedAtUtc.AddHours(-1), submittedAtUtc);

    private static DateTime At(int year, int month, int day, int hour, int minute, int second, int millis) =>
        new(year, month, day, hour, minute, second, millis, DateTimeKind.Utc);

    private static string[] ColumnNames(DataTable table) =>
        [.. table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)];

    private static string[] Rows(DataTable table) =>
        [.. table.Rows.Cast<DataRow>().Select(row => string.Join('|', row.ItemArray))];

    // The audit table has no foreign key, so the event needs no user or participant.
    private static async Task AddAuditEventAsync(OpsDatabase db, Guid organisationId, DateTime occurredAtUtc)
    {
        await using var scope = db.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        context.AuditEvents.Add(new AuditEvent
        {
            OrganisationId = organisationId,
            OccurredAtUtc = occurredAtUtc,
            EventType = "note.submitted",
            EntityType = "Note",
        });
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisationId))
        {
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // ROLLBACK IMMEDIATE ends the sessions that the app's pool still holds, so the pool is emptied on both sides of the
    // switch and no later command is handed a connection that the switch killed. It runs from master, because a session
    // in the database itself would block the switch.
    private async Task SetReadOnlyAsync(string database, bool readOnly, CancellationToken ct)
    {
        SqlConnection.ClearAllPools();
        await using (var connection = new SqlConnection(sqlServer.ConnectionStringFor("master")))
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"ALTER DATABASE [{database}] SET {(readOnly ? "READ_ONLY" : "READ_WRITE")} WITH ROLLBACK IMMEDIATE";
            await command.ExecuteNonQueryAsync(ct);
        }

        SqlConnection.ClearAllPools();
    }

    private static async Task<long> CountAsync(string connectionString, string table, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT_BIG(*) FROM dbo.[{table}]";
        return (long)(await command.ExecuteScalarAsync(ct))!;
    }

    private static async Task<DateTime> ServerTimeAsync(string connectionString, string query, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        return Assert.IsType<DateTime>(await command.ExecuteScalarAsync(ct));
    }
}
