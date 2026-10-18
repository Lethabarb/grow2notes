using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET {base}</c> (design.md §6.3), called with the test-only sign-in (<see cref="TestSignIn"/>): a submitted
/// note's author, first submitted time and current version, its goals in the snapshot's order with its wording and
/// ticks, the same for a worker and a manager, with no review data (A13, A14); a draft's status alone, whose content it
/// never gives (A9); <c>404</c> for no live note, a discarded draft included (§5.5 item 1), and for another
/// organisation's note (§9.2); as JSON that is never stored (§6.1), and reading writes nothing (A28). The tests add
/// their own participants, goals, users and notes (tests/README.md). The endpoint matrix covers who may call it, and
/// <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a note for Thursday 1 October 2026, started at 9:14 am and submitted at 4:42 pm in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = new(2026, 9, 30, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // design.md §6.3's fields that this task gives, in its order, and nothing more: no flagStatus, reviews or
    // versionCount for a manager either, even on a note To review, as T16 and T18 add them. The text keeps its line
    // breaks, and the reason is the flagged version's.
    [Fact]
    public async Task A_submitted_note_gives_a_worker_and_a_manager_its_author_submitted_time_and_current_version()
    {
        const string narrative = "Jane made toast.\n\nThen a walk to the shops.";
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goal = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 0));
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            author,
            StartedAtUtc,
            SubmittedAtUtc,
            narrative,
            IsFlagged: true,
            FlagReason: "Jane seemed unwell.",
            FlagStatus: FlagStatus.ToReview,
            Goals: [new(goal.Id, "Makes own breakfast", true)]));

        var worker = await ReadAsync(Caller.WorkerOfA, participant.Id);
        var manager = await ReadAsync(Caller.ManagerOfA, participant.Id);

        Assert.Equal(worker.GetRawText(), manager.GetRawText());
        Assert.Equal(
            ["status", "author", "firstSubmittedAtUtc", "current"],
            worker.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Submitted", worker.GetProperty("status").GetString());
        var writtenBy = worker.GetProperty("author");
        Assert.Equal(["displayName"], writtenBy.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Priya Nair", writtenBy.GetProperty("displayName").GetString());
        Assert.Equal("2026-10-01T06:42:00Z", worker.GetProperty("firstSubmittedAtUtc").GetString());
        var current = worker.GetProperty("current");
        Assert.Equal(
            ["narrative", "isFlagged", "flagReason", "goals"],
            current.EnumerateObject().Select(property => property.Name));
        Assert.Equal(narrative, current.GetProperty("narrative").GetString());
        Assert.True(current.GetProperty("isFlagged").GetBoolean());
        Assert.Equal("Jane seemed unwell.", current.GetProperty("flagReason").GetString());
        Assert.Equal([("Makes own breakfast", true)], Goals(current));
    }

    // Every goal row of the version, in its place (§5.3), with the wording and tick it was saved with (A3), whatever
    // the participant's goals say now: reworded, reordered and archived since.
    [Fact]
    public async Task The_goals_are_the_version_s_in_the_snapshot_s_order_with_its_wording_and_ticks()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes breakfast and lunch", 0),
            new("Phones her sister", 1),
            new("Catches the bus", 2, ArchivedAtUtc: SubmittedAtUtc.AddDays(1)));
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            Goals:
            [
                new(goals[2], "Catches the bus", true),
                new(goals[0], "Makes own breakfast", false),
                new(goals[1], "Phones her sister", true),
            ]));

        var note = await ReadAsync(Caller.WorkerOfA, participant.Id);

        Assert.Equal(
            [("Catches the bus", true), ("Makes own breakfast", false), ("Phones her sister", true)],
            Goals(note.GetProperty("current")));
    }

    // design.md §5.3: an unflagged version has no reason, written as null; and a note with no goals has none.
    [Fact]
    public async Task An_unflagged_note_gives_a_null_reason()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));

        var current = (await ReadAsync(Caller.WorkerOfA, participant.Id)).GetProperty("current");

        Assert.False(current.GetProperty("isFlagged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("flagReason").ValueKind);
        Assert.Equal([], Goals(current));
    }

    // Its status alone, which tells the page to open the form, for its author and anyone else, and nothing of what it
    // says (A9) or who started it, until T12 adds a draft's header.
    [Fact]
    public async Task A_draft_gives_its_status_alone_to_its_author_and_to_anyone_else()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));

        foreach (var caller in new[] { Caller.WorkerOfA, Caller.ManagerOfA })
        {
            var note = await ReadAsync(caller, participant.Id);

            Assert.Equal("""{"status":"Draft"}""", note.GetRawText());
        }
    }

    // A discarded draft is not live (§5.5 item 1), so it is never reached, as no note is.
    [Fact]
    public async Task No_note_and_a_discarded_draft_each_give_404()
    {
        var none = await factory.Services.AddParticipantAsync(seeded.A);
        var discarded = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(discarded.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        await factory.Services.DiscardAsync(seeded.A, noteId, StartedAtUtc.AddHours(1));
        using var client = ClientFor(Caller.ManagerOfA);

        foreach (var participantId in new[] { none.Id, discarded.Id })
        {
            using var response = await client.GetAsync(PathOf(participantId), TestContext.Current.CancellationToken);

            var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
            Assert.DoesNotContain("Jane made toast.", problem, StringComparison.Ordinal);
        }
    }

    // §9.2: another organisation's participant is not found, whatever their note.
    [Fact]
    public async Task B_s_manager_gets_404_for_A_s_submitted_note()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        using var client = ClientFor(Caller.ManagerOfB);

        using var response = await client.GetAsync(PathOf(participant.Id), TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.DoesNotContain("Jane made toast.", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_answer_is_JSON_that_is_never_stored()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        using var client = ClientFor(Caller.WorkerOfA);

        using var response = await client.GetAsync(PathOf(participant.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
    }

    // design.md §5.4 lists no event for reading (A28), and reading a draft makes no working copy of it.
    [Fact]
    public async Task Reading_writes_nothing()
    {
        var submitted = await factory.Services.AddParticipantAsync(seeded.A);
        var draft = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(submitted.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        var draftId = await factory.Services.AddDraftAsync(
            seeded.A, new(draft.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc));
        var lastEventId = await LastEventIdAsync();

        foreach (var caller in new[] { Caller.WorkerOfA, Caller.ManagerOfA })
        {
            await ReadAsync(caller, submitted.Id);
            await ReadAsync(caller, draft.Id);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
        Assert.Equal(1, await WorkingCopiesAsync(draftId));
    }

    // A version's goals, in its order, as their wording and tick.
    private static List<(string? Text, bool IsTicked)> Goals(JsonElement current) =>
    [
        .. current.GetProperty("goals").EnumerateArray().Select(goal =>
        {
            Assert.Equal(["text", "isTicked"], goal.EnumerateObject().Select(property => property.Name));
            return (goal.GetProperty("text").GetString(), goal.GetProperty("isTicked").GetBoolean());
        }),
    ];

    private static string PathOf(Guid participantId) => $"/api/participants/{participantId}/notes/2026-10-01";

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    private async Task<JsonElement> ReadAsync(Caller caller, Guid participantId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = ClientFor(caller);

        using var response = await client.GetAsync(PathOf(participantId), cancellationToken);

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

    // The note's working copies, read past the tenant query filter with plain SQL.
    private async Task<int> WorkingCopiesAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [NoteDraft] WHERE [NoteId] = {noteId}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
