using System.Data;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.Data.SqlClient;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// <c>ops/restore-note.sql</c>, which the operator runs on a restored copy and on production for the note that
/// <c>ops/restore-check.sql</c> names (A83). Each test runs it on a migrated database of its own, with a second
/// organisation whose rows must never come back.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RestoreNoteScriptTests(SqlServerFixture sqlServer)
{
    private const string Script = "ops/restore-note.sql";

    // The texts that the fixtures give a note's versions, which the script must never show.
    private static readonly string[] NarrativeTexts = ["made toast", "walk to the shops"];

    [Fact]
    public async Task It_shows_the_note_and_each_version_with_its_time_editor_items_and_text_length_but_no_text()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RestoreNoteShows", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        var author = await db.Services.AddUserAsync(a, UserRole.Worker, "Casey Quinn");
        var noteA = await SeedNoteAsync(db, a, "Alex", "Adams", author);
        await SeedNoteAsync(db, b, "Kim", "Brown", b.WorkerId);

        var results = await RunAsync(db, Text(noteA), ct);

        Assert.Equal(2, results.Count);
        Assert.Equal(["Participant", "Note date", "Status", "Current version", "Author"], ColumnNames(results[0]));
        Assert.Equal(
            ["Version", "Saved (Melbourne)", "Saved by", "Items", "Guided notes length"], ColumnNames(results[1]));
        var note = Assert.Single(results[0].Rows.Cast<DataRow>());
        object?[] expected = ["Alex Adams", new DateTime(2026, 10, 5), "Submitted", 2, "Casey Quinn"];
        Assert.Equal(expected, note.ItemArray);
        var versions = results[1].Rows.Cast<DataRow>().ToArray();
        Assert.Equal(2, versions.Length);
        AssertVersion(versions[0], 1, new DateTime(2026, 10, 5, 12, 2, 3, 111), "Casey Quinn", 2, 16);
        AssertVersion(versions[1], 2, new DateTime(2026, 10, 5, 17, 7, 8, 123), "Manager", 0, 42);
        Assert.DoesNotContain(
            results.SelectMany(table => table.Rows.Cast<DataRow>()).SelectMany(row => row.ItemArray),
            cell => NarrativeTexts.Any(text => cell?.ToString()?.Contains(text, StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task It_reads_only_the_organisation_of_the_note_it_is_given()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RestoreNoteTenancy", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        await SeedNoteAsync(db, a, "Alex", "Adams", a.WorkerId);
        var noteB = await SeedNoteAsync(db, b, "Kim", "Brown", b.WorkerId);

        var results = await RunAsync(db, Text(noteB), ct);

        var note = Assert.Single(results[0].Rows.Cast<DataRow>());
        Assert.Equal("Kim Brown", note["Participant"]);
        Assert.Equal(2, results[1].Rows.Count);
    }

    [Fact]
    public async Task It_refuses_a_note_that_is_malformed_unknown_or_empty()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RestoreNoteRefuses", ct);
        var a = await db.Services.AddOrganisationAsync();
        await SeedNoteAsync(db, a, "Alex", "Adams", a.WorkerId);

        (string Why, string Note)[] cases =
        [
            ("a value that is not an ID", "not-a-note"),
            ("an ID that no note has", Text(Guid.NewGuid())),
            ("an empty value", ""),
        ];

        foreach (var (why, note) in cases)
        {
            var refused = await Assert.ThrowsAsync<SqlException>(() => RunAsync(db, note, ct));
            Assert.True(refused.Number == 50000, $"{why} was not refused by the script: {refused.Message}");
        }
    }

    // A submitted note of the participant, version 1 by the author with two goal rows and version 2 by the manager of
    // the organisation with none, as a manager's edit leaves it. Gives the note's ID.
    private static async Task<Guid> SeedNoteAsync(
        OpsDatabase db, SeededOrganisation organisation, string givenName, string familyName, Guid authorId)
    {
        var participant = await db.Services.AddParticipantAsync(
            organisation, givenName: givenName, familyName: familyName);
        var goals = await db.Services.AddGoalsAsync(
            organisation, participant.Id, new TestGoal("Cook dinner", 1), new TestGoal("Catch the bus", 2));
        var submittedAtUtc = At(2026, 10, 5, 1, 2, 3, 111);
        var note = await db.Services.AddSubmittedAsync(
            organisation,
            new(participant.Id, new DateOnly(2026, 10, 5), authorId, submittedAtUtc.AddHours(-1), submittedAtUtc,
                Goals: [new(goals[0], "Cook dinner", true), new(goals[1], "Catch the bus", false)]));
        await db.Services.AddVersion2Async(
            organisation, note, At(2026, 10, 5, 6, 7, 8, 123), flagReason: null, flaggedVersionNumber: 1);
        return note;
    }

    private static DateTime At(int year, int month, int day, int hour, int minute, int second, int millis) =>
        new(year, month, day, hour, minute, second, millis, DateTimeKind.Utc);

    // SQL Server prints an ID in capitals.
    private static string Text(Guid id) => id.ToString().ToUpperInvariant();

    private static string[] ColumnNames(DataTable table) =>
        [.. table.Columns.Cast<DataColumn>().Select(column => column.ColumnName)];

    // The time is shown in Melbourne with its offset, which was +11:00 on 5 October 2026. DateTimeOffset compares
    // instants, so the offset the operator reads is asserted on its own.
    private static void AssertVersion(
        DataRow row, int version, DateTime melbourne, string savedBy, long items, long guidedNotesLength)
    {
        Assert.Equal(version, row["Version"]);
        var saved = Assert.IsType<DateTimeOffset>(row["Saved (Melbourne)"]);
        Assert.Equal(melbourne, saved.DateTime);
        Assert.Equal(TimeSpan.FromHours(11), saved.Offset);
        Assert.Equal(savedBy, row["Saved by"]);
        Assert.Equal(items, row["Items"]);
        Assert.Equal(guidedNotesLength, row["Guided notes length"]);
    }

    private static Task<IReadOnlyList<DataTable>> RunAsync(OpsDatabase db, string note, CancellationToken ct) =>
        OpsScripts.RunAsync(db.ConnectionString, Script, new Dictionary<string, string> { ["Note"] = note }, ct);
}
