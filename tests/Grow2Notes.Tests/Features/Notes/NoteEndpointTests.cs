using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET {base}</c> (design.md §6.3), called with the test-only sign-in (<see cref="TestSignIn"/>): a submitted
/// note's author, first submitted time, whether it has been edited and by whom last, and current version, its goals in
/// the snapshot's order with its wording and ticks, the same for a worker and a manager, with no review data (A13,
/// A14), a pending edit never showing; a draft's header, who started it and
/// when, whose content it gives to a manager alone, by the role as stored, from its author's working copy (§3.4, A9),
/// and a draft submitted while a manager reads it as the submitted note; <c>404</c> for no live note, a discarded draft
/// included (§5.5 item 1), and for another organisation's note (§9.2); as JSON that is never stored (§6.1), and reading
/// writes nothing (A28). The tests add their own participants, goals, users and notes (tests/README.md). The endpoint
/// matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a note for Thursday 1 October 2026, started at 9:14 am, a draft last saved at 9:42 am, and
    // submitted at 4:42 pm in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = new(2026, 9, 30, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SavedAtUtc = new(2026, 9, 30, 23, 42, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // design.md §6.3's fields, in its order. A manager has the flag status, the reviews and the number of versions
    // after the version, and a worker none of them, even on a note To review. The text keeps its line breaks, and the
    // reason is the flagged version's. A note with one version is not edited, and has no last edit.
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
            Goals: [new(goal.Id, "Makes own breakfast", true)]));

        var worker = await ReadAsync(Caller.WorkerOfA, participant.Id);
        var manager = await ReadAsync(Caller.ManagerOfA, participant.Id);

        Assert.Equal(
            [
                "status", "author", "startedAtUtc", "firstSubmittedAtUtc", "isPastDayNote", "isEdited", "current",
                "flagStatus", "reviews", "versionCount",
            ],
            manager.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, manager.GetProperty("versionCount").GetInt32());
        Assert.Equal("ToReview", manager.GetProperty("flagStatus").GetString());
        Assert.Equal(0, manager.GetProperty("reviews").GetArrayLength());
        Assert.Equal(
            ["status", "author", "startedAtUtc", "firstSubmittedAtUtc", "isPastDayNote", "isEdited", "current"],
            worker.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Submitted", worker.GetProperty("status").GetString());
        var writtenBy = worker.GetProperty("author");
        Assert.Equal(["id", "displayName"], writtenBy.EnumerateObject().Select(property => property.Name));
        Assert.Equal(author, writtenBy.GetProperty("id").GetGuid());
        Assert.Equal("Priya Nair", writtenBy.GetProperty("displayName").GetString());
        Assert.Equal("2026-09-30T23:14:00Z", worker.GetProperty("startedAtUtc").GetString());
        Assert.Equal("2026-10-01T06:42:00Z", worker.GetProperty("firstSubmittedAtUtc").GetString());
        Assert.False(worker.GetProperty("isPastDayNote").GetBoolean());
        Assert.False(worker.GetProperty("isEdited").GetBoolean());
        var current = worker.GetProperty("current");
        Assert.Equal(
            ["versionNumber", "narrative", "isFlagged", "flagReason", "goals", "commonItemGroups"],
            current.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, current.GetProperty("versionNumber").GetInt32());
        Assert.Equal(narrative, current.GetProperty("narrative").GetString());
        Assert.True(current.GetProperty("isFlagged").GetBoolean());
        Assert.Equal("Jane seemed unwell.", current.GetProperty("flagReason").GetString());
        Assert.Equal([("Makes own breakfast", true)], Goals(current));
    }

    // A manager always has a submitted note's flag status, None included, and its reviews, none included.
    [Fact]
    public async Task A_manager_gets_flagStatus_None_and_no_reviews_for_a_note_never_flagged()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));

        var note = await ReadAsync(Caller.ManagerOfA, participant.Id);

        Assert.Equal("None", note.GetProperty("flagStatus").GetString());
        Assert.Equal(JsonValueKind.Array, note.GetProperty("reviews").ValueKind);
        Assert.Equal(0, note.GetProperty("reviews").GetArrayLength());
    }

    // A worker never learns that a manager reviewed their note: neither key, and nothing of the review's comment, which
    // is the manager's alone (A9). The same holds for a worker whose session names Manager, as the role is read as
    // stored.
    [Fact]
    public async Task A_worker_gets_neither_flagStatus_nor_reviews_nor_a_review_s_comment_for_a_reviewed_note()
    {
        const string comment = "Called Sam's mum.";
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc, IsFlagged: true,
            FlagReason: "Jane seemed unwell.", FlagStatus: FlagStatus.Reviewed));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
            db.NoteReviews.Add(new()
            {
                OrganisationId = seeded.A.OrganisationId,
                NoteId = noteId,
                VersionNumber = 1,
                ReviewedByUserId = seeded.A.ManagerId,
                ReviewedAtUtc = SubmittedAtUtc.AddHours(1),
                Comment = comment,
            });
            using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
            {
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        using var namedManager = ClientFor(Caller.WorkerOfA);
        namedManager.DefaultRequestHeaders.Add(TestSignIn.RoleHeader, nameof(UserRole.Manager));
        var viaWorker = await ReadAsync(Caller.WorkerOfA, participant.Id);
        var viaNamedManager = await ReadAsync(namedManager, participant.Id);
        var manager = await ReadAsync(Caller.ManagerOfA, participant.Id);

        foreach (var worker in new[] { viaWorker, viaNamedManager })
        {
            Assert.Equal(
                ["status", "author", "startedAtUtc", "firstSubmittedAtUtc", "isPastDayNote", "isEdited", "current"],
                worker.EnumerateObject().Select(property => property.Name));
            Assert.DoesNotContain("Sam", worker.GetRawText(), StringComparison.Ordinal);
        }

        Assert.Equal(comment, manager.GetProperty("reviews")[0].GetProperty("comment").GetString());
    }

    // design.md §6.3: a note with a version after the first is edited, whoever wrote it, and its last edit is the
    // current version's creator and time, here the manager's, while the author stays the worker's (D39); the version
    // shown is the current one, and a pending edit, even a manager's, shows to no one.
    [Fact]
    public async Task An_edited_note_gives_the_editor_and_time_of_its_current_version_and_that_version_s_number()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        var editedAtUtc = SubmittedAtUtc.AddHours(1);
        await factory.Services.AddVersion2Async(
            seeded.A, noteId, editedAtUtc, flagReason: null, flaggedVersionNumber: 1);
        await factory.Services.AddPendingEditAsync(seeded.A, noteId, seeded.A.WorkerId, editedAtUtc.AddHours(1));

        var note = await ReadAsync(Caller.WorkerOfA, participant.Id);

        Assert.Equal(
            [
                "status", "author", "startedAtUtc", "firstSubmittedAtUtc", "isPastDayNote", "isEdited", "lastEdit",
                "current",
            ],
            note.EnumerateObject().Select(property => property.Name));
        Assert.Equal(seeded.A.WorkerId, note.GetProperty("author").GetProperty("id").GetGuid());
        Assert.True(note.GetProperty("isEdited").GetBoolean());
        var lastEdit = note.GetProperty("lastEdit");
        Assert.Equal(["by", "atUtc"], lastEdit.EnumerateObject().Select(property => property.Name));
        Assert.Equal(await DisplayNameOfAsync(seeded.A.ManagerId), lastEdit.GetProperty("by").GetString());
        Assert.Equal(editedAtUtc, lastEdit.GetProperty("atUtc").GetDateTime());
        var current = note.GetProperty("current");
        Assert.Equal(2, current.GetProperty("versionNumber").GetInt32());
        Assert.Equal("Jane made toast. Then a walk to the shops.", current.GetProperty("narrative").GetString());
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
        Assert.Equal("[]", current.GetProperty("commonItemGroups").GetRawText());
    }

    // design.md §6.3 and §4.2's "Someone else's draft": a draft's header alone, those three members in that order, to a
    // worker, whether Alex P. or another worker, which tells the page to open the form or to say whose draft it is, and
    // nothing of what it says (A9), however much it holds. Whether the caller is a manager is read from the role as
    // stored, so a worker whose session names Manager gets no more.
    [Fact]
    public async Task A_draft_gives_a_worker_who_started_it_or_not_its_status_author_and_start_time_alone()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goal = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 0));
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            author,
            StartedAtUtc,
            [new(NoteSection.Goal, goal.Id, null, "Makes own breakfast", true)],
            "Jane made toast.",
            SavedAtUtc,
            IsFlagged: true,
            FlagReason: "Jane seemed unwell."));
        using var anotherWorker = ClientFor(Caller.WorkerOfA);
        using var alex = factory.CreateHttpsClient().SignInAs(author);
        using var namedManager = ClientFor(Caller.WorkerOfA);
        namedManager.DefaultRequestHeaders.Add(TestSignIn.RoleHeader, nameof(UserRole.Manager));

        foreach (var client in new[] { anotherWorker, alex, namedManager })
        {
            var note = await ReadAsync(client, participant.Id);

            Assert.Equal(
                "{\"status\":\"Draft\",\"authorDisplayName\":\"Alex P.\",\"startedAtUtc\":\"2026-09-30T23:14:00Z\","
                + "\"isPastDayNote\":false}",
                note.GetRawText());
        }
    }

    // design.md §3.4 and §6.3: a manager, by the role as stored, also gets the author's working copy, read-only, in the
    // current shape as built so far (T12's Notes): its text with its line breaks, its flag and reason, and its
    // snapshot's goals in the snapshot's order, with their ticks and the wording the draft started with, though one of
    // them has been reworded since (A3).
    [Fact]
    public async Task A_draft_gives_a_manager_its_header_and_its_author_s_working_copy_with_the_snapshot_s_wording()
    {
        const string narrative = "Jane made toast.\n\nThen a walk to the shops.";
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var breakfast = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 1));
        var sister = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Phones her sister", 0));
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            author,
            StartedAtUtc,
            [
                new(NoteSection.Goal, sister.Id, null, "Phones her sister", false),
                new(NoteSection.Goal, breakfast.Id, null, "Makes own breakfast", true),
            ],
            narrative,
            SavedAtUtc,
            IsFlagged: true,
            FlagReason: "Jane seemed unwell."));
        using var manager = ClientFor(Caller.ManagerOfA);
        using (var reworded = await manager.PutWithTokenAsync(
            $"/api/admin/goals/{breakfast.Id}",
            await manager.FetchTokenAsync(),
            new { text = "Makes breakfast and lunch" },
            breakfast.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, reworded.StatusCode);
        }

        var note = await ReadAsync(manager, participant.Id);

        Assert.Equal(
            ["status", "authorDisplayName", "startedAtUtc", "isPastDayNote", "current"],
            note.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Draft", note.GetProperty("status").GetString());
        Assert.Equal("Alex P.", note.GetProperty("authorDisplayName").GetString());
        Assert.Equal("2026-09-30T23:14:00Z", note.GetProperty("startedAtUtc").GetString());
        var current = note.GetProperty("current");
        Assert.Equal(
            ["narrative", "isFlagged", "flagReason", "goals", "commonItemGroups"],
            current.EnumerateObject().Select(property => property.Name));
        Assert.Equal(narrative, current.GetProperty("narrative").GetString());
        Assert.True(current.GetProperty("isFlagged").GetBoolean());
        Assert.Equal("Jane seemed unwell.", current.GetProperty("flagReason").GetString());
        Assert.Equal([("Phones her sister", false), ("Makes own breakfast", true)], Goals(current));
    }

    // A draft started with nothing ticked or written yet, as its first save may leave it: no text, no reason, as an
    // unflagged note has none (§5.3), and no goals for a participant who has none. A manager's own draft gives them its
    // working copy as anyone else's does.
    [Fact]
    public async Task An_empty_unflagged_draft_gives_a_manager_empty_text_a_null_reason_and_no_goals()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(seeded.A, new(participant.Id, NoteDate, seeded.A.ManagerId, StartedAtUtc));

        var current = (await ReadAsync(Caller.ManagerOfA, participant.Id)).GetProperty("current");

        Assert.Equal(
            """{"narrative":"","isFlagged":false,"flagReason":null,"goals":[],"commonItemGroups":[]}""",
            current.GetRawText());
    }

    // A submit that commits between the manager's read of the draft and their read of its working copy takes the
    // working copy away (§5.8 step 7), so the note is read again and answers as the submitted note it is now.
    [Fact]
    public async Task A_draft_submitted_while_a_manager_reads_it_gives_the_submitted_note()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, author, StartedAtUtc, Narrative: "Jane made toast."));
        var gate = new HoldsTheWorkingCopyReadOf(noteId);
        await using var gated = gate.On(factory);
        using var manager = gated.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var alex = gated.CreateHttpsClient().SignInAs(author);

        var held = manager.GetAsync(PathOf(participant.Id), cancellationToken);
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            using var submit = new HttpRequestMessage(HttpMethod.Post, $"{PathOf(participant.Id)}/versions")
            {
                Content = JsonContent.Create(new
                {
                    baseVersion = 0,
                    narrative = "Jane made toast.",
                    tickedGoalIds = Array.Empty<Guid>(),
                    pickedGroupIds = Array.Empty<Guid>(),
                    tickedCommonItemIds = Array.Empty<Guid>(),
                    isFlagged = false,
                    flagReason = (string?)null,
                }),
            };
            submit.Headers.Add(AntiforgeryTokens.Header, await alex.FetchTokenAsync());
            submit.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
            using var submitted = await alex.SendAsync(submit, cancellationToken);
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await held;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(
            [
                "status", "author", "startedAtUtc", "firstSubmittedAtUtc", "isPastDayNote", "isEdited", "current",
                "flagStatus", "reviews", "versionCount",
            ],
            note.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Submitted", note.GetProperty("status").GetString());
        Assert.Equal("Alex P.", note.GetProperty("author").GetProperty("displayName").GetString());
        Assert.Equal("Jane made toast.", note.GetProperty("current").GetProperty("narrative").GetString());
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

    // design.md §6.3, R11: a manager's submitted note says how many versions it has, which is its current version's
    // number, here 3 after two edits, and a worker's says nothing of it. A draft has no versions to count.
    [Fact]
    public async Task A_manager_alone_gets_the_number_of_versions_of_a_submitted_note_and_a_draft_has_none()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var draft = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        for (var versionNumber = 2; versionNumber <= 3; versionNumber++)
        {
            await factory.Services.AddVersionAsync(
                seeded.A,
                noteId,
                versionNumber,
                SubmittedAtUtc.AddHours(versionNumber),
                flagReason: null,
                flaggedVersionNumber: 1,
                groups: null,
                seeded.A.ManagerId);
        }

        await factory.Services.AddDraftAsync(seeded.A, new(draft.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc));

        var manager = await ReadAsync(Caller.ManagerOfA, participant.Id);
        var worker = await ReadAsync(Caller.WorkerOfA, participant.Id);

        Assert.Equal(3, manager.GetProperty("versionCount").GetInt32());
        Assert.False(worker.TryGetProperty("versionCount", out _));
        Assert.False((await ReadAsync(Caller.ManagerOfA, draft.Id)).TryGetProperty("versionCount", out _));
        Assert.False((await ReadAsync(Caller.WorkerOfA, draft.Id)).TryGetProperty("versionCount", out _));
        Assert.False(manager.GetProperty("current").TryGetProperty("createdBy", out _));
        Assert.False(manager.GetProperty("current").TryGetProperty("createdAtUtc", out _));
    }

    // design.md §3.3, R21: a note is a past-day note when its first change came on a later Melbourne day than its date.
    // The edges are Melbourne midnight, 14:00 UTC before daylight saving starts on 4 October 2026 and 13:00 UTC after,
    // for a draft's header and a submitted note, whoever reads them.
    [Theory]
    [InlineData("2026-09-30", "2026-09-30T13:59:00Z", false)]
    [InlineData("2026-09-30", "2026-09-30T14:00:00Z", true)]
    [InlineData("2026-10-04", "2026-10-04T12:59:00Z", false)]
    [InlineData("2026-10-04", "2026-10-04T13:00:00Z", true)]
    public async Task A_note_started_after_Melbourne_midnight_of_its_date_is_a_past_day_note_whoever_reads_it(
        string noteDate, string startedAt, bool isPastDayNote)
    {
        var date = DateOnly.Parse(noteDate, CultureInfo.InvariantCulture);
        var startedAtUtc = DateTimeOffset.Parse(startedAt, CultureInfo.InvariantCulture).UtcDateTime;
        var drafted = await factory.Services.AddParticipantAsync(seeded.A);
        var submitted = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(seeded.A, new(drafted.Id, date, seeded.A.ManagerId, startedAtUtc));
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            submitted.Id, date, seeded.A.ManagerId, startedAtUtc, startedAtUtc.AddHours(3)));

        foreach (var caller in new[] { Caller.WorkerOfA, Caller.ManagerOfA })
        {
            using var client = ClientFor(caller);
            foreach (var participant in new[] { drafted, submitted })
            {
                var note = await ReadAsync(client, participant.Id, date);

                Assert.Equal(isPastDayNote, note.GetProperty("isPastDayNote").GetBoolean());
            }
        }
    }

    // design.md §3.3, §5.5 item 4: a manager's first save for an earlier day starts a past-day note with them as its
    // author, for a draft's header and, once submitted, the note, which says when it was started. The last case
    // starts it at 12:30 am on 5 October, the day after daylight saving starts, 13:30 UTC on the 4th, when a fixed
    // UTC+10 would still say the 4th.
    [Theory]
    [InlineData("2026-09-28", "2026-10-01T00:30:00Z")]
    [InlineData("2026-09-30", "2026-09-30T14:30:00Z")]
    [InlineData("2026-10-04", "2026-10-04T13:30:00Z")]
    public async Task A_manager_s_first_save_for_an_earlier_day_makes_a_past_day_note_that_stays_one_when_submitted(
        string noteDate, string now)
    {
        var date = DateOnly.Parse(noteDate, CultureInfo.InvariantCulture);
        var startedAt = DateTimeOffset.Parse(now, CultureInfo.InvariantCulture);
        await using var app = factory.WithWebHostBuilder(
            builder => builder.UseClock(new FakeTimeProvider(startedAt)));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var manager = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var worker = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await StartAsync(manager, participant.Id, date);

        foreach (var client in new[] { manager, worker })
        {
            var draft = await ReadAsync(client, participant.Id, date);

            Assert.Equal("Draft", draft.GetProperty("status").GetString());
            Assert.True(draft.GetProperty("isPastDayNote").GetBoolean());
        }

        await SubmitAsync(manager, participant.Id, date);

        foreach (var client in new[] { manager, worker })
        {
            var note = await ReadAsync(client, participant.Id, date);

            Assert.Equal("Submitted", note.GetProperty("status").GetString());
            Assert.True(note.GetProperty("isPastDayNote").GetBoolean());
            Assert.Equal(seeded.A.ManagerId, note.GetProperty("author").GetProperty("id").GetGuid());
            Assert.Equal(startedAt.UtcDateTime, note.GetProperty("startedAtUtc").GetDateTime());
        }
    }

    // R22: a worker's draft started at 11:50 pm on its date and submitted after midnight is not a past-day note, as it
    // is the first change that counts and not the submit, and a worker can only start a note for today. The second case
    // is 11:50 pm AEDT, 10 minutes before the first midnight after the change.
    [Theory]
    [InlineData("2026-10-01", "2026-10-01T13:50:00Z", "2026-10-01T14:10:00Z")]
    [InlineData("2026-10-04", "2026-10-04T12:50:00Z", "2026-10-04T13:10:00Z")]
    public async Task A_worker_s_draft_started_before_midnight_and_submitted_after_it_is_not_a_past_day_note(
        string noteDate, string startedAt, string submittedAt)
    {
        var date = DateOnly.Parse(noteDate, CultureInfo.InvariantCulture);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse(startedAt, CultureInfo.InvariantCulture));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var worker = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var manager = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        await StartAsync(worker, participant.Id, date);
        Assert.False((await ReadAsync(manager, participant.Id, date)).GetProperty("isPastDayNote").GetBoolean());
        clock.SetUtcNow(DateTimeOffset.Parse(submittedAt, CultureInfo.InvariantCulture));

        await SubmitAsync(worker, participant.Id, date);

        foreach (var client in new[] { manager, worker })
        {
            var note = await ReadAsync(client, participant.Id, date);

            Assert.Equal("Submitted", note.GetProperty("status").GetString());
            Assert.False(note.GetProperty("isPastDayNote").GetBoolean());
        }
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

    // design.md §6.3 and D47: Every note, when it has items, then the picked groups, in the version's own places, with
    // the name and wording it was saved with, every item of them, ticked or not. A group that was not picked leaves no
    // trace, its name and its items included, though it holds items.
    [Fact]
    public async Task A_submitted_note_gives_Every_note_then_the_picked_groups_with_every_item_and_nothing_of_the_rest()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var lists = await AddListsAsync();
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            Groups:
            [
                new(
                    seeded.A.EveryNoteGroupId,
                    "Every note",
                    true,
                    false,
                    [new(lists.EveryNoteItem, "Took medication", true)]),
                new(
                    lists.Outing,
                    "Outings as they were",
                    false,
                    true,
                    [
                        new(lists.OutingItemA, "Caught the bus", true),
                        new(lists.OutingItemB, "Went to the shop", false),
                    ]),
                new(lists.Care, "Personal care", false, false, [new(lists.CareItem, "Showered", false)]),
                new(lists.Meals, "Meal preparation", false, true, [new(lists.MealsItem, "Cooked dinner", true)]),
            ]));

        var raw = await ReadAsync(Caller.WorkerOfA, participant.Id);

        var current = raw.GetProperty("current");
        Assert.Equal(
            ["versionNumber", "narrative", "isFlagged", "flagReason", "goals", "commonItemGroups"],
            current.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [
                ("Every note", true, "Took medication", true),
                ("Outings as they were", false, "Caught the bus", true),
                ("Outings as they were", false, "Went to the shop", false),
                ("Meal preparation", false, "Cooked dinner", true),
            ],
            CommonItemGroups(current));
        Assert.DoesNotContain("Personal care", raw.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Showered", raw.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(lists.Care.ToString(), raw.GetRawText(), StringComparison.Ordinal);
    }

    // An Every note with no item rows has nothing to show and is left out, and a version with nothing to show gives an
    // empty array, which the read view calls "No common items set".
    [Fact]
    public async Task An_Every_note_with_no_items_is_left_out_and_nothing_to_show_gives_an_empty_array()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var lists = await AddListsAsync();
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            Groups:
            [
                new(seeded.A.EveryNoteGroupId, "Every note", true, false, []),
                new(lists.Care, "Personal care", false, false, [new(lists.CareItem, "Showered", false)]),
            ]));

        var current = (await ReadAsync(Caller.WorkerOfA, participant.Id)).GetProperty("current");

        Assert.Equal("""[]""", current.GetProperty("commonItemGroups").GetRawText());
    }

    // design.md §3.4 and §6.3: a manager also gets the working copy's shown groups, by the same rule, in the snapshot's
    // order and with its names; a worker gets the header alone.
    [Fact]
    public async Task A_draft_gives_a_manager_its_shown_groups_and_a_worker_none()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var lists = await AddListsAsync();
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            author,
            StartedAtUtc,
            [
                new(NoteSection.CommonItem, lists.EveryNoteItem, seeded.A.EveryNoteGroupId, "Took medication", true),
                new(NoteSection.CommonItem, lists.OutingItemA, lists.Outing, "Caught the bus", false),
                new(NoteSection.CommonItem, lists.CareItem, lists.Care, "Showered", false),
            ],
            "Jane made toast.",
            SavedAtUtc,
            Groups:
            [
                new(seeded.A.EveryNoteGroupId, "Every note", true, false),
                new(lists.Outing, "Outings as they were", false, true),
                new(lists.Care, "Personal care", false, false),
                new(lists.Meals, "Meal preparation", false, true),
            ]));

        var manager = await ReadAsync(Caller.ManagerOfA, participant.Id);
        var worker = await ReadAsync(Caller.WorkerOfA, participant.Id);

        Assert.Equal(
            [
                ("Every note", true, "Took medication", true),
                ("Outings as they were", false, "Caught the bus", false),
            ],
            CommonItemGroups(manager.GetProperty("current")));
        Assert.DoesNotContain("Showered", manager.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(
            ["status", "authorDisplayName", "startedAtUtc", "isPastDayNote"],
            worker.EnumerateObject().Select(property => property.Name));
    }

    // The items of a version's shown groups, in their order, each as its group's name and whether it is Every note,
    // and its own wording and tick. A tuple holding a list would compare the list by reference.
    private static List<(string? Name, bool IsEveryNote, string? Text, bool IsTicked)> CommonItemGroups(
        JsonElement current) =>
    [
        .. current.GetProperty("commonItemGroups").EnumerateArray().SelectMany(group =>
        {
            Assert.Equal(["name", "isEveryNote", "items"], group.EnumerateObject().Select(property => property.Name));
            var name = group.GetProperty("name").GetString();
            var isEveryNote = group.GetProperty("isEveryNote").GetBoolean();
            return TickedRows(group, "items").Select(item => (name, isEveryNote, item.Text, item.IsTicked));
        }),
    ];

    /// <summary>
    /// The IDs of the groups and items that <c>AddListsAsync</c> added to A: Every note's item, the groups Community
    /// outing, with two items, Personal care, with one, and Meal preparation, with one.
    /// </summary>
    private sealed record Lists(
        Guid EveryNoteItem,
        Guid Outing,
        Guid OutingItemA,
        Guid OutingItemB,
        Guid Care,
        Guid CareItem,
        Guid Meals,
        Guid MealsItem);

    // Other tests add groups and items to A for good, so a test that looks at them looks at its own alone.
    private async Task<Lists> AddListsAsync()
    {
        var organisationId = seeded.A.OrganisationId;
        var everyNote = await factory.Services.AddItemsAsync(
            organisationId, seeded.A.EveryNoteGroupId, new TestCommonItem("Took medication", 0));
        var groups = await factory.Services.AddGroupsAsync(
            organisationId, new("Community outing", 0), new("Personal care", 1), new("Meal preparation", 2));
        var outing = await factory.Services.AddItemsAsync(
            organisationId, groups[0], new("Caught the bus", 0), new("Visited the library", 1));
        var care = await factory.Services.AddItemsAsync(organisationId, groups[1], new TestCommonItem("Showered", 0));
        var meals = await factory.Services.AddItemsAsync(
            organisationId, groups[2], new TestCommonItem("Cooked dinner", 0));
        return new(everyNote[0], groups[0], outing[0], outing[1], groups[1], care[0], groups[2], meals[0]);
    }

    // A version's goals, in its order, as their wording and tick.
    private static List<(string? Text, bool IsTicked)> Goals(JsonElement current) => TickedRows(current, "goals");

    // The rows of a version's or a group's list, in its order, as their wording and tick.
    private static List<(string? Text, bool IsTicked)> TickedRows(JsonElement parent, string list) =>
    [
        .. parent.GetProperty(list).EnumerateArray().Select(goal =>
        {
            Assert.Equal(["text", "isTicked"], goal.EnumerateObject().Select(property => property.Name));
            return (goal.GetProperty("text").GetString(), goal.GetProperty("isTicked").GetBoolean());
        }),
    ];

    // A user's display name as stored, read past the tenant query filter with plain SQL.
    private async Task<string> DisplayNameOfAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"SELECT [DisplayName] AS [Value] FROM [AspNetUsers] WHERE [Id] = {userId}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static string PathOf(Guid participantId) => PathOf(participantId, NoteDate);

    private static string PathOf(Guid participantId, DateOnly noteDate) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}");

    private static Task<JsonElement> ReadAsync(HttpClient client, Guid participantId) =>
        ReadAsync(client, participantId, NoteDate);

    private static async Task<JsonElement> ReadAsync(HttpClient client, Guid participantId, DateOnly noteDate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(PathOf(participantId, noteDate), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    // The note's first save, as the form sends it: from a page that has not saved the note before, with the template's
    // lists' version, nothing ticked and some text.
    private static async Task StartAsync(HttpClient client, Guid participantId, DateOnly noteDate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var draftPath = PathOf(participantId, noteDate) + "/draft";
        var form = await client.GetFromJsonAsync<JsonElement>(draftPath, cancellationToken);
        using var started = await client.PutWithTokenAsync(
            draftPath,
            await client.FetchTokenAsync(),
            new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion = form.GetProperty("listsVersion").GetString(),
                narrative = "Jane made toast.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
    }

    // Submit of the caller's own draft, as the screen sends it, with a key of its own.
    private static async Task SubmitAsync(HttpClient client, Guid participantId, DateOnly noteDate)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PathOf(participantId, noteDate) + "/versions")
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 0,
                narrative = "Jane made toast.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            }),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    private async Task<JsonElement> ReadAsync(Caller caller, Guid participantId)
    {
        using var client = ClientFor(caller);
        return await ReadAsync(client, participantId);
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

    /// <summary>
    /// Holds back the first command, sent outside a transaction, that reads a working copy of the note whose ID it is
    /// given, until the test lets it go: a manager's read of the draft's working copy, once the request has found the
    /// draft, as <see cref="HoldsTheHeaderUpdateOf"/> holds an update. Every other command passes, so that a submit
    /// meanwhile runs to its end.
    /// </summary>
    private sealed class HoldsTheWorkingCopyReadOf(Guid noteId) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int held;

        /// <summary>Completes once the command is held.</summary>
        public Task Reached => reached.Task;

        /// <summary><paramref name="app"/>, with this interceptor on every context it makes.</summary>
        public WebApplicationFactory<Program> On(WebApplicationFactory<Program> app) =>
            app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(this))));

        /// <summary>Lets the held command go on.</summary>
        public void Release() => released.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.Transaction is null
                && command.CommandText.Contains("FROM [NoteDraft]", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is Guid id && id == noteId)
                && Interlocked.Exchange(ref held, 1) == 0)
            {
                reached.SetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
