using System.Data.SqlTypes;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Today;

/// <summary>
/// <c>GET /api/today</c> (design.md §6.3), called with the test-only sign-in (<see cref="TestSignIn"/>): every
/// signed-in user gets the date in Melbourne and their own organisation's active participants, and none of the other's
/// (§5.9 item 7), sorted by family name, then given name (A5), then ID, each with their live note for the day as the
/// caller sees it, or null (§4.2 item 4), as JSON that is never stored (§6.1), and no call writes an audit
/// event (A28). The tests add their own participants, users and notes to the seeded organisations, and change no
/// seeded row (tests/README.md). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/>
/// what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class TodayEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Path = "/api/today";

    // Made up (A35), with milliseconds, which datetime2(3) keeps.
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);

    // 4:30 pm on Thursday 1 October 2026 in Melbourne, still in standard time (UTC+10), when the tests of the day's
    // notes ask for the list.
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 6, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 10, 1);

    // Made up (A35): a note started at 9:14 am, last saved at 9:42 am and submitted at 4:12 pm that day in Melbourne.
    private static readonly DateTime StartedAtUtc = new(2026, 9, 30, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SavedAtUtc = new(2026, 9, 30, 23, 42, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 6, 12, 0, DateTimeKind.Utc);

    // SQL Server's order of uniqueidentifier values, which compares their last six bytes first, unlike Guid's.
    private static readonly Comparer<Guid> SqlServerGuidOrder =
        Comparer<Guid>.Create((x, y) => new SqlGuid(x).CompareTo(new SqlGuid(y)));

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    [Fact]
    public async Task A_s_worker_and_manager_get_the_same_list_of_A_s_active_participants_and_none_of_B_s()
    {
        var added = await AddAsync(seeded.A, ("Jane", "Citizen", ParticipantStatus.Active));

        var worker = await ListAsync(Caller.WorkerOfA);
        var manager = await ListAsync(Caller.ManagerOfA);

        // The same people, whose notes, which other tests start, differ only in what is the caller's own.
        Assert.Equal(People(worker), People(manager));
        var ids = ParticipantIds(worker);
        Assert.Equal((await StoredActiveIdsAsync(seeded.A)).Order(), ids.Order());
        Assert.Contains(seeded.A.ParticipantId, ids);
        Assert.Contains(added[0], ids);
        Assert.DoesNotContain(seeded.B.ParticipantId, ids);
    }

    [Fact]
    public async Task B_s_manager_gets_B_s_active_participants_and_none_of_A_s()
    {
        var addedToA = await AddAsync(seeded.A, ("Jane", "Citizen", ParticipantStatus.Active));
        var addedToB = await AddAsync(seeded.B, ("Priya", "Kumar", ParticipantStatus.Active));

        var ids = ParticipantIds(await ListAsync(Caller.ManagerOfB));

        Assert.Equal((await StoredActiveIdsAsync(seeded.B)).Order(), ids.Order());
        Assert.Contains(seeded.B.ParticipantId, ids);
        Assert.Contains(addedToB[0], ids);
        Assert.DoesNotContain(seeded.A.ParticipantId, ids);
        Assert.DoesNotContain(addedToA[0], ids);
    }

    [Fact]
    public async Task An_archived_participant_is_left_out()
    {
        var added = await AddAsync(
            seeded.A, ("Sam", "Active", ParticipantStatus.Active), ("Sam", "Archived", ParticipantStatus.Archived));

        var ids = ParticipantIds(await ListAsync(Caller.WorkerOfA));

        Assert.Contains(added[0], ids);
        Assert.DoesNotContain(added[1], ids);
    }

    // By family name, then given name, in SQL Server's collation, which ignores case, so "de Silva" sorts between
    // "Citizen" and "Ng", where an ordinal sort would put it last; then two people of one name by ID, in SQL Server's
    // order of IDs. Neither the order they were added in nor given names first gives the same order.
    [Fact]
    public async Task The_list_is_sorted_by_family_name_then_given_name_then_ID()
    {
        var added = await AddAsync(
            seeded.A,
            ("Adam", "Ng", ParticipantStatus.Active),
            ("Zoë", "de Silva", ParticipantStatus.Active),
            ("Jane", "Citizen", ParticipantStatus.Active),
            ("Bea", "Citizen", ParticipantStatus.Active),
            ("Sam", "Twin", ParticipantStatus.Active),
            ("Sam", "Twin", ParticipantStatus.Active));

        var ids = ParticipantIds(await ListAsync(Caller.WorkerOfA));

        Assert.Equal(
            [added[3], added[2], added[1], added[0], .. added[4..].Order(SqlServerGuidOrder)],
            ids.Where(added.Contains));
    }

    // design.md §6.3's shape: the names as typed, accents and all, and never the date of birth or the status; and the
    // note, written as null for a participant with none (A17).
    [Fact]
    public async Task The_answer_has_the_date_and_each_participant_s_ID_names_as_stored_and_note()
    {
        var added = await AddAsync(seeded.A, ("Nguyễn", "O’Brien-Smith", ParticipantStatus.Active));

        var list = await ListAsync(Caller.WorkerOfA);

        Assert.Equal(["date", "participants"], list.EnumerateObject().Select(property => property.Name));
        var participant = Row(list, added[0]);
        Assert.Equal(
            ["id", "givenName", "familyName", "note"], participant.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Nguyễn", participant.GetProperty("givenName").GetString());
        Assert.Equal("O’Brien-Smith", participant.GetProperty("familyName").GetString());
        Assert.Equal(JsonValueKind.Null, participant.GetProperty("note").ValueKind);
    }

    // A discarded draft is not live (§5.5 item 1): alone it leaves the day with no note, and beside a note started
    // since, the new one is the participant's note.
    [Fact]
    public async Task A_discarded_draft_gives_null_alone_and_the_new_note_beside_it()
    {
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var alone = await factory.Services.AddParticipantAsync(seeded.A);
        var startedAgain = await factory.Services.AddParticipantAsync(seeded.A);
        foreach (var participant in new[] { alone, startedAgain })
        {
            var noteId = await factory.Services.AddDraftAsync(
                seeded.A, new(participant.Id, Today, author, StartedAtUtc, Narrative: "Jane made toast."));
            await factory.Services.DiscardAsync(seeded.A, noteId, SavedAtUtc);
        }

        await factory.Services.AddDraftAsync(
            seeded.A,
            new(startedAgain.Id, Today, seeded.A.WorkerId, SavedAtUtc.AddMinutes(5), SavedAtUtc: SubmittedAtUtc));
        await using var app = AppAtNow();

        var list = await ListAsync(app, Caller.WorkerOfA);

        Assert.Equal(JsonValueKind.Null, NoteOf(list, alone.Id).ValueKind);
        AssertNote(
            new("Draft", "Casey Nguyen", true, "2026-09-30T23:47:00Z", "2026-10-01T06:12:00Z", null, false),
            NoteOf(list, startedAgain.Id));
    }

    // The same row for a worker and a manager (§4.2 item 4): who started the draft, and when, and nothing of what it
    // says (A9), or of when its author last saved it.
    [Fact]
    public async Task Another_user_s_draft_gives_a_worker_and_a_manager_its_author_s_name_and_start_time_alone()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        await factory.Services.AddDraftAsync(
            seeded.A,
            new(participant.Id, Today, author, StartedAtUtc, Narrative: "Jane made toast.", SavedAtUtc: SavedAtUtc));
        await using var app = AppAtNow();

        foreach (var caller in new[] { Caller.WorkerOfA, Caller.ManagerOfA })
        {
            var list = await ListAsync(app, caller);

            AssertNote(
                new("Draft", "Alex P.", false, "2026-09-30T23:14:00Z", null, null, false),
                NoteOf(list, participant.Id));
            Assert.DoesNotContain("Jane made toast.", list.GetRawText(), StringComparison.Ordinal);
        }
    }

    // When the caller last saved their own draft, from their working copy, which a manager reading the same row never
    // gets.
    [Fact]
    public async Task The_caller_s_own_draft_gives_when_they_last_saved_it()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, Today, seeded.A.WorkerId, StartedAtUtc, SavedAtUtc: SavedAtUtc));
        await using var app = AppAtNow();

        var mine = NoteOf(await ListAsync(app, Caller.WorkerOfA), participant.Id);
        var theirs = NoteOf(await ListAsync(app, Caller.ManagerOfA), participant.Id);

        AssertNote(
            new("Draft", "Casey Nguyen", true, "2026-09-30T23:14:00Z", "2026-09-30T23:42:00Z", null, false), mine);
        AssertNote(new("Draft", "Casey Nguyen", false, "2026-09-30T23:14:00Z", null, null, false), theirs);
    }

    // The first submitted time, and never the time of a save: its author has a working copy of it too, a pending edit
    // (§5.6), added by hand until T14 adds them, which is not a draft.
    [Fact]
    public async Task A_submitted_note_gives_its_first_submitted_time()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, Today, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        await AddPendingEditAsync(noteId, seeded.A.WorkerId);
        await using var app = AppAtNow();

        var mine = NoteOf(await ListAsync(app, Caller.WorkerOfA), participant.Id);
        var theirs = NoteOf(await ListAsync(app, Caller.ManagerOfA), participant.Id);

        AssertNote(
            new("Submitted", "Casey Nguyen", true, "2026-09-30T23:14:00Z", null, "2026-10-01T06:12:00Z", false), mine);
        AssertNote(
            new("Submitted", "Casey Nguyen", false, "2026-09-30T23:14:00Z", null, "2026-10-01T06:12:00Z", false),
            theirs);
    }

    // The current version's flag (§4.2 item 4), not the review state (A15): flagged while FlagStatus is Reviewed, as a
    // review leaves it, and not while it is ToReview over a version not flagged, as an edit that unticks the flag
    // leaves it, each set by hand. The reason stays off the row.
    [Fact]
    public async Task IsFlagged_is_the_current_version_s_flag_not_the_flag_status()
    {
        var flagged = await factory.Services.AddParticipantAsync(seeded.A);
        var toReview = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(
            seeded.A,
            new(
                flagged.Id, Today, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc,
                IsFlagged: true, FlagReason: "Jane seemed unwell.", FlagStatus: FlagStatus.Reviewed));
        await factory.Services.AddSubmittedAsync(
            seeded.A,
            new(toReview.Id, Today, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc, FlagStatus: FlagStatus.ToReview));
        await using var app = AppAtNow();

        var list = await ListAsync(app, Caller.ManagerOfA);

        Assert.True(NoteOf(list, flagged.Id).GetProperty("isFlagged").GetBoolean());
        Assert.False(NoteOf(list, toReview.Id).GetProperty("isFlagged").GetBoolean());
        Assert.DoesNotContain("Jane seemed unwell.", list.GetRawText(), StringComparison.Ordinal);
    }

    // The list's date's note alone: yesterday's, submitted or a draft still open, is not today's.
    [Fact]
    public async Task A_note_of_another_day_is_left_out()
    {
        var submitted = await factory.Services.AddParticipantAsync(seeded.A);
        var draft = await factory.Services.AddParticipantAsync(seeded.A);
        var yesterday = Today.AddDays(-1);
        await factory.Services.AddSubmittedAsync(
            seeded.A,
            new(submitted.Id, yesterday, seeded.A.WorkerId, StartedAtUtc.AddDays(-1), SubmittedAtUtc.AddDays(-1)));
        await factory.Services.AddDraftAsync(
            seeded.A, new(draft.Id, yesterday, seeded.A.WorkerId, StartedAtUtc.AddDays(-1)));
        await using var app = AppAtNow();

        var list = await ListAsync(app, Caller.WorkerOfA);

        Assert.Equal(JsonValueKind.Null, NoteOf(list, submitted.Id).ValueKind);
        Assert.Equal(JsonValueKind.Null, NoteOf(list, draft.Id).ValueKind);
    }

    [Fact]
    public async Task The_answer_is_JSON_that_is_never_stored()
    {
        using var client = ClientFor(Caller.WorkerOfA);

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
    }

    // design.md §5.4 lists no event for reading (A28).
    [Fact]
    public async Task The_calls_write_no_audit_event()
    {
        var lastEventId = await LastEventIdAsync();

        foreach (var caller in new[] { Caller.WorkerOfA, Caller.ManagerOfA, Caller.ManagerOfB })
        {
            await ListAsync(caller);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // 14:30 UTC on 3 October is half past midnight on 4 October in Melbourne, still in standard time (UTC+10), as
    // daylight saving starts at 2 am that day.
    [Fact]
    public async Task The_date_is_today_in_Melbourne_where_it_differs_from_the_date_in_UTC()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 14, 30, 0, TimeSpan.Zero));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        var list = await ReadListAsync(client);

        Assert.Equal("2026-10-04", list.GetProperty("date").GetString());
    }

    private static Guid Id(JsonElement participant) => participant.GetProperty("id").GetGuid();

    // The list's participants' IDs, in its order.
    private static List<Guid> ParticipantIds(JsonElement list) =>
        [.. list.GetProperty("participants").EnumerateArray().Select(Id)];

    // The list's participants, in its order, as their IDs and names, without their notes.
    private static List<(Guid Id, string? GivenName, string? FamilyName)> People(JsonElement list) =>
    [
        .. list.GetProperty("participants").EnumerateArray().Select(row => (
            Id(row), row.GetProperty("givenName").GetString(), row.GetProperty("familyName").GetString())),
    ];

    private static JsonElement Row(JsonElement list, Guid participantId) =>
        list.GetProperty("participants").EnumerateArray().Single(row => Id(row) == participantId);

    private static JsonElement NoteOf(JsonElement list, Guid participantId) =>
        Row(list, participantId).GetProperty("note");

    // The note as its row has it: design.md §6.3's fields, in its order, with no key for a time that the row does not
    // give, and nothing more, each time in UTC, written with Z.
    private static void AssertNote(NoteFields expected, JsonElement note)
    {
        string?[] keys =
        [
            "status", "authorDisplayName", "isMine", "startedAtUtc",
            expected.SavedAtUtc is null ? null : "savedAtUtc",
            expected.FirstSubmittedAtUtc is null ? null : "firstSubmittedAtUtc",
            "isFlagged",
        ];
        Assert.Equal(keys.OfType<string>(), note.EnumerateObject().Select(property => property.Name));
        Assert.Equal(expected, note.Deserialize<NoteFields>(JsonSerializerOptions.Web));
    }

    private static async Task<JsonElement> ReadListAsync(HttpClient client)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(Path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    private async Task<JsonElement> ListAsync(Caller caller)
    {
        using var client = ClientFor(caller);
        return await ReadListAsync(client);
    }

    // The app on a clock that says Now, so that the list is the notes' date's whenever the test runs.
    private WebApplicationFactory<Program> AppAtNow() =>
        factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));

    private async Task<JsonElement> ListAsync(WebApplicationFactory<Program> app, Caller caller)
    {
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);
        var list = await ReadListAsync(client);
        Assert.Equal("2026-10-01", list.GetProperty("date").GetString());
        return list;
    }

    /// <summary>
    /// Adds <paramref name="editorUserId"/>'s working copy of the submitted note of ID <paramref name="noteId"/> in A,
    /// on its version 1, as Edit leaves one (design.md §5.6), saved after the note was submitted.
    /// </summary>
    private async Task AddPendingEditAsync(Guid noteId, Guid editorUserId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.NoteDrafts.Add(new NoteDraft
        {
            NoteId = noteId,
            EditorUserId = editorUserId,
            OrganisationId = seeded.A.OrganisationId,
            BaseVersionNumber = 1,
            Narrative = "Jane made toast, then walked.",
            LastClientId = Guid.NewGuid(),
            LastSeq = 1,
            CreatedAtUtc = SubmittedAtUtc.AddMinutes(1),
            UpdatedAtUtc = SubmittedAtUtc.AddMinutes(2),
        });
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Adds the participants to <paramref name="organisation"/>, through the app's context under its tenant, as the app
    /// adds them, each born on 7 March 1987, and an archived one archived as Archive leaves them, and gives their IDs
    /// in the order given.
    /// </summary>
    private async Task<List<Guid>> AddAsync(
        SeededOrganisation organisation,
        params (string GivenName, string FamilyName, ParticipantStatus Status)[] people)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var participants = people.Select(person => new Participant
        {
            OrganisationId = organisation.OrganisationId,
            GivenName = person.GivenName,
            FamilyName = person.FamilyName,
            DateOfBirth = new DateOnly(1987, 3, 7),
            Status = person.Status,
            ArchivedAtUtc = person.Status == ParticipantStatus.Archived ? CreatedAtUtc.AddDays(1) : null,
            CreatedAtUtc = CreatedAtUtc,
        }).ToList();
        db.Participants.AddRange(participants);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return [.. participants.Select(participant => participant.Id)];
    }

    // The organisation's active participants. The tests of the collection run one at a time, so no participant is added
    // between this and the call it is compared with.
    private async Task<List<Guid>> StoredActiveIdsAsync(SeededOrganisation organisation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            return await db.Participants
                .Where(p => p.Status == ParticipantStatus.Active)
                .Select(p => p.Id)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
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

    /// <summary>A row's note, its times as the JSON writes them, and those it does not give null.</summary>
    private sealed record NoteFields(
        string Status,
        string AuthorDisplayName,
        bool IsMine,
        string StartedAtUtc,
        string? SavedAtUtc,
        string? FirstSubmittedAtUtc,
        bool IsFlagged);
}
