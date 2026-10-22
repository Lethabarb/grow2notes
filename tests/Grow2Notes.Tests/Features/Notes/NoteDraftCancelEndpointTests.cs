using System.Globalization;
using System.Net;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>DELETE {base}/draft</c>, Cancel (design.md §6.3, A71), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): the caller's own pending edit of a submitted note deleted, and nothing else, and
/// <c>204</c>; otherwise <c>404</c>, for a caller with no pending edit, a draft, whose working copy is never deleted
/// (Discard is its own call), no note, a participant not in the caller's organisation (§9.2) or none, and never
/// <c>403</c>, as it removes nothing but the caller's own row. It writes no audit event (§5.4). The tests add their
/// own participants, users and notes (tests/README.md). The endpoint matrix covers who may call it, and
/// <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteDraftCancelEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a note for Friday 2 October 2026, started at 9:14 am and submitted at 9:30 am in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // The caller's pending edit goes, as a 204 with no body, and the worker's and the manager's are separate rows, so
    // the other editor's stays, with the note, its version and the audit log as they were.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task A_cancel_deletes_only_the_caller_s_pending_edit_and_changes_nothing_else(Caller caller)
    {
        var (participantId, noteId) = await AddSubmittedNoteAsync();
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var manager = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        await BeginEditAsync(worker, participantId);
        await BeginEditAsync(manager, participantId);
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        var other = caller == Caller.WorkerOfA ? seeded.A.ManagerId : seeded.A.WorkerId;

        using var response = await CancelAsync(caller == Caller.WorkerOfA ? worker : manager, participantId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal([other], await EditorsAsync(noteId));
        Assert.Equal(note, await StoredNoteAsync(noteId));
        var after = await StoredRowsAsync();
        Assert.Equal(
            (before.Notes, before.Drafts - 1, before.Versions, before.LastEventId),
            (after.Notes, after.Drafts, after.Versions, after.LastEventId));
    }

    // Cancelled once, there is nothing to cancel: the SPA reads a 404 as already cancelled (A71).
    [Fact]
    public async Task A_second_cancel_finds_no_pending_edit()
    {
        var (participantId, noteId) = await AddSubmittedNoteAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await BeginEditAsync(client, participantId);
        using (var first = await CancelAsync(client, participantId))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        using var second = await CancelAsync(client, participantId);

        await second.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Empty(await EditorsAsync(noteId));
    }

    // A pending edit is the caller's own, so a caller with none finds none to cancel, the author's untouched, and gets
    // 404, never 403, whoever they are: a manager, or a worker who did not write the note (A71).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_cancel_with_no_pending_edit_is_not_found_and_leaves_the_author_s(bool anotherWorker)
    {
        var (participantId, noteId) = await AddSubmittedNoteAsync();
        using var author = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await BeginEditAsync(author, participantId);
        var before = await StoredRowsAsync();
        var callerId = anotherWorker
            ? await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.")
            : seeded.A.ManagerId;
        using var client = factory.CreateHttpsClient().SignInAs(callerId);

        using var response = await CancelAsync(client, participantId);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal([seeded.A.WorkerId], await EditorsAsync(noteId));
        Assert.Equal(before, await StoredRowsAsync());
    }

    // A draft's working copy has base 0, which Cancel never deletes: the draft is left to its author, to be
    // submitted or discarded (design.md §3.4).
    [Fact]
    public async Task A_cancel_on_a_draft_is_not_found_and_the_working_copy_stays()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await CancelAsync(client, participant.Id);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal([seeded.A.WorkerId], await EditorsAsync(noteId));
        Assert.Equal(before, await StoredRowsAsync());
    }

    // design.md §9.2: B's manager does not find A's participant, and nobody finds a participant that does not exist
    // or a day with no note; A's pending edit stays.
    [Fact]
    public async Task A_cancel_for_B_s_manager_at_A_s_note_a_day_with_no_note_or_no_participant_is_not_found()
    {
        var (participantId, noteId) = await AddSubmittedNoteAsync();
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await BeginEditAsync(worker, participantId);
        var before = await StoredRowsAsync();
        using var b = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfB, seeded);

        using var other = await CancelAsync(b, participantId);
        using var noNote = await CancelAsync(worker, participantId, NoteDate.AddDays(-1));
        using var none = await CancelAsync(worker, Guid.NewGuid());

        await other.ReadProblemAsync(HttpStatusCode.NotFound);
        await noNote.ReadProblemAsync(HttpStatusCode.NotFound);
        await none.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal([seeded.A.WorkerId], await EditorsAsync(noteId));
        Assert.Equal(before, await StoredRowsAsync());
    }

    private static string DraftPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/draft");

    // Over HTTPS with the caller's antiforgery token and no body, as the screen cancels (tests/README.md).
    private static async Task<HttpResponseMessage> CancelAsync(
        HttpClient client, Guid participantId, DateOnly? noteDate = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, DraftPath(participantId, noteDate ?? NoteDate));
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The caller's pending edit on version 1, made by their first save of it, as the form sends it: from a page of its
    // own, with nothing ticked and the flag unticked.
    private static async Task BeginEditAsync(HttpClient client, Guid participantId)
    {
        using var response = await client.PutWithTokenAsync(
            DraftPath(participantId, NoteDate),
            await client.FetchTokenAsync(),
            new
            {
                baseVersion = 1,
                clientId = Guid.NewGuid(),
                seq = 1,
                narrative = "Jane made toast and tea.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A participant of A's own with a note of A's worker for NoteDate, submitted as version 1.
    private async Task<(Guid ParticipantId, Guid NoteId)> AddSubmittedNoteAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        return (participant.Id, noteId);
    }

    // Each read below is in SQL from a scope of its own, so that neither the query filter, which would need a tenant,
    // nor a context's tracked rows decide what is seen.
    private async Task<NoteRow> StoredNoteAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<NoteRow>($"""
                SELECT [Status], [CurrentVersionNumber], [FirstSubmittedAtUtc], [FlagStatus], [AuthorUserId]
                FROM [Note]
                WHERE [Id] = {noteId}
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // The IDs of the editors who hold a working copy of the note.
    private async Task<List<Guid>> EditorsAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<Guid>($"SELECT [EditorUserId] AS [Value] FROM [NoteDraft] WHERE [NoteId] = {noteId}")
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
                       (SELECT COUNT(*) FROM [NoteDraft]) AS [Drafts],
                       (SELECT COUNT(*) FROM [NoteVersion]) AS [Versions],
                       (SELECT MAX([Id]) FROM [AuditEvent]) AS [LastEventId]
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>How many notes, working copies and versions there are, and the last event's ID.</summary>
    private sealed record StoredRows(int Notes, int Drafts, int Versions, long? LastEventId);

    /// <summary>A <c>Note</c> row's header, its status and flag status as their stored <c>tinyint</c>s.</summary>
    private sealed record NoteRow(
        byte Status, int CurrentVersionNumber, DateTime? FirstSubmittedAtUtc, byte FlagStatus, Guid AuthorUserId);
}
