using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// <c>GET /api/reports/participants/{participantId}/export</c> (design.md §6.5), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): the file's type and name, the headers every API answer has, and the one
/// <c>participant.exported</c> event it writes before it sends the file; and what it refuses without one: a caller who
/// is not a manager, a query that breaks a rule, a participant who is not in the caller's organisation, and a range
/// with no submitted note. Each test adds an organisation of its own (<see cref="TestOrganisations"/>), so that the
/// organisation's audit events are the test's alone. What the files say is covered by
/// <see cref="ParticipantExportTextMatchTests"/>, and who may call by the endpoint matrix.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ParticipantExportEndpointTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const string Range = "from=2026-01-01&to=2026-10-31";

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 0, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc);

    // A manager's export of either format: the file, its name and its headers, and one event with exactly the four
    // details.
    [Theory]
    [InlineData("docx", DocxType)]
    [InlineData("pdf", "application/pdf")]
    public async Task A_manager_gets_the_file_with_its_headers_and_one_audit_event(string format, string contentType)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            Url(participantId, $"{Range}&format={format}&includeHistory=false"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        var name = $"participant-record_2026-01-01_to_2026-10-31.{format}";
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.Equal("attachment", disposition?.DispositionType);
        Assert.Equal(name, disposition?.FileName?.Trim('"'));
        Assert.Equal(name, disposition?.FileNameStar);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(format == "docx" ? "PK" : "%P", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
        var written = Assert.Single(await EventsOfAsync(organisation));
        Assert.Equal(
            ("participant.exported", organisation.ManagerId, "Participant", (Guid?)participantId, (Guid?)participantId),
            (written.EventType, written.ActorUserId, written.EntityType, written.EntityId, written.ParticipantId));
        var details = JsonNode.Parse(written.Details!)!.AsObject();
        Assert.Equal(
            ["from", "to", "format", "includeHistory"], details.Select(property => property.Key));
        Assert.Equal("2026-01-01", (string)details["from"]!);
        Assert.Equal("2026-10-31", (string)details["to"]!);
        Assert.Equal(format, (string)details["format"]!);
        Assert.False((bool)details["includeHistory"]!);
    }

    // S05.01.03: the flag is recorded as it was asked for, and an absent one is false.
    [Theory]
    [InlineData("&includeHistory=true", true)]
    [InlineData("&includeHistory=false", false)]
    [InlineData("", false)]
    public async Task The_event_records_whether_earlier_versions_were_asked_for(string query, bool expected)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            Url(participantId, $"{Range}&format=docx{query}"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var written = Assert.Single(await EventsOfAsync(organisation));
        Assert.Equal(expected, (bool)JsonNode.Parse(written.Details!)!["includeHistory"]!);
    }

    // The binder, not the endpoint, refuses a value that is not a boolean: the bare 400, with no file and no event.
    [Fact]
    public async Task An_include_history_value_that_is_not_a_boolean_gets_400_and_no_event()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            Url(participantId, $"{Range}&format=docx&includeHistory=maybe"), TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // §2: a worker has no part in the record, and a request with no session has none at all. Neither reads anything or
    // writes an event.
    [Theory]
    [InlineData("docx", false, HttpStatusCode.Unauthorized)]
    [InlineData("pdf", false, HttpStatusCode.Unauthorized)]
    [InlineData("docx", true, HttpStatusCode.Forbidden)]
    [InlineData("pdf", true, HttpStatusCode.Forbidden)]
    public async Task A_caller_who_is_not_a_manager_gets_no_file_and_no_event(
        string format, bool isWorker, HttpStatusCode expected)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        using var client = factory.CreateHttpsClient();
        if (isWorker)
        {
            client.SignInAs(organisation.WorkerId);
        }

        using var response = await client.GetAsync(
            Url(participantId, $"{Range}&format={format}"), TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // A90: every field error comes at once, under from, to and format alone, before anything is read, so a missing,
    // incomplete or impossible date, a to before from and a format that is not exactly pdf or docx are all 422.
    [Theory]
    [InlineData("to=2026-10-31&format=pdf", "from")]
    [InlineData("from=2026-01-01&format=pdf", "to")]
    [InlineData("from=&to=2026-10-31&format=pdf", "from")]
    [InlineData("from=2026-01&to=2026-10-31&format=pdf", "from")]
    [InlineData("from=2026-02-30&to=2026-10-31&format=pdf", "from")]
    [InlineData("from=2026-01-01&to=2026-02-30&format=pdf", "to")]
    [InlineData("from=2026-10-31&to=2026-01-01&format=pdf", "to")]
    [InlineData("from=2026-01-01&to=2026-10-31", "format")]
    [InlineData("from=2026-01-01&to=2026-10-31&format=PDF", "format")]
    [InlineData("from=2026-01-01&to=2026-10-31&format=csv", "format")]
    [InlineData("from=2026-01&to=2026-02-30&format=csv", "from,to,format")]
    [InlineData("", "from,to,format")]
    public async Task A_query_that_breaks_a_rule_gets_422_for_each_field_at_fault_and_no_event(
        string query, string expectedKeys)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(Url(participantId, query), TestContext.Current.CancellationToken);

        await AssertValidationFailedAsync(response, expectedKeys);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // The query is checked before the participant is looked for, so what it says is no clue to who exists.
    [Fact]
    public async Task A_bad_query_for_a_participant_who_does_not_exist_gets_422_not_404()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            Url(Guid.NewGuid(), "from=2026-01-01&to=2026-10-31&format=csv"), TestContext.Current.CancellationToken);

        await AssertValidationFailedAsync(response, "format");
    }

    // §6.5: a range whose only notes are a draft and a discarded draft has nothing to export.
    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    public async Task A_range_with_only_a_draft_and_a_discarded_draft_gets_404_and_no_event(string format)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation);
        await factory.Services.AddDraftAsync(
            organisation, new(participant.Id, NoteDate, organisation.WorkerId, StartedAtUtc, Narrative: "A draft"));
        var discarded = await factory.Services.AddDraftAsync(
            organisation,
            new(participant.Id, NoteDate.AddDays(1), organisation.WorkerId, StartedAtUtc, Narrative: "Discarded"));
        await factory.Services.DiscardAsync(organisation, discarded, StartedAtUtc.AddHours(1));
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        await AssertNoNotesAsync(client, participant.Id, $"{Range}&format={format}");

        Assert.Empty(await EventsOfAsync(organisation));
    }

    // A range that holds none of the participant's submitted notes, although they have one outside it.
    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    public async Task An_empty_range_gets_404_and_no_event(string format)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        await AssertNoNotesAsync(client, participantId, $"from=2026-01-01&to=2026-09-30&format={format}");

        Assert.Empty(await EventsOfAsync(organisation));
    }

    // §9.2: a participant who does not exist and one who is another organisation's get the same plain 404, with no
    // code and no event.
    [Fact]
    public async Task A_participant_who_is_not_in_the_organisation_gets_a_plain_404_and_no_event()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var other = await factory.Services.AddOrganisationAsync();
        var othersParticipantId = await SubmitANoteAsync(other, NoteDate);
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        foreach (var participantId in new[] { Guid.NewGuid(), othersParticipantId })
        {
            using var response = await client.GetAsync(
                Url(participantId, $"{Range}&format=docx"), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            if (body.Length > 0)
            {
                Assert.False(JsonDocument.Parse(body).RootElement.TryGetProperty("code", out _));
            }

            Assert.Null(response.Content.Headers.ContentDisposition);
        }

        Assert.Empty(await EventsOfAsync(organisation));
        Assert.Empty(await EventsOfAsync(other));
    }

    // R14, A60: a To date after today is taken as entered, and the title block prints it so.
    [Fact]
    public async Task A_to_date_after_today_gets_the_file_with_that_date_printed()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        var docx = await client.GetByteArrayAsync(
            Url(participantId, "from=2026-01-01&to=2026-11-01&format=docx"), TestContext.Current.CancellationToken);

        Assert.Contains("1 January 2026 to 1 November 2026", ReportText.WordParagraphs(docx));
        var written = Assert.Single(await EventsOfAsync(organisation));
        Assert.Equal("2026-11-01", (string)JsonNode.Parse(written.Details!)!["to"]!);
    }

    // §3.7: an archived participant's record is a record of what happened, the same as an active one's.
    [Fact]
    public async Task An_archived_participant_gets_the_file_the_same_as_an_active_one()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation, archived: true);
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(participant.Id, NoteDate, organisation.WorkerId, StartedAtUtc, SubmittedAtUtc));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            Url(participant.Id, $"{Range}&format=docx"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var docx = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Jane Citizen", ReportText.WordParagraphs(docx));
        Assert.Single(await EventsOfAsync(organisation));
    }

    // §5.3: the event is committed before the file is sent, so an export whose event cannot be written sends no file.
    [Fact]
    public async Task An_export_whose_event_cannot_be_written_gets_500_and_no_file()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participantId = await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
            services.AddScoped<IAuditWriter, ThrowingAuditWriter>();
        }));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            Url(participantId, $"{Range}&format=docx"), TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // A19: the app logs nothing of the record. The export runs, with earlier versions, over a note whose text, flag
    // reason and participant would show in a log that echoed what it read.
    [Fact]
    public async Task The_logs_hold_no_note_content_and_no_name()
    {
        var logs = new RecordingLoggerProvider();
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Quentin", familyName: "Zebediah");
        var noteId = await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participant.Id,
                NoteDate,
                organisation.WorkerId,
                StartedAtUtc,
                SubmittedAtUtc,
                "Narrative that must stay out of the logs",
                IsFlagged: true,
                FlagReason: "Flag reason that must stay out of the logs"));
        await factory.Services.AddVersion2Async(
            organisation, noteId, SubmittedAtUtc.AddHours(1), flagReason: null, flaggedVersionNumber: 1);
        await using var app = factory.WithWebHostBuilder(builder => builder
            .UseClock(new FakeTimeProvider(Now))
            .ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            Url(participant.Id, $"{Range}&format=docx&includeHistory=true"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string[] secrets = ["must stay out of the logs", "Quentin", "Zebediah"];
        Assert.DoesNotContain(logs.Entries, entry => secrets.Any(entry.Holds));
    }

    // Adds a participant of the organisation's own with a note for the day, submitted by its worker, as Submit leaves
    // it, and gives the participant's ID. The times are made up (A35).
    private async Task<Guid> SubmitANoteAsync(SeededOrganisation organisation, DateOnly noteDate)
    {
        var participant = await factory.Services.AddParticipantAsync(organisation);
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(participant.Id, noteDate, organisation.WorkerId, StartedAtUtc, SubmittedAtUtc));
        return participant.Id;
    }

    private static string Url(Guid participantId, string query) =>
        $"/api/reports/participants/{participantId}/export?{query}";

    // 422 validation.failed whose errors have exactly the given keys, written as a comma-separated list.
    private static async Task AssertValidationFailedAsync(HttpResponseMessage response, string expectedKeys)
    {
        var body = await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        var problem = JsonNode.Parse(body)!;
        Assert.Equal("validation.failed", (string)problem["code"]!);
        Assert.Equal(
            expectedKeys.Split(',').Order(StringComparer.Ordinal),
            problem["errors"]!.AsObject().Select(property => property.Key).Order(StringComparer.Ordinal));
    }

    // The export says report.no_notes, with no file.
    private static async Task AssertNoNotesAsync(HttpClient client, Guid participantId, string query)
    {
        using var response = await client.GetAsync(Url(participantId, query), TestContext.Current.CancellationToken);

        var body = await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("report.no_notes", JsonNode.Parse(body)!["code"]!.GetValue<string>());
        Assert.Null(response.Content.Headers.ContentDisposition);
    }

    // Every event of the organisation, read in SQL so that the query filter, which would need a tenant, does not decide
    // what is seen.
    private async Task<List<WrittenEvent>> EventsOfAsync(SeededOrganisation organisation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<WrittenEvent>($"""
            SELECT [EventType], [OrganisationId], [ActorUserId], [EntityType], [EntityId], [ParticipantId], [Details]
            FROM [AuditEvent]
            WHERE [OrganisationId] = {organisation.OrganisationId}
            ORDER BY [Id]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>An audit entry's fields that a test checks, its <c>Details</c> as the JSON stored.</summary>
    private sealed record WrittenEvent(
        string EventType,
        Guid OrganisationId,
        Guid? ActorUserId,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);

    // Throws before anything is written, as the app's writer does when its save fails.
    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public Task WriteAsync(
            Guid organisationId,
            Guid? actorUserId,
            string eventType,
            string? entityType,
            Guid? entityId,
            Guid? participantId,
            JsonObject? details,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The audit event could not be written.");
    }
}
