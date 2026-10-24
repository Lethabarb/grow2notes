using System.Data;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;

namespace Grow2Notes.Tests.Ops;

/// <summary>
/// <c>ops/retention-list.sql</c>, the yearly list of participants whose periods for keeping records may have ended
/// (A29, A84). Each test runs it on a migrated database of its own, so it knows every row that the script returns.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class RetentionListScriptTests(SqlServerFixture sqlServer)
{
    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task It_has_the_columns_the_managers_read_in_one_result_set()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionListColumns", ct);
        var organisation = await db.Services.AddOrganisationAsync();

        var results = await ListAsync(db, organisation.OrganisationId, ct);

        var list = Assert.Single(results);
        Assert.Equal(
            [
                "Family name", "Given name", "Date of birth", "Status", "Last submitted note",
                "7 years after last service", "Note before 18", "Turns 25", "May have ended", "Participant ID",
            ],
            list.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToArray());
    }

    [Fact]
    public async Task Last_service_is_the_latest_submitted_note_and_not_a_later_draft_or_discarded_note()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionListLastService", ct);
        var organisation = await db.Services.AddOrganisationAsync();
        var participant = await db.Services.AddParticipantAsync(organisation, familyName: "Adams");
        await SubmitAsync(db, organisation, participant.Id, new(2012, 3, 1));
        await DraftAsync(db, organisation, participant.Id, new(2015, 5, 5));
        await DiscardAsync(db, organisation, participant.Id, new(2016, 6, 6));

        var row = Row(await ListAsync(db, organisation.OrganisationId, ct), participant.Id);

        Assert.Equal(new DateTime(2012, 3, 1), row["Last submitted note"]);
        Assert.Equal(new DateTime(2019, 3, 1), row["7 years after last service"]);
        Assert.Equal("No", row["Note before 18"]);
        Assert.Equal(new DateTime(2012, 3, 7), row["Turns 25"]);
        Assert.Equal("Yes", row["May have ended"]);
    }

    [Fact]
    public async Task A_note_dated_the_day_before_the_18th_birthday_counts_and_one_dated_on_it_does_not()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionListEighteenth", ct);
        var organisation = await db.Services.AddOrganisationAsync();
        var born = new DateOnly(2000, 6, 15);
        var before = await db.Services.AddParticipantAsync(
            organisation, givenName: "Before", familyName: "Adams", dateOfBirth: born);
        var on = await db.Services.AddParticipantAsync(
            organisation, givenName: "On", familyName: "Adams", dateOfBirth: born);
        await SubmitAsync(db, organisation, before.Id, new(2018, 6, 14));
        await SubmitAsync(db, organisation, on.Id, new(2018, 6, 15));

        var results = await ListAsync(db, organisation.OrganisationId, ct);

        Assert.Equal("Yes", Row(results, before.Id)["Note before 18"]);
        Assert.Equal("No", Row(results, on.Id)["Note before 18"]);
        Assert.Equal(new DateTime(2025, 6, 15), Row(results, before.Id)["Turns 25"]);
        Assert.Equal(new DateTime(2025, 6, 15), Row(results, on.Id)["Turns 25"]);
    }

    [Fact]
    public async Task A_discarded_note_before_the_18th_birthday_still_counts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionListDiscarded", ct);
        var organisation = await db.Services.AddOrganisationAsync();
        var participant = await db.Services.AddParticipantAsync(organisation, dateOfBirth: new DateOnly(2000, 6, 15));
        await SubmitAsync(db, organisation, participant.Id, new(2020, 1, 1));
        await DiscardAsync(db, organisation, participant.Id, new(2017, 1, 1));

        var row = Row(await ListAsync(db, organisation.OrganisationId, ct), participant.Id);

        Assert.Equal("Yes", row["Note before 18"]);
    }

    [Fact]
    public async Task It_lists_an_archived_participant_and_shows_no_dates_without_a_submitted_note()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionListWhoIsListed", ct);
        var organisation = await db.Services.AddOrganisationAsync();
        var archived = await db.Services.AddParticipantAsync(
            organisation, archived: true, givenName: "Sam", familyName: "Adams");
        await SubmitAsync(db, organisation, archived.Id, new(2012, 3, 1));
        var draftOnly = await db.Services.AddParticipantAsync(organisation, givenName: "Dee", familyName: "Adams");
        await DraftAsync(db, organisation, draftOnly.Id, new(2020, 1, 1));

        var results = await ListAsync(db, organisation.OrganisationId, ct);

        Assert.Equal("Archived", Row(results, archived.Id)["Status"]);
        foreach (var id in new[] { draftOnly.Id, organisation.ParticipantId })
        {
            var row = Row(results, id);
            Assert.True(row.IsNull("Last submitted note"));
            Assert.True(row.IsNull("7 years after last service"));
            Assert.Equal("No submitted note", row["May have ended"]);
            Assert.False(row.IsNull("Turns 25"));
        }
    }

    [Fact]
    public async Task It_says_a_period_may_have_ended_by_comparing_with_today_in_Melbourne()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionListMayHaveEnded", ct);
        var organisation = await db.Services.AddOrganisationAsync();
        var today = MelbourneClock.DateOf(DateTime.UtcNow);
        var longAgo = await db.Services.AddParticipantAsync(
            organisation, givenName: "Long", dateOfBirth: new DateOnly(1980, 1, 1));
        await SubmitAsync(db, organisation, longAgo.Id, new(2010, 5, 5));
        var recent = await db.Services.AddParticipantAsync(
            organisation, givenName: "Recent", dateOfBirth: new DateOnly(1980, 1, 1));
        await SubmitAsync(db, organisation, recent.Id, today.AddYears(-1));
        var young = await db.Services.AddParticipantAsync(
            organisation, givenName: "Young", dateOfBirth: today.AddYears(-20));
        await SubmitAsync(db, organisation, young.Id, today.AddYears(-20).AddYears(10));

        var results = await ListAsync(db, organisation.OrganisationId, ct);

        Assert.Equal("Yes", Row(results, longAgo.Id)["May have ended"]);
        Assert.Equal("No", Row(results, recent.Id)["May have ended"]);
        Assert.Equal("Yes", Row(results, young.Id)["Note before 18"]);
        Assert.Equal("No", Row(results, young.Id)["May have ended"]);
    }

    [Fact]
    public async Task It_lists_the_organisations_participants_in_the_order_the_app_does_and_no_others()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(sqlServer, "RetentionListOrder", ct);
        var a = await db.Services.AddOrganisationAsync();
        var b = await db.Services.AddOrganisationAsync();
        await db.Services.AddParticipantAsync(a, givenName: "Zed", familyName: "Adams");
        await db.Services.AddParticipantAsync(a, archived: true, givenName: "Amy", familyName: "Adams");
        await db.Services.AddParticipantAsync(a, givenName: "Kim", familyName: "Brown");
        var other = await db.Services.AddParticipantAsync(b, givenName: "Aaron", familyName: "Aardvark");

        var list = Assert.Single(await ListAsync(db, a.OrganisationId, ct));

        Assert.Equal(
            ["Adams Amy", "Adams Zed", "Brown Kim", "Citizen Jane"],
            list.Rows.Cast<DataRow>().Select(row => $"{row["Family name"]} {row["Given name"]}").ToArray());
        Assert.DoesNotContain(list.Rows.Cast<DataRow>(), row => (Guid)row["Participant ID"] == other.Id);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001", "no organisation")]
    [InlineData("not-an-id", "must be an organisation ID")]
    [InlineData("", "must be an organisation ID")]
    public async Task It_refuses_an_organisation_that_does_not_exist_or_is_not_an_ID(
        string organisation, string message)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = await OpsScripts.OnDatabaseOfItsOwnAsync(
            sqlServer, $"RetentionListRefuses{organisation.Length}", ct);

        var refused = await Assert.ThrowsAsync<SqlException>(() => OpsScripts.RunAsync(
            db.ConnectionString,
            "ops/retention-list.sql",
            new Dictionary<string, string> { ["Organisation"] = organisation },
            ct));

        Assert.Equal(50000, refused.Number);
        Assert.Contains(message, refused.Message);
    }

    private static Task<IReadOnlyList<DataTable>> ListAsync(
        OpsDatabase db, Guid organisationId, CancellationToken cancellationToken) =>
        OpsScripts.RunAsync(
            db.ConnectionString,
            "ops/retention-list.sql",
            new Dictionary<string, string> { ["Organisation"] = organisationId.ToString() },
            cancellationToken);

    private static DataRow Row(IReadOnlyList<DataTable> results, Guid participantId) =>
        Assert.Single(Assert.Single(results).Rows.Cast<DataRow>(), row => (Guid)row["Participant ID"] == participantId);

    private static Task SubmitAsync(
        OpsDatabase db, SeededOrganisation organisation, Guid participantId, DateOnly date) =>
        db.Services.AddSubmittedAsync(
            organisation, new(participantId, date, organisation.WorkerId, StartedAtUtc, SubmittedAtUtc));

    private static Task DraftAsync(
        OpsDatabase db, SeededOrganisation organisation, Guid participantId, DateOnly date) =>
        db.Services.AddDraftAsync(organisation, new(participantId, date, organisation.WorkerId, StartedAtUtc));

    private static async Task DiscardAsync(
        OpsDatabase db, SeededOrganisation organisation, Guid participantId, DateOnly date)
    {
        var noteId = await db.Services.AddDraftAsync(
            organisation, new(participantId, date, organisation.WorkerId, StartedAtUtc));
        await db.Services.DiscardAsync(organisation, noteId, SubmittedAtUtc);
    }
}
