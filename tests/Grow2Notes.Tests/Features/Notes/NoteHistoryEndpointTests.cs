using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET /api/participants/{participantId}/notes?before=</c> (design.md §6.3), a participant's history, called with
/// the test-only sign-in (<see cref="TestSignIn"/>): their live notes dated before <c>before</c>, by default tomorrow
/// in Melbourne, newest first, 30 at a time with <c>hasMore</c> (§6.1 <i>Lists</i>), never a discarded draft (§5.5
/// item 1), each row's status, author and flag from its current version, and its flag status for a manager alone;
/// an archived participant's too (§3.7); <c>404</c> for another organisation's participant (§9.2); as JSON that is
/// never stored (§6.1), and reading writes nothing (A28). The tests add their own participants, users and notes
/// (tests/README.md). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out
/// caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteHistoryEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // 4:30 pm on Thursday 1 October 2026 in Melbourne, still in standard time (UTC+10), when the tests ask for the
    // first page, which then starts at the day's note.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 10, 1);

    // Made up (A35): a note started at 9:14 am and submitted at 4:42 pm on 30 September in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 9, 30);

    private static readonly DateTime StartedAtUtc = new(2026, 9, 29, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 9, 30, 6, 42, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // design.md §4.4 item 2: 30 at a time, then the next 30 before the last row's date, until none remain.
    [Fact]
    public async Task The_first_page_gives_the_newest_30_today_s_first_then_the_next_gives_the_31st_and_no_more()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var dates = Enumerable.Range(0, 31).Select(daysBefore => Today.AddDays(-daysBefore)).ToList();
        foreach (var date in dates)
        {
            var startedAtUtc = date.ToDateTime(new TimeOnly(0, 14), DateTimeKind.Utc);
            await factory.Services.AddSubmittedAsync(
                seeded.A,
                new(participant.Id, date, seeded.A.WorkerId, startedAtUtc, startedAtUtc.AddHours(7)));
        }

        await using var app = AppAtNow();

        var first = await HistoryAsync(app, Caller.WorkerOfA, participant.Id);

        Assert.Equal(dates[..30], NoteDates(first));
        Assert.True(first.GetProperty("hasMore").GetBoolean());

        var next = await HistoryAsync(app, Caller.WorkerOfA, participant.Id, before: dates[29]);

        Assert.Equal([dates[30]], NoteDates(next));
        Assert.False(next.GetProperty("hasMore").GetBoolean());
    }

    // A discarded draft is not live (§5.5 item 1): its day shows no row, or the live note started after it.
    [Fact]
    public async Task A_discarded_draft_is_never_listed_alone_or_beside_the_live_note_of_its_day()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var alone = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate.AddDays(-1), seeded.A.WorkerId, StartedAtUtc.AddDays(-1)));
        await factory.Services.DiscardAsync(seeded.A, alone, StartedAtUtc.AddDays(-1).AddHours(1));
        var replaced = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.ManagerId, StartedAtUtc));
        await factory.Services.DiscardAsync(seeded.A, replaced, StartedAtUtc.AddHours(1));
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc.AddHours(2), SubmittedAtUtc));

        var history = await HistoryAsync(Caller.ManagerOfA, participant.Id);

        var row = Assert.Single(history.GetProperty("notes").EnumerateArray());
        Assert.Equal("2026-09-30", row.GetProperty("noteDate").GetString());
        Assert.Equal("Submitted", row.GetProperty("status").GetString());
        Assert.False(history.GetProperty("hasMore").GetBoolean());
    }

    // §6.3's fields that this task gives, in its order, and nothing of what the draft says (A9): its status and author
    // as stored, never flagged, as only a version is.
    [Fact]
    public async Task A_draft_row_gives_its_date_status_and_author_and_is_never_flagged()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Sam Lee");
        await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, author, StartedAtUtc, Narrative: "Jane made toast."));

        var history = await HistoryAsync(Caller.WorkerOfA, participant.Id);

        var row = Assert.Single(history.GetProperty("notes").EnumerateArray());
        Assert.Equal(
            ["noteDate", "status", "authorDisplayName", "isFlagged"],
            row.EnumerateObject().Select(property => property.Name));
        Assert.Equal("2026-09-30", row.GetProperty("noteDate").GetString());
        Assert.Equal("Draft", row.GetProperty("status").GetString());
        Assert.Equal("Sam Lee", row.GetProperty("authorDisplayName").GetString());
        Assert.False(row.GetProperty("isFlagged").GetBoolean());
        Assert.DoesNotContain("toast", history.GetRawText(), StringComparison.Ordinal);
    }

    // The flag is the current version's (§6.3), whatever the note's flag status, which only a version and a review
    // change (§5.8): flagged while it is Reviewed, set by hand, and not flagged while it is To review over an unflagged
    // version 2, added by hand as an edit that unticks the flag of a flagged version 1 leaves it.
    [Fact]
    public async Task Each_row_s_flag_is_its_current_version_s()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            IsFlagged: true,
            FlagReason: "Jane seemed unwell.",
            FlagStatus: FlagStatus.Reviewed));
        var toReview = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate.AddDays(-1),
            seeded.A.WorkerId,
            StartedAtUtc.AddDays(-1),
            SubmittedAtUtc.AddDays(-1),
            IsFlagged: true,
            FlagReason: "Jane seemed unwell."));
        await factory.Services.AddVersion2Async(
            seeded.A, toReview, SubmittedAtUtc.AddDays(-1).AddHours(1), flagReason: null, flaggedVersionNumber: 1);

        var history = await HistoryAsync(Caller.WorkerOfA, participant.Id);

        Assert.Equal([(NoteDate, true), (NoteDate.AddDays(-1), false)], NoteDates(history).Zip(Flags(history)));
        Assert.DoesNotContain("unwell", history.GetRawText(), StringComparison.Ordinal);
    }

    // An archived participant's notes are kept (§3.7), and anyone may read them (D20).
    [Fact]
    public async Task An_archived_participant_s_notes_are_listed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));

        var history = await HistoryAsync(Caller.WorkerOfA, participant.Id);

        Assert.Equal([NoteDate], NoteDates(history));
    }

    // §6.3: flagStatus for managers, by the caller's role as stored, and no key at all for a worker (A13, A14); None
    // for a note never flagged, and To review for a flagged one, as Submit leaves them.
    [Fact]
    public async Task A_manager_s_rows_carry_the_flag_status_where_a_worker_s_have_no_such_key()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            IsFlagged: true,
            FlagReason: "Jane seemed unwell."));
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate.AddDays(-1),
            seeded.A.WorkerId,
            StartedAtUtc.AddDays(-1),
            SubmittedAtUtc.AddDays(-1)));

        var manager = await HistoryAsync(Caller.ManagerOfA, participant.Id);
        var worker = await HistoryAsync(Caller.WorkerOfA, participant.Id);

        Assert.Equal(
            ["ToReview", "None"], Rows(manager).Select(row => row.GetProperty("flagStatus").GetString()));
        Assert.Equal(
            ["noteDate", "status", "authorDisplayName", "isFlagged", "flagStatus"],
            Rows(manager)[0].EnumerateObject().Select(property => property.Name));
        Assert.All(Rows(worker), row => Assert.False(row.TryGetProperty("flagStatus", out _)));
    }

    // §9.2: another organisation's participant, or none, is not found.
    [Fact]
    public async Task B_s_manager_gets_404_for_A_s_participant_and_anyone_gets_404_for_no_participant()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));

        foreach (var (caller, participantId) in new[]
        {
            (Caller.ManagerOfB, participant.Id),
            (Caller.ManagerOfA, Guid.NewGuid()),
        })
        {
            using var client = ClientFor(factory, caller);

            using var response = await client.GetAsync(PathOf(participantId), TestContext.Current.CancellationToken);

            var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
            Assert.DoesNotContain("2026-09-30", problem, StringComparison.Ordinal);
        }
    }

    // before is bound as a date, so the framework refuses a value that is not one.
    [Theory]
    [InlineData("yesterday")]
    [InlineData("2026-02-30")]
    public async Task A_before_that_is_not_a_date_gets_400(string before)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = ClientFor(factory, Caller.WorkerOfA);

        using var response = await client.GetAsync(
            $"{PathOf(participant.Id)}?before={before}", TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_answer_is_JSON_that_is_never_stored()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = ClientFor(factory, Caller.WorkerOfA);

        using var response = await client.GetAsync(PathOf(participant.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        Assert.Equal(
            """{"notes":[],"hasMore":false}""",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // design.md §5.4 lists no event for reading (A28).
    [Fact]
    public async Task Reading_writes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc));
        var lastEventId = await LastEventIdAsync();

        foreach (var caller in new[] { Caller.WorkerOfA, Caller.ManagerOfA })
        {
            await HistoryAsync(caller, participant.Id);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    private static string PathOf(Guid participantId) => $"/api/participants/{participantId}/notes";

    private static List<JsonElement> Rows(JsonElement history) => [.. history.GetProperty("notes").EnumerateArray()];

    // Whether each row is flagged, in the page's order.
    private static List<bool> Flags(JsonElement history) =>
        [.. Rows(history).Select(row => row.GetProperty("isFlagged").GetBoolean())];

    // The rows' note dates, in the page's order.
    private static List<DateOnly> NoteDates(JsonElement history) =>
    [
        .. Rows(history).Select(row =>
            DateOnly.ParseExact(row.GetProperty("noteDate").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)),
    ];

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(WebApplicationFactory<Program> app, Caller caller) =>
        app.CreateHttpsClient().SignInAs(caller, seeded);

    // The app on a clock that says Now, so that the first page starts at that day's note whenever the test runs.
    private WebApplicationFactory<Program> AppAtNow() =>
        factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));

    private Task<JsonElement> HistoryAsync(Caller caller, Guid participantId) =>
        HistoryAsync(factory, caller, participantId);

    private async Task<JsonElement> HistoryAsync(
        WebApplicationFactory<Program> app, Caller caller, Guid participantId, DateOnly? before = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = ClientFor(app, caller);
        var query = before is { } date
            ? string.Create(CultureInfo.InvariantCulture, $"?before={date:yyyy-MM-dd}")
            : "";

        using var response = await client.GetAsync(PathOf(participantId) + query, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after its requests.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
