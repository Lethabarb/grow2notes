using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>POST {base}/discard</c>, Discard draft (design.md §3.4, §5.5 item 5, §5.8 <i>Discard</i>, §6.3), called with the
/// test-only sign-in (<see cref="TestSignIn"/>): for the draft's author or a manager, the note Discarded with who
/// discarded it and when, its content and working copy kept, and <c>note.discarded</c>, together or not at all, and
/// <c>204</c>, after which the day is free for a new note (§14 M2); and, changing nothing, a <c>403</c> problem with no
/// code for another worker, <c>409 note.not_a_draft</c> for a submitted note or a draft discarded already, also by a
/// discard that commits while this one runs, and <c>404</c> for a participant not in the caller's organisation (§9.2)
/// or a day with no note. Nothing is deleted (A29). The tests add their own participants, users and notes
/// (tests/README.md). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out
/// caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteDiscardEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string BaseRoute = "/api/participants/{participantId:guid}/notes/{noteDate:isodate}";

    // Made up (A35): a draft for Friday 2 October 2026, started at 9:14 am and last saved at 9:30 am in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SavedAtUtc = new(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);

    // 9:42 am on the note's date in Melbourne, with a part of a millisecond, which a datetime2(3) column cannot hold.
    private static readonly DateTimeOffset DiscardedAt =
        new DateTimeOffset(2026, 10, 1, 23, 42, 0, 123, TimeSpan.Zero).AddTicks(4567);

    private static readonly DateTime DiscardedAtUtc = new(2026, 10, 1, 23, 42, 0, 123, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // The header's one conditional update sets the status, the time and who, and nothing else, so the note keeps its
    // date, author and start time; the working copy keeps every column, its snapshot and text included (design.md
    // §5.3); and the event holds IDs alone, no note text (§9.5). The time stored is the clock's to the millisecond.
    [Fact]
    public async Task The_author_s_discard_leaves_the_note_discarded_with_who_and_when_its_working_copy_and_one_event()
    {
        await using var app = AppAtTheDiscard();
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goal = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 0));
        var noteId = await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            [new(NoteSection.Goal, goal.Id, null, "Makes own breakfast", true)],
            "Jane made toast.",
            SavedAtUtc));
        var before = await StoredRowsAsync();
        var workingCopy = await StoredWorkingCopyAsync(noteId);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await DiscardAsync(client, participant.Id, NoteDate);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            new NoteRow(
                (byte)NoteStatus.Discarded, 0, null, DiscardedAtUtc, seeded.A.WorkerId, NoteDate, StartedAtUtc,
                seeded.A.WorkerId),
            await StoredNoteAsync(noteId));
        Assert.Equal(workingCopy, await StoredWorkingCopyAsync(noteId));
        Assert.Equal(
            new WrittenEvent(
                AuditEventTypes.Note.Discarded, seeded.A.OrganisationId, seeded.A.WorkerId, AuditEntityTypes.Note,
                noteId, participant.Id, null),
            Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0)));
        var after = await StoredRowsAsync();
        Assert.Equal(
            (before.Notes, before.DiscardedNotes + 1, before.Drafts, before.Versions),
            (after.Notes, after.DiscardedNotes, after.Drafts, after.Versions));
    }

    // design.md §3.4 and §3.8: a manager may discard any draft, one left by a deactivated worker among them (§3.7). The
    // note records the manager as who discarded it, and so does the event, its actor; the author stays the worker.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_manager_s_discard_of_a_worker_s_draft_records_the_manager(bool deactivated)
    {
        await using var app = AppAtTheDiscard();
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        if (deactivated)
        {
            await DeactivateAsync(author);
        }

        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, author, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        var workingCopy = await StoredWorkingCopyAsync(noteId);
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await DiscardAsync(client, participant.Id, NoteDate);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            new NoteRow(
                (byte)NoteStatus.Discarded, 0, null, DiscardedAtUtc, seeded.A.ManagerId, NoteDate, StartedAtUtc,
                author),
            await StoredNoteAsync(noteId));
        Assert.Equal(workingCopy, await StoredWorkingCopyAsync(noteId));
        Assert.Equal(
            new WrittenEvent(
                AuditEventTypes.Note.Discarded, seeded.A.OrganisationId, seeded.A.ManagerId, AuditEntityTypes.Note,
                noteId, participant.Id, null),
            Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0)));
    }

    // Only the author or a manager may discard a draft (design.md §3.4). §6.9 has no code for another worker, whom no
    // screen offers Discard draft (T12's Notes), so the problem has none, and names nothing of the draft.
    [Fact]
    public async Task Another_worker_s_discard_is_refused_with_a_403_problem_with_no_code_and_changes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, author, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await DiscardAsync(client, participant.Id, NoteDate);

        var body = await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        using var problem = JsonDocument.Parse(body);
        Assert.False(problem.RootElement.TryGetProperty(ErrorCode.Extension, out _));
        Assert.DoesNotContain("Alex P.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Jane made toast.", body, StringComparison.Ordinal);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // design.md §6.9: a draft already discarded is not a draft. The day has no live note then, and the code is the
    // same for the author and a manager, and for another worker, who would be refused only a draft (T12's Notes). The
    // note keeps who discarded it first and when.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task A_second_discard_is_refused_as_not_a_draft_and_changes_nothing(Caller caller)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        using (var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded))
        using (var first = await DiscardAsync(worker, participant.Id, NoteDate))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await DiscardAsync(client, participant.Id, NoteDate);

        await ReadNotADraftAsync(response);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // design.md §5.5 item 5: a submitted note can never be discarded, by its author or a manager.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task A_discard_of_a_submitted_note_is_refused_as_not_a_draft_and_changes_nothing(Caller caller)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SavedAtUtc));
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await DiscardAsync(client, participant.Id, NoteDate);

        await ReadNotADraftAsync(response);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // Two discards at once, as when the author and a manager press Discard draft together: the author's is held just
    // before the header's conditional update, having found the draft, while the manager's discards it. The author's
    // update then changes no row (design.md §5.8 Discard), so it writes no event of its own.
    [Fact]
    public async Task Two_discards_at_once_discard_the_draft_once_and_the_later_is_refused_as_not_a_draft()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        var gate = new HoldsTheHeaderUpdateOf(noteId);
        await using var gated = gate.On(factory);
        using var worker = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var manager = gated.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        var held = DiscardAsync(worker, participant.Id, NoteDate);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await DiscardAsync(manager, participant.Id, NoteDate);
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await held;
        await ReadNotADraftAsync(response);
        var note = await StoredNoteAsync(noteId);
        Assert.Equal(((byte)NoteStatus.Discarded, seeded.A.ManagerId), (note.Status, note.DiscardedByUserId));
        Assert.Equal(seeded.A.ManagerId, Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0)).ActorUserId);
    }

    // The header's conditional update runs before the event, so only the transaction's rollback takes it away.
    [Fact]
    public async Task A_discard_whose_event_fails_to_save_changes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(
                options => options.AddInterceptors(new FailsTheEventsOf(participant.Id)))));
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await DiscardAsync(client, participant.Id, NoteDate);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // design.md §5.5 items 1 and 5, and §14 M2's "after a discard, a new note can": whether its author or a manager
    // discarded it, the draft is on no read, not the note's address, Today or Past notes, and the form opens as a new
    // note's, whose first save starts a new draft for the same participant and day, the one-live-note index letting
    // the discarded one stay beside it. On a clock at the note's date, which Today and the first save read.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task After_a_discard_the_draft_is_on_no_read_and_a_first_save_starts_a_new_draft_beside_it(
        Caller discarder)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var app = AppAtTheDiscard();
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await StartAsync(client, participant.Id, "Jane made toast.");
        var discardedId = Assert.Single(await NotesAsync(participant.Id)).Id;
        using (var discarding = app.CreateHttpsClient().SignInAs(discarder, seeded))
        using (var discarded = await DiscardAsync(discarding, participant.Id, NoteDate))
        {
            Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);
        }

        using (var read = await client.GetAsync(NotePath(participant.Id, NoteDate), cancellationToken))
        {
            await read.ReadProblemAsync(HttpStatusCode.NotFound);
        }

        var today = await client.GetFromJsonAsync<JsonElement>("/api/today", cancellationToken);
        Assert.Equal("2026-10-02", today.GetProperty("date").GetString());
        var row = today.GetProperty("participants").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetGuid() == participant.Id);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("note").ValueKind);
        var history = await client.GetFromJsonAsync<JsonElement>(
            $"/api/participants/{participant.Id}/notes", cancellationToken);
        Assert.Equal(0, history.GetProperty("notes").GetArrayLength());

        await StartAsync(client, participant.Id, "Sam made toast.");

        var notes = await NotesAsync(participant.Id);
        Assert.Equal(2, notes.Count);
        var discardedNote = Assert.Single(notes, note => note.Id == discardedId);
        var started = Assert.Single(notes, note => note.Id != discardedId);
        Assert.Equal(
            (NoteStatus.Discarded, NoteDate, NoteStatus.Draft, NoteDate),
            (discardedNote.Status, discardedNote.NoteDate, started.Status, started.NoteDate));
        Assert.Equal("Jane made toast.", (await StoredWorkingCopyAsync(discardedId)).Narrative);
        Assert.Equal("Sam made toast.", (await StoredWorkingCopyAsync(started.Id)).Narrative);
    }

    // A29: the app has no hard delete, so it maps no DELETE on a note, and a DELETE there gets the unknown path's 404,
    // leaving the draft live; the discard itself keeps the note and its working copy (above).
    [Fact]
    public async Task The_app_maps_no_DELETE_on_a_note_and_a_DELETE_there_deletes_nothing()
    {
        var routesTakingDelete = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => EndpointMatrix.MethodsOf(endpoint).Contains(HttpMethods.Delete))
            .Select(endpoint => endpoint.RoutePattern.RawText);
        Assert.DoesNotContain(BaseRoute, routesTakingDelete);
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var request = new HttpRequestMessage(HttpMethod.Delete, NotePath(participant.Id, NoteDate));
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // design.md §9.2: B's manager does not find A's participant; and nobody finds a participant that does not exist,
    // or a note for a day that has none.
    [Fact]
    public async Task A_discard_for_B_s_manager_at_A_s_draft_a_day_with_no_note_or_no_participant_is_not_found()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var b = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfB, seeded);
        using var a = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var other = await DiscardAsync(b, participant.Id, NoteDate);
        using var noNote = await DiscardAsync(a, participant.Id, NoteDate.AddDays(-1));
        using var none = await DiscardAsync(a, Guid.NewGuid(), NoteDate);

        await other.ReadProblemAsync(HttpStatusCode.NotFound);
        await noNote.ReadProblemAsync(HttpStatusCode.NotFound);
        await none.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    private static string NotePath(Guid participantId, DateOnly noteDate) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}");

    // Over HTTPS with the caller's antiforgery token and no body, as the screen discards (tests/README.md).
    private static async Task<HttpResponseMessage> DiscardAsync(
        HttpClient client, Guid participantId, DateOnly noteDate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{NotePath(participantId, noteDate)}/discard");
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // A 409 note.not_a_draft (design.md §6.9), which, unlike note.in_progress_by_other, names no author.
    private static async Task ReadNotADraftAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        Assert.Equal("note.not_a_draft", problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
        Assert.False(problem.RootElement.TryGetProperty("authorDisplayName", out _));
    }

    // The participant's note for NoteDate started by the client's first save, which the client's clock must take for
    // that day, as the form sends it: from a page of its own, with the template's lists' version, the participant
    // having no goals.
    private static async Task StartAsync(HttpClient client, Guid participantId, string narrative)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = $"{NotePath(participantId, NoteDate)}/draft";
        var form = await client.GetFromJsonAsync<JsonElement>(path, cancellationToken);
        Assert.False(form.GetProperty("isSaved").GetBoolean());
        using var started = await client.PutWithTokenAsync(
            path,
            await client.FetchTokenAsync(),
            new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion = form.GetProperty("listsVersion").GetString(),
                narrative,
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
    }

    // The app on a clock at DiscardedAt, which the discard stores as its time, and Today and a first save read.
    private WebApplicationFactory<Program> AppAtTheDiscard() =>
        factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(DiscardedAt)));

    // The user deactivated as deactivation leaves them (§8.6), through the app's UserManager, a user the test added, as
    // no test deactivates a seeded user (tests/README.md).
    private async Task DeactivateAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.Status = UserStatus.Deactivated;
        user.DeactivatedAtUtc = SavedAtUtc;
        var updated = await users.UpdateAsync(user);
        Assert.True(updated.Succeeded, string.Join(" ", updated.Errors.Select(error => error.Code)));
    }

    // The participant's notes, live and discarded, through the app's context under A's tenant.
    private async Task<List<Note>> NotesAsync(Guid participantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Notes.AsNoTracking()
                .Where(note => note.ParticipantId == participantId)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // Each read below is in SQL from a scope of its own, so that neither the query filter, which would need a tenant,
    // nor a context's tracked rows decide what is seen.
    private async Task<NoteRow> StoredNoteAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<NoteRow>($"""
                SELECT [Status], [CurrentVersionNumber], [FirstSubmittedAtUtc], [DiscardedAtUtc], [DiscardedByUserId],
                       [NoteDate], [CreatedAtUtc], [AuthorUserId]
                FROM [Note]
                WHERE [Id] = {noteId}
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // The note's one working copy, every column, with its snapshots as the JSON stored.
    private async Task<WorkingCopyRow> StoredWorkingCopyAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<WorkingCopyRow>($"""
                SELECT [NoteId], [EditorUserId], [OrganisationId], [BaseVersionNumber], [Groups], [Items], [Narrative],
                       [IsFlagged], [FlagReason], [LastClientId], [LastSeq], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [NoteDraft]
                WHERE [NoteId] = {noteId}
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // The tests of the collection run one at a time, so the events after the one given are the test's alone.
    private async Task<List<WrittenEvent>> EventsAfterAsync(long eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<WrittenEvent>($"""
                SELECT [EventType], [OrganisationId], [ActorUserId], [EntityType], [EntityId], [ParticipantId],
                       [Details]
                FROM [AuditEvent]
                WHERE [Id] > {eventId}
                ORDER BY [Id]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // What every test has written, by any test: the tests of the collection run one at a time, so none writes a row
    // while a test reads them before and after its requests.
    private async Task<StoredRows> StoredRowsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<StoredRows>($"""
                SELECT (SELECT COUNT(*) FROM [Note]) AS [Notes],
                       (SELECT COUNT(*) FROM [Note] WHERE [Status] = 2) AS [DiscardedNotes],
                       (SELECT COUNT(*) FROM [NoteDraft]) AS [Drafts],
                       (SELECT COUNT(*) FROM [NoteVersion]) AS [Versions],
                       (SELECT MAX([Id]) FROM [AuditEvent]) AS [LastEventId]
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// How many notes there are, and how many discarded, how many working copies and versions, and the last event's ID.
    /// </summary>
    private sealed record StoredRows(int Notes, int DiscardedNotes, int Drafts, int Versions, long? LastEventId);

    /// <summary>A <c>Note</c> row's header, its status as its stored <c>tinyint</c>.</summary>
    private sealed record NoteRow(
        byte Status,
        int CurrentVersionNumber,
        DateTime? FirstSubmittedAtUtc,
        DateTime? DiscardedAtUtc,
        Guid? DiscardedByUserId,
        DateOnly NoteDate,
        DateTime CreatedAtUtc,
        Guid AuthorUserId);

    /// <summary>A <c>NoteDraft</c> row, its snapshots as the JSON stored.</summary>
    private sealed record WorkingCopyRow(
        Guid NoteId,
        Guid EditorUserId,
        Guid OrganisationId,
        int BaseVersionNumber,
        string Groups,
        string Items,
        string Narrative,
        bool IsFlagged,
        string? FlagReason,
        Guid LastClientId,
        int LastSeq,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    /// <summary>An audit entry's fields, its <c>Details</c> as the JSON stored.</summary>
    private sealed record WrittenEvent(
        string EventType,
        Guid OrganisationId,
        Guid? ActorUserId,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);

    /// <summary>Fails each save that adds an audit event of the participant's, before it is sent.</summary>
    private sealed class FailsTheEventsOf(Guid participantId) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var addsAnEvent = eventData.Context?.ChangeTracker.Entries<AuditEvent>().Any(
                entry => entry.State == EntityState.Added && entry.Entity.ParticipantId == participantId) == true;
            return addsAnEvent ? throw new EventNotSavedException() : ValueTask.FromResult(result);
        }
    }

    private sealed class EventNotSavedException() : Exception("The audit event was not saved.");
}
