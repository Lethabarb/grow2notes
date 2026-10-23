using System.Net;
using System.Text.Json.Nodes;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Reports;

/// <summary>
/// <c>GET /api/reports/daily/{date}/export?format=docx|pdf</c> (design.md §6.5), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): the file's type and name, the headers every API answer has, and the one
/// <c>report.downloaded</c> event it writes before it sends the file; and what it refuses without one: a caller who is
/// not a manager, a format that is not exactly <c>docx</c> or <c>pdf</c>, a date that is not a date, and a day with no
/// submitted note in the caller's organisation. Each test adds an organisation of its own
/// (<see cref="TestOrganisations"/>), so that the organisation's audit events are the test's alone. What the files say
/// is covered by <see cref="DailyReportTextMatchTests"/>, and who may call by the endpoint matrix.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class DailyReportEndpointTests(Grow2NotesFactory factory) : IClassFixture<Grow2NotesFactory>
{
    private const string Date = "2026-10-01";

    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // 9:14 am on Friday 2 October 2026 in Melbourne, before the daylight-saving change on the 4th.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 0, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc);

    // A manager's download of either format: the file, its name and its headers, and one event.
    [Theory]
    [InlineData("docx", DocxType)]
    [InlineData("pdf", "application/pdf")]
    public async Task A_manager_gets_the_file_with_its_headers_and_one_audit_event(string format, string contentType)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            $"/api/reports/daily/{Date}/export?format={format}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.Equal("attachment", disposition?.DispositionType);
        Assert.Equal($"daily-notes_{Date}.{format}", disposition?.FileName?.Trim('"'));
        Assert.Equal($"daily-notes_{Date}.{format}", disposition?.FileNameStar);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(format == "docx" ? "PK" : "%P", System.Text.Encoding.ASCII.GetString(bytes, 0, 2));
        var written = Assert.Single(await EventsOfAsync(organisation));
        Assert.Equal(
            ("report.downloaded", organisation.ManagerId, "Report", (Guid?)null, (Guid?)null),
            (written.EventType, written.ActorUserId, written.EntityType, written.EntityId, written.ParticipantId));
        var details = JsonNode.Parse(written.Details!)!.AsObject();
        Assert.Equal([Date, format], [(string)details["date"]!, (string)details["format"]!]);
        Assert.Equal(2, details.Count);
    }

    // §2: a worker has no part in the report, and a request with no session has none at all. Neither reads anything or
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
        await SubmitANoteAsync(organisation, NoteDate);
        using var client = factory.CreateHttpsClient();
        if (isWorker)
        {
            client.SignInAs(organisation.WorkerId);
        }

        using var response = await client.GetAsync(
            $"/api/reports/daily/{Date}/export?format={format}", TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // §6.5: a day whose only notes are a draft and a discarded draft has nothing to report.
    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    public async Task A_day_with_only_a_draft_and_a_discarded_draft_gets_404_and_no_event(string format)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var drafter = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Dora", familyName: "Draftley");
        await factory.Services.AddDraftAsync(
            organisation, new(drafter.Id, NoteDate, organisation.WorkerId, StartedAtUtc, Narrative: "A draft"));
        var discarded = await factory.Services.AddParticipantAsync(
            organisation, givenName: "Dan", familyName: "Discardson");
        var noteId = await factory.Services.AddDraftAsync(
            organisation,
            new(discarded.Id, NoteDate, organisation.WorkerId, StartedAtUtc, Narrative: "A discarded draft"));
        await factory.Services.DiscardAsync(organisation, noteId, StartedAtUtc.AddHours(1));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        await AssertNoNotesAsync(client, Date, format);

        Assert.Empty(await EventsOfAsync(organisation));
    }

    // §6.5: any date after today in Melbourne has no submitted note, so it is the same 404. At the clock's time it is
    // Friday 2 October there, so the next day is the 3rd.
    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    public async Task A_future_date_gets_404_and_no_event(string format)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        await AssertNoNotesAsync(client, "2026-10-03", format);

        Assert.Empty(await EventsOfAsync(organisation));
    }

    // §5.9 item 7: a day whose notes are all another organisation's has no notes for this manager.
    [Theory]
    [InlineData("docx")]
    [InlineData("pdf")]
    public async Task A_day_with_notes_only_in_another_organisation_gets_404_and_no_event(string format)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var other = await factory.Services.AddOrganisationAsync();
        await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using var client = app.CreateHttpsClient().SignInAs(other.ManagerId);

        await AssertNoNotesAsync(client, Date, format);

        Assert.Empty(await EventsOfAsync(organisation));
        Assert.Empty(await EventsOfAsync(other));
    }

    // §6.5: the format is exactly docx or pdf, in lower case, or the request is refused before anything is read.
    [Theory]
    [InlineData("")]
    [InlineData("?format=doc")]
    [InlineData("?format=PDF")]
    public async Task A_missing_or_unknown_format_gets_400_and_no_event(string query)
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        await SubmitANoteAsync(organisation, NoteDate);
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            $"/api/reports/daily/{Date}/export{query}", TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // The isodate route constraint (§6.1): a segment that is not a real date matches no route.
    [Fact]
    public async Task A_date_that_is_not_a_date_gets_404()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            "/api/reports/daily/2026-13-45/export?format=docx", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // §5.3: the event is committed before the file is sent, so a download whose event cannot be written sends no file.
    [Fact]
    public async Task A_download_whose_event_cannot_be_written_gets_500_and_no_file()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        await SubmitANoteAsync(organisation, NoteDate);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
            services.AddScoped<IAuditWriter, ThrowingAuditWriter>();
        }));
        using var client = app.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(
            $"/api/reports/daily/{Date}/export?format=docx", TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Null(response.Content.Headers.ContentDisposition);
        Assert.Empty(await EventsOfAsync(organisation));
    }

    // Adds a participant of the organisation's own with a note for the day, submitted by its worker, as Submit leaves
    // it. The times are made up (A35).
    private async Task SubmitANoteAsync(SeededOrganisation organisation, DateOnly noteDate)
    {
        var participant = await factory.Services.AddParticipantAsync(organisation);
        await factory.Services.AddSubmittedAsync(
            organisation,
            new(participant.Id, noteDate, organisation.WorkerId, StartedAtUtc, SubmittedAtUtc));
    }

    private static async Task AssertNoNotesAsync(HttpClient client, string date, string format)
    {
        using var response = await client.GetAsync(
            $"/api/reports/daily/{date}/export?format={format}", TestContext.Current.CancellationToken);

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
