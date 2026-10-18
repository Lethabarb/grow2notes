using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>POST {base}/versions</c>, Submit (design.md §5.8, §6.3), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): for the author's own draft, the header moved to Submitted, version 1 with the body's
/// text and flag and the goals of the working copy's snapshot, ticked as sent, the working copy deleted, and
/// <c>note.submitted</c>, together or not at all, and <c>201</c>; a retry with the same key <c>200</c> with that
/// version, changing nothing; and, changing nothing, <c>409 note.in_progress_by_other</c> for anyone else's draft,
/// <c>409 note.version_conflict</c> for a submitted note, also at its autosave (§6.7), the body's checks, and
/// <c>404</c> for a participant not in the caller's organisation (§9.2) or no live note. The tests add their own
/// participants, goals, users and drafts (tests/README.md). The endpoint matrix covers who may call it, and
/// <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteVersionEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a draft for Friday 2 October 2026, started at 9:14 am and last saved at 9:30 am in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SavedAtUtc = new(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);

    // Two minutes before midnight on 2 October 2026 in Melbourne.
    private static readonly DateTimeOffset BeforeMidnight = new(2026, 10, 2, 13, 58, 0, TimeSpan.Zero);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // Today in Melbourne, by the app's own clock.
    private DateOnly Today => factory.Services.GetRequiredService<MelbourneClock>().Today();

    // The goals in the snapshot's order, which a manager has changed since the draft started, the ticks the body's, a
    // goal ID sent twice ticking it once, and the flag's reason with it. The clock's time has a part of a millisecond,
    // which the datetime2(3) columns cannot hold, so the time answered is the time stored. The event holds the version
    // number alone, no note text (§5.3, §9.5).
    [Fact]
    public async Task The_author_s_submit_makes_exactly_version_1_its_goal_rows_the_header_s_change_and_one_event()
    {
        const string narrative = "Jane made toast.\nThen a walk to the shops.";
        const string reason = "Jane seemed unwell.";
        var submittedAt = new DateTimeOffset(2026, 10, 2, 5, 42, 0, 123, TimeSpan.Zero).AddTicks(4567);
        var submittedAtUtc = new DateTime(2026, 10, 2, 5, 42, 0, 123, DateTimeKind.Utc);
        await using var app = factory.WithWebHostBuilder(
            builder => builder.UseClock(new FakeTimeProvider(submittedAt)));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Phones her sister", 1),
            new("Catches the bus", 2));
        var noteId = await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            [
                new(NoteSection.Goal, goals[1], null, "Phones her sister", false),
                new(NoteSection.Goal, goals[0], null, "Makes own breakfast", true),
                new(NoteSection.Goal, goals[2], null, "Catches the bus", false),
            ],
            "Jane",
            SavedAtUtc));
        var key = Guid.NewGuid();
        var before = await StoredRowsAsync();
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client,
            participant.Id,
            NoteDate,
            key,
            Body(narrative, true, reason, goals[2], goals[1], goals[2]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal((1, submittedAtUtc), await AnswerAsync(response));
        Assert.Equal(
            new NoteRow(
                (byte)NoteStatus.Submitted, 1, submittedAtUtc, (byte)FlagStatus.None, null, NoteDate, StartedAtUtc,
                seeded.A.WorkerId),
            await StoredNoteAsync(noteId));
        var version = Assert.Single(await StoredVersionsAsync(noteId));
        Assert.Equal(
            new VersionRow(
                version.Id, seeded.A.OrganisationId, noteId, 1, narrative, true, reason, seeded.A.WorkerId,
                submittedAtUtc, key),
            version);
        Assert.Equal(
            [
                GoalRow(version.Id, 0, goals[1], "Phones her sister", true),
                GoalRow(version.Id, 1, goals[0], "Makes own breakfast", false),
                GoalRow(version.Id, 2, goals[2], "Catches the bus", true),
            ],
            await StoredItemsAsync(version.Id));
        Assert.Equal(0, await WorkingCopiesAsync(noteId));
        Assert.Equal(
            new WrittenEvent(
                AuditEventTypes.Note.Submitted, seeded.A.OrganisationId, seeded.A.WorkerId, AuditEntityTypes.Note,
                noteId, participant.Id, """{"versionNumber":1}"""),
            Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0)));
        var after = await StoredRowsAsync();
        Assert.Equal(
            (before.Notes, before.Drafts - 1, before.Versions + 1, before.VersionItems + 3),
            (after.Notes, after.Drafts, after.Versions, after.VersionItems));
    }

    // design.md §5.3: the reason is null unless the version is flagged, whatever the body holds.
    [Fact]
    public async Task An_unflagged_submit_stores_no_reason()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), Body("Jane made toast.", false, "Jane seemed unwell."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = Assert.Single(await StoredVersionsAsync(noteId));
        Assert.Equal((false, null), (version.IsFlagged, version.FlagReason));
    }

    // The header's change and the version are saved before the event, so only the transaction's rollback takes them
    // away, the conditional update's included.
    [Fact]
    public async Task A_submit_whose_event_fails_to_save_changes_nothing()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(
                options => options.AddInterceptors(new FailsTheEventsOf(participantId)))));
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), Body("Jane made toast.", true, "Jane seemed unwell."));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
        Assert.Equal(1, await WorkingCopiesAsync(noteId));
    }

    // A retry after a lost answer, with the key of the first try and a body changed since, which it does not save
    // (design.md §5.8 step 3).
    [Fact]
    public async Task A_retry_with_the_same_key_answers_200_with_the_version_and_changes_nothing()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var key = Guid.NewGuid();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var first = await SubmitAsync(client, participantId, NoteDate, key, Body("Jane made toast."));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var saved = await AnswerAsync(first);
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);

        using var retry = await SubmitAsync(
            client, participantId, NoteDate, key, Body("Jane made toast and tea.", true, "Jane seemed unwell."));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(saved, await AnswerAsync(retry));
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
        Assert.Equal("Jane made toast.", Assert.Single(await StoredVersionsAsync(noteId)).Narrative);
    }

    // Two tries with one key at once, as when the screen sends its submit again while its first try is still on its
    // way: the first try is held just before the header's conditional update, while the second saves the version. The
    // first try's update then changes no row, and it finds the version by the key.
    [Fact]
    public async Task Two_submits_with_one_key_at_once_make_one_version_and_the_later_answers_200_with_it()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var key = Guid.NewGuid();
        var before = await StoredRowsAsync();
        var gate = new HoldsTheHeaderUpdateOf(noteId);
        await using var gated = gate.On(factory);
        using var firstTry = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var secondTry = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        var held = SubmitAsync(firstTry, participantId, NoteDate, key, Body("Jane made toast."));
        (int VersionNumber, DateTime CreatedAtUtc) saved;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var second = await SubmitAsync(secondTry, participantId, NoteDate, key, Body("Jane made toast."));
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            saved = await AnswerAsync(second);
        }
        finally
        {
            gate.Release();
        }

        using var response = await held;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(saved, await AnswerAsync(response));
        Assert.Single(await StoredVersionsAsync(noteId));
        Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0));
    }

    // A second tab's submit, with a key of its own, of a note that the first tab submitted (design.md §6.7): no
    // extensions until T14's notice reads them.
    [Fact]
    public async Task A_second_submit_with_a_new_key_is_refused_as_a_version_conflict_and_changes_nothing()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using (var first = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), Body("Jane made toast.")))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);

        using var second = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), Body("Jane made toast and tea."));

        using var problem = JsonDocument.Parse(await second.ReadProblemAsync(HttpStatusCode.Conflict));
        Assert.Equal("note.version_conflict", problem.RootElement.GetProperty("code").GetString());
        Assert.False(problem.RootElement.TryGetProperty("currentVersion", out _));
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // Only the author creates version 1 (design.md §5.8 step 2), so another worker, and a manager, get who started the
    // draft and when, and nothing that it holds.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task Anyone_else_s_submit_of_a_worker_s_draft_is_refused_as_in_progress_and_creates_nothing(
        Caller caller)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, author, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await SubmitAsync(
            client, participant.Id, NoteDate, Guid.NewGuid(), Body("Jane went out.", true, "Jane seemed unwell."));

        var body = await response.ReadProblemAsync(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(
            ("note.in_progress_by_other", "Alex P.", "2026-10-01T23:14:00Z"),
            (problem.RootElement.GetProperty("code").GetString(),
                problem.RootElement.GetProperty("authorDisplayName").GetString(),
                problem.RootElement.GetProperty("startedAtUtc").GetString()));
        Assert.DoesNotContain("Jane made toast.", body, StringComparison.Ordinal);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // Spaces alone count as blank (design.md §5.8 step 4), and Limits.GuidedNotes is the longest text, as for a draft.
    [Theory]
    [InlineData("blank")]
    [InlineData("spaces")]
    [InlineData("too long")]
    public async Task A_blank_spaces_only_or_too_long_narrative_is_refused_under_narrative_and_creates_nothing(
        string narrative)
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client,
            participantId,
            NoteDate,
            Guid.NewGuid(),
            Body(narrative switch
            {
                "blank" => "",
                "spaces" => "     ",
                _ => new string('x', Limits.GuidedNotes + 1),
            }));

        await ReadValidationProblemAsync(response, "narrative");
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(1, await WorkingCopiesAsync(noteId));
    }

    [Fact]
    public async Task Guided_notes_of_20000_characters_are_submitted_whole()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var text = new string('x', Limits.GuidedNotes - 1) + " ";
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), Body(text));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(text, Assert.Single(await StoredVersionsAsync(noteId)).Narrative);
    }

    // design.md §3.6: a flagged note needs a reason of 1 to 200 characters, spaces alone counting as none. The longest
    // is refused by the validation filter, and the others by the handler, which reads the flag.
    [Theory]
    [InlineData("none")]
    [InlineData("spaces")]
    [InlineData("too long")]
    public async Task A_flagged_submit_with_no_reason_a_spaces_only_one_or_too_long_a_one_is_refused_under_flagReason(
        string reason)
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client,
            participantId,
            NoteDate,
            Guid.NewGuid(),
            Body("Jane made toast.", true, reason switch
            {
                "none" => null,
                "spaces" => "   ",
                _ => new string('x', Limits.FlagReason + 1),
            }));

        await ReadValidationProblemAsync(response, "flagReason");
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(1, await WorkingCopiesAsync(noteId));
    }

    [Fact]
    public async Task A_flag_reason_of_200_characters_is_submitted()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var reason = new string('x', Limits.FlagReason);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), Body("Jane made toast.", true, reason));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(reason, Assert.Single(await StoredVersionsAsync(noteId)).FlagReason);
    }

    // The snapshot is the version's goals (design.md §5.7 *Save a version*), so a goal added since the draft started
    // cannot be ticked in it.
    [Fact]
    public async Task A_ticked_ID_that_is_not_one_of_the_snapshot_s_goals_is_refused_and_creates_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new TestGoal("Makes own breakfast", 0));
        var noteId = await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            [new(NoteSection.Goal, goals[0], null, "Makes own breakfast", false)]));
        var added = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new TestGoal("Waters the garden", 1));
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client,
            participant.Id,
            NoteDate,
            Guid.NewGuid(),
            Body("Jane made toast.", false, null, goals[0], added[0]));

        await ReadValidationProblemAsync(response, "tickedGoalIds");
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(1, await WorkingCopiesAsync(noteId));
    }

    // design.md §14 M2: "A goal reworded … after a draft was created does not change that draft", nor the version
    // submitted from it (A3). The draft starts at the form's first save, and A's manager rewords the goal on the Goals
    // screen.
    [Fact]
    public async Task A_goal_reworded_after_the_draft_started_keeps_its_wording_in_version_1()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goal = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 0));
        var today = Today;
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await StartNoteAsync(worker, participant.Id, today, "Jane made toast.");
        using var manager = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using (var reworded = await manager.PutWithTokenAsync(
                   $"/api/admin/goals/{goal.Id}", await manager.FetchTokenAsync(), new { text = "Makes own lunch" },
                   goal.ETag))
        {
            Assert.Equal(HttpStatusCode.OK, reworded.StatusCode);
        }

        using var response = await SubmitAsync(
            worker, participant.Id, today, Guid.NewGuid(), Body("Jane made toast.", false, null, goal.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var noteId = await LiveNoteIdAsync(participant.Id, today);
        var version = Assert.Single(await StoredVersionsAsync(noteId));
        Assert.Equal(
            [GoalRow(version.Id, 0, goal.Id, "Makes own breakfast", true)], await StoredItemsAsync(version.Id));
    }

    // design.md §3.3 and §3.4: the draft started at 11:58 pm, and is submitted at 12:01 am, when today is the next
    // day; its date is the day it started on, and its submitted time the real one. There is no date check at Submit.
    [Fact]
    public async Task A_draft_started_at_11_58_pm_and_submitted_at_12_01_am_keeps_its_date()
    {
        var clock = new FakeTimeProvider(BeforeMidnight);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await StartNoteAsync(client, participant.Id, NoteDate, "Jane made toast.");
        clock.Advance(TimeSpan.FromMinutes(3));

        using var response = await SubmitAsync(
            client, participant.Id, NoteDate, Guid.NewGuid(), Body("Jane made toast."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submittedAtUtc = BeforeMidnight.AddMinutes(3).UtcDateTime;
        Assert.Equal((1, submittedAtUtc), await AnswerAsync(response));
        var noteId = await LiveNoteIdAsync(participant.Id, NoteDate);
        var note = await StoredNoteAsync(noteId);
        Assert.Equal(
            ((byte)NoteStatus.Submitted, NoteDate, BeforeMidnight.UtcDateTime, submittedAtUtc),
            (note.Status, note.NoteDate, note.CreatedAtUtc, note.FirstSubmittedAtUtc));
    }

    // The submit deleted the working copy (design.md §5.6), so the author's page saving again, and a manager's first
    // save on the note, each have a stale base, a draft's 0 (§6.7), and recreate no working copy.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task An_autosave_after_the_submit_is_refused_as_a_version_conflict_and_recreates_nothing(
        Caller caller)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var today = Today;
        var clientId = Guid.NewGuid();
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await StartNoteAsync(worker, participant.Id, today, "Jane made toast.", clientId);
        using (var submitted = await SubmitAsync(
                   worker, participant.Id, today, Guid.NewGuid(), Body("Jane made toast.")))
        {
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        var noteId = await LiveNoteIdAsync(participant.Id, today);
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await client.PutWithTokenAsync(
            DraftPath(participant.Id, today),
            await client.FetchTokenAsync(),
            new
            {
                clientId = caller == Caller.WorkerOfA ? clientId : Guid.NewGuid(),
                seq = caller == Caller.WorkerOfA ? 2 : 1,
                narrative = "Jane made toast and tea.",
                tickedGoalIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        Assert.Equal("note.version_conflict", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
        Assert.Equal(0, await WorkingCopiesAsync(noteId));
    }

    // The key is unique in the organisation (design.md §5.3), so one that a version of another note has cannot name
    // this save. The screen makes a key for each opening of its confirmation, so it never sends one.
    [Fact]
    public async Task A_key_that_a_version_of_another_note_has_is_refused_under_idempotencyKey_and_creates_nothing()
    {
        var (firstParticipantId, _) = await AddDraftOfTheWorkerAsync();
        var (secondParticipantId, secondNoteId) = await AddDraftOfTheWorkerAsync();
        var key = Guid.NewGuid();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using (var first = await SubmitAsync(client, firstParticipantId, NoteDate, key, Body("Jane made toast.")))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        var before = await StoredRowsAsync();

        using var response = await SubmitAsync(client, secondParticipantId, NoteDate, key, Body("Jane made toast."));

        await ReadValidationProblemAsync(response, "idempotencyKey");
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal((byte)NoteStatus.Draft, (await StoredNoteAsync(secondNoteId)).Status);
    }

    // The screen always sends a key, so these are requests made some other way, which binding refuses before the body's
    // checks or the endpoint (T09's Notes).
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task A_missing_key_or_one_that_is_not_a_GUID_gets_400_and_writes_nothing(string? key)
    {
        var (participantId, _) = await AddDraftOfTheWorkerAsync();
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var request = new HttpRequestMessage(HttpMethod.Post, VersionsPath(participantId, NoteDate))
        {
            Content = JsonContent.Create(Body("Jane made toast.")),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        if (key is not null)
        {
            request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, key);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(before, await StoredRowsAsync());
    }

    // The validation filter's checks (NoteVersionRequest), before the endpoint looks for the participant or a note:
    // a base version other than 0, which T14 takes, and a body without its narrative or ticks.
    [Theory]
    [InlineData("""{"baseVersion":1,"narrative":"Jane made toast.","tickedGoalIds":[]}""", "baseVersion")]
    [InlineData("""{"baseVersion":-1,"narrative":"Jane made toast.","tickedGoalIds":[]}""", "baseVersion")]
    [InlineData("""{"baseVersion":0,"tickedGoalIds":[]}""", "narrative")]
    [InlineData("""{"baseVersion":0,"narrative":null,"tickedGoalIds":[]}""", "narrative")]
    [InlineData("""{"baseVersion":0,"narrative":"Jane made toast."}""", "tickedGoalIds")]
    [InlineData("""{"baseVersion":0,"narrative":"Jane made toast.","tickedGoalIds":null}""", "tickedGoalIds")]
    public async Task A_body_that_breaks_a_rule_is_refused_under_that_field_and_writes_nothing(
        string body, string field)
    {
        var (participantId, _) = await AddDraftOfTheWorkerAsync();
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var request = new HttpRequestMessage(HttpMethod.Post, VersionsPath(participantId, NoteDate))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await ReadValidationProblemAsync(response, field);
        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task A_submit_for_B_s_manager_at_A_s_participant_or_for_one_that_does_not_exist_is_not_found()
    {
        var (participantId, _) = await AddDraftOfTheWorkerAsync();
        var before = await StoredRowsAsync();
        using var b = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfB, seeded);
        using var a = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var other = await SubmitAsync(b, participantId, NoteDate, Guid.NewGuid(), Body("Jane made toast."));
        using var none = await SubmitAsync(a, Guid.NewGuid(), NoteDate, Guid.NewGuid(), Body("Jane made toast."));

        await other.ReadProblemAsync(HttpStatusCode.NotFound);
        await none.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredRowsAsync());
    }

    // Submit saves a draft that exists, and starts none (design.md §5.8 step 1): a day with no note, and one whose only
    // note is a discarded draft, which is not live (§5.5 item 1).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_submit_with_no_live_note_is_not_found_and_creates_nothing(bool discarded)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        if (discarded)
        {
            var noteId = await factory.Services.AddDraftAsync(
                seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane"));
            await DiscardAsync(noteId);
        }

        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participant.Id, NoteDate, Guid.NewGuid(), Body("Jane made toast."));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredRowsAsync());
    }

    private static string VersionsPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/versions");

    private static string DraftPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/draft");

    // Submit's body as the screen sends it: on base 0, the content on screen, and the flag with its reason, which it
    // sends as null while the box is unticked.
    private static object Body(
        string narrative, bool isFlagged = false, string? flagReason = null, params Guid[] tickedGoalIds) =>
        new { baseVersion = 0, narrative, tickedGoalIds, isFlagged, flagReason };

    // Over HTTPS with the caller's antiforgery token and the key, as the SPA submits (tests/README.md).
    private static async Task<HttpResponseMessage> SubmitAsync(
        HttpClient client, Guid participantId, DateOnly noteDate, Guid key, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, VersionsPath(participantId, noteDate))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, key.ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The answer, {versionNumber, createdAtUtc} alone.
    private static async Task<(int VersionNumber, DateTime CreatedAtUtc)> AnswerAsync(HttpResponseMessage response)
    {
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["versionNumber", "createdAtUtc"], answer.EnumerateObject().Select(property => property.Name));
        return (answer.GetProperty("versionNumber").GetInt32(), answer.GetProperty("createdAtUtc").GetDateTime());
    }

    // A 422 validation.failed whose errors name the field alone (design.md §6.1).
    private static async Task ReadValidationProblemAsync(HttpResponseMessage response, string field)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal([field], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
    }

    // The participant's note for the day, which must be the client's today, started by the client's first save, as the
    // form sends it, with the template's lists' version and no goals ticked, from the page given, or a new one.
    private static async Task StartNoteAsync(
        HttpClient client, Guid participantId, DateOnly today, string narrative, Guid? clientId = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var form = await client.GetFromJsonAsync<JsonElement>(DraftPath(participantId, today), cancellationToken);
        using var started = await client.PutWithTokenAsync(
            DraftPath(participantId, today),
            await client.FetchTokenAsync(),
            new
            {
                clientId = clientId ?? Guid.NewGuid(),
                seq = 1,
                listsVersion = form.GetProperty("listsVersion").GetString(),
                narrative,
                tickedGoalIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
    }

    private static GoalItemRow GoalRow(Guid versionId, int position, Guid goalId, string text, bool isTicked) =>
        new(versionId, (byte)NoteSection.Goal, position, goalId, null, null, text, isTicked);

    // A participant of A's own with a draft of A's worker for NoteDate, as its first save leaves it, with no goals.
    private async Task<(Guid ParticipantId, Guid NoteId)> AddDraftOfTheWorkerAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A,
            new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane", SavedAtUtc: SavedAtUtc));
        return (participant.Id, noteId);
    }

    // As Discard draft leaves the note (design.md §5.5 item 5), by A's manager.
    private async Task DiscardAsync(Guid noteId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            var note = await db.Notes.SingleAsync(n => n.Id == noteId, cancellationToken);
            note.Status = NoteStatus.Discarded;
            note.DiscardedAtUtc = SavedAtUtc;
            note.DiscardedByUserId = seeded.A.ManagerId;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // The ID of the participant's live note for the day, through the app's context under A's tenant.
    private async Task<Guid> LiveNoteIdAsync(Guid participantId, DateOnly noteDate)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.Notes
                .Where(note => note.ParticipantId == participantId
                    && note.NoteDate == noteDate
                    && note.Status != NoteStatus.Discarded)
                .Select(note => note.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }
    }

    // Each read below is in SQL from a scope of its own, so that neither the query filter, which would need a tenant,
    // nor a context's tracked rows decide what is seen.
    private async Task<NoteRow> StoredNoteAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<NoteRow>($"""
                SELECT [Status], [CurrentVersionNumber], [FirstSubmittedAtUtc], [FlagStatus], [FlaggedVersionNumber],
                       [NoteDate], [CreatedAtUtc], [AuthorUserId]
                FROM [Note]
                WHERE [Id] = {noteId}
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<VersionRow>> StoredVersionsAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<VersionRow>($"""
                SELECT [Id], [OrganisationId], [NoteId], [VersionNumber], [Narrative], [IsFlagged], [FlagReason],
                       [CreatedByUserId], [CreatedAtUtc], [IdempotencyKey]
                FROM [NoteVersion]
                WHERE [NoteId] = {noteId}
                ORDER BY [VersionNumber]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<GoalItemRow>> StoredItemsAsync(Guid versionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<GoalItemRow>($"""
                SELECT [NoteVersionId], [Section], [Position], [GoalId], [CommonItemId], [GroupPosition], [Text],
                       [IsTicked]
                FROM [NoteVersionItem]
                WHERE [NoteVersionId] = {versionId}
                ORDER BY [Section], [Position]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> WorkingCopiesAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM [NoteDraft] WHERE [NoteId] = {noteId}")
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
                       (SELECT COUNT(*) FROM [Note] WHERE [Status] = 1) AS [SubmittedNotes],
                       (SELECT COUNT(*) FROM [NoteDraft]) AS [Drafts],
                       (SELECT MAX([UpdatedAtUtc]) FROM [NoteDraft]) AS [LastSavedAtUtc],
                       (SELECT COUNT(*) FROM [NoteVersion]) AS [Versions],
                       (SELECT COUNT(*) FROM [NoteVersionItem]) AS [VersionItems],
                       (SELECT MAX([Id]) FROM [AuditEvent]) AS [LastEventId]
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// How many notes there are, and how many submitted, how many working copies, and the last save of any, how many
    /// versions and version rows, and the last event's ID.
    /// </summary>
    private sealed record StoredRows(
        int Notes,
        int SubmittedNotes,
        int Drafts,
        DateTime? LastSavedAtUtc,
        int Versions,
        int VersionItems,
        long? LastEventId);

    /// <summary>A <c>Note</c> row's header, its status and flag status as their stored <c>tinyint</c>s.</summary>
    private sealed record NoteRow(
        byte Status,
        int CurrentVersionNumber,
        DateTime? FirstSubmittedAtUtc,
        byte FlagStatus,
        int? FlaggedVersionNumber,
        DateOnly NoteDate,
        DateTime CreatedAtUtc,
        Guid AuthorUserId);

    /// <summary>A <c>NoteVersion</c> row.</summary>
    private sealed record VersionRow(
        Guid Id,
        Guid OrganisationId,
        Guid NoteId,
        int VersionNumber,
        string Narrative,
        bool IsFlagged,
        string? FlagReason,
        Guid CreatedByUserId,
        DateTime CreatedAtUtc,
        Guid IdempotencyKey);

    /// <summary>A <c>NoteVersionItem</c> row, its section as its stored <c>tinyint</c>.</summary>
    private sealed record GoalItemRow(
        Guid NoteVersionId,
        byte Section,
        int Position,
        Guid? GoalId,
        Guid? CommonItemId,
        int? GroupPosition,
        string Text,
        bool IsTicked);

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

    /// <summary>
    /// Holds back the first command, sent inside a transaction, that updates without reading the note whose ID it is
    /// given, until the test lets it go: a submit's conditional update of the header (design.md §5.8 step 5), once it
    /// has found the note, the key and the working copy, which hold no lock. As
    /// <see cref="HoldsTheFirstSaveOfTheUser"/> holds a save, so that a test's second request meanwhile runs to its
    /// end. Every other command passes.
    /// </summary>
    private sealed class HoldsTheHeaderUpdateOf(Guid noteId) : DbCommandInterceptor
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

        // ExecuteUpdate sends its UPDATE as a command that reads no rows, where SaveChanges reads back what it saved.
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.Transaction is not null
                && command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.Ordinal)
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
