using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>POST {base}/versions</c>, Submit and Save changes (design.md §5.8, §6.3), called with the test-only sign-in
/// (<see cref="TestSignIn"/>): for the author's own draft, the header moved to Submitted, version 1 with the body's
/// text and flag and the goals of the working copy's snapshot, ticked as sent, the working copy deleted, and
/// <c>note.submitted</c>, together or not at all, and <c>201</c>; for the author's or a manager's pending edit of a
/// submitted note, the same as version <c>n + 1</c> at base <c>n</c>, by the editor, with <c>note.edited</c> and the
/// flag status left as it was; a retry with the same key <c>200</c> with that version, changing nothing; and, changing
/// nothing, <c>409 note.in_progress_by_other</c> for anyone else's draft, <c>403 note.not_editable</c> for a worker
/// who did not write a submitted note, <c>409 note.version_conflict</c> for a base that is not the current version,
/// also at its autosave (§6.7), with the current version, its creator and its time when the note has one, the body's
/// checks, and <c>404</c> for a participant not in the caller's organisation (§9.2) or no live note. There is no date
/// or archive check, so a draft submitted on a later day keeps its date, and one for a participant archived since is
/// submitted (§3.4, §3.7). The tests add their own participants, goals, users and drafts (tests/README.md). The
/// endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed partial class NoteVersionEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a draft for Friday 2 October 2026, started at 9:14 am and last saved at 9:30 am in Melbourne.
    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SavedAtUtc = new(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);

    // Two minutes before midnight on 2 October 2026 in Melbourne.
    private static readonly DateTimeOffset BeforeMidnight = new(2026, 10, 2, 13, 58, 0, TimeSpan.Zero);

    // Made up (A35): a draft for Wednesday 30 September 2026, started that day at 9:14 am in Melbourne; then 9:00 am on
    // Thursday 1 October, and 4:12 pm on Friday 2 October, all before daylight saving starts on 4 October.
    private static readonly DateOnly EarlierDay = new(2026, 9, 30);

    private static readonly DateTimeOffset On30SeptemberAt914 = new(2026, 9, 29, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset On1OctoberAt900 = new(2026, 9, 30, 23, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset On2OctoberAt412 = new(2026, 10, 2, 6, 12, 0, TimeSpan.Zero);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // Today in Melbourne, by the app's own clock.
    private DateOnly Today => factory.Services.GetRequiredService<MelbourneClock>().Today();

    // The goals in the snapshot's order, which a manager has changed since the draft started, the ticks the body's, a
    // goal ID sent twice ticking it once, and the flag's reason with it, which puts the note in To review with
    // version 1 its flagged version (§5.8's flag rules), which the worker's answer does not say (it is a manager's
    // alone). The clock's time has a part of a millisecond, which the datetime2(3) columns cannot hold, so the time
    // answered is the time stored. The event holds the version number alone, no note text (§5.3, §9.5).
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
        Assert.Equal((1, submittedAtUtc, (string?)null), await AnswerAsync(response));
        Assert.Equal(
            new NoteRow(
                (byte)NoteStatus.Submitted, 1, submittedAtUtc, (byte)FlagStatus.ToReview, 1, NoteDate, StartedAtUtc,
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

    // design.md §5.3: the reason is null unless the version is flagged, whatever the body holds. The note stays None.
    [Fact]
    public async Task An_unflagged_submit_stores_no_reason()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), Body("Jane made toast.", false, "Jane seemed unwell."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((byte)FlagStatus.None, (await StoredNoteAsync(noteId)).FlagStatus);
        var version = Assert.Single(await StoredVersionsAsync(noteId));
        Assert.Equal((false, null), (version.IsFlagged, version.FlagReason));
    }

    // The header's change, with the flag status and flagged version that the flagged body sets, and the version are
    // saved before the event, so only the transaction's rollback takes them away, the conditional update's included.
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

    // design.md §5.8 step 5: a flagged version's flag status and flagged version are set by the header's one
    // conditional update, the one that moves the version on, so that they commit with it or not at all; an unflagged
    // one's update names neither, leaving them as they are. Read from the command as sent.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_header_s_one_update_sets_the_flag_status_and_flagged_version_of_a_flagged_version_alone(
        bool flagged)
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var updates = new RecordsTheHeaderUpdatesOf(noteId);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(updates))));
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client,
            participantId,
            NoteDate,
            Guid.NewGuid(),
            Body("Jane made toast.", flagged, flagged ? "Jane seemed unwell." : null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string[] header = ["CurrentVersionNumber", "Status", "FirstSubmittedAtUtc"];
        Assert.Equal(
            flagged ? [.. header, "FlagStatus", "FlaggedVersionNumber"] : header,
            SetColumns(Assert.Single(updates.Commands)));
    }

    // A retry after a lost answer, with the key of the first try and a body changed since, which it does not save
    // (design.md §5.8 step 3): its flag included, so the note stays None.
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

    // The flag status is a manager's alone, so a worker's answer, a retry's too, carries none, though a manager has
    // reviewed the note since: the worker cannot learn of the review from it.
    [Fact]
    public async Task A_workers_flagged_submit_and_its_retry_carry_no_flag_status_even_after_a_review()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var key = Guid.NewGuid();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var first = await SubmitAsync(
            client, participantId, NoteDate, key, Body("Jane made toast.", true, "Jane seemed unwell."));
        var saved = await AnswerAsync(first);
        await ReviewByHandAsync(noteId);

        using var retry = await SubmitAsync(
            client, participantId, NoteDate, key, Body("Jane made toast.", true, "Jane seemed unwell."));

        Assert.Equal((HttpStatusCode.Created, (string?)null), (first.StatusCode, saved.FlagStatus));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(saved, await AnswerAsync(retry));
    }

    // A manager has it: the status the save left, and, for a retry, the status as it is now.
    [Fact]
    public async Task A_managers_flagged_submit_carries_the_flag_status_and_its_retry_the_status_as_it_is_now()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A,
            new(participant.Id, NoteDate, seeded.A.ManagerId, StartedAtUtc, Narrative: "Jane", SavedAtUtc: SavedAtUtc));
        var key = Guid.NewGuid();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using var first = await SubmitAsync(
            client, participant.Id, NoteDate, key, Body("Jane made toast.", true, "Jane seemed unwell."));
        var saved = await AnswerAsync(first);
        await ReviewByHandAsync(noteId);

        using var retry = await SubmitAsync(
            client, participant.Id, NoteDate, key, Body("Jane made toast.", true, "Jane seemed unwell."));

        Assert.Equal((HttpStatusCode.Created, "ToReview"), (first.StatusCode, saved.FlagStatus));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(saved with { FlagStatus = "Reviewed" }, await AnswerAsync(retry));
    }

    // A worker's Save changes on a note that a manager has reviewed answers no flag status either.
    [Fact]
    public async Task A_workers_save_changes_on_a_reviewed_note_carries_no_flag_status()
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        await ReviewByHandAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await BeginEditAsync(client, participantId, Guid.NewGuid(), EditBody(1, "Jane made toast and tea."));

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), EditBody(1, "Jane made toast and tea."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await AnswerAsync(response);
        Assert.Equal((2, (string?)null), (saved.VersionNumber, saved.FlagStatus));
    }

    // Two tries with one key at once, as when the screen sends its submit again while its first try is still on its
    // way: the first try is held just before the header's conditional update, having locked the working copy, and the
    // second waits for that lock. Once the first commits, the second finds the working copy gone, and the version by
    // the key, so it answers 200 with the first's answer.
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
        Task<HttpResponseMessage> second;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            second = SubmitAsync(secondTry, participantId, NoteDate, key, Body("Jane made toast."));
            await WaitForALockAsync(second);
        }
        finally
        {
            gate.Release();
        }

        using var first = await held;
        using var response = await second;
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(await AnswerAsync(first), await AnswerAsync(response));
        Assert.Single(await StoredVersionsAsync(noteId));
        Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0));
    }

    // A second tab's submit, with a key of its own, of a note that the first tab submitted (design.md §6.7): the
    // conflict names version 1, who saved it and when, as T14's notice reads them.
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

        var version1 = Assert.Single(await StoredVersionsAsync(noteId));
        Assert.Equal(
            (1, await DisplayNameOfAsync(seeded.A.WorkerId), version1.CreatedAtUtc), await ReadConflictAsync(second));
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
    // is refused by the validation filter, and the others by the handler, which reads the flag. So the note never
    // reaches To review (§14 M3 Scope: "the required flag reason").
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
        var note = await StoredNoteAsync(noteId);
        Assert.Equal(((byte)FlagStatus.None, null), (note.FlagStatus, note.FlaggedVersionNumber));
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
            [GoalRow(version.Id, 0, goal.Id, "Makes own breakfast", true)],
            (await StoredItemsAsync(version.Id)).Where(row => row.Section == (byte)NoteSection.Goal));
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
        Assert.Equal((1, submittedAtUtc, (string?)null), await AnswerAsync(response));
        var noteId = await LiveNoteIdAsync(participant.Id, NoteDate);
        var note = await StoredNoteAsync(noteId);
        Assert.Equal(
            ((byte)NoteStatus.Submitted, NoteDate, BeforeMidnight.UtcDateTime, submittedAtUtc),
            (note.Status, note.NoteDate, note.CreatedAtUtc, note.FirstSubmittedAtUtc));
    }

    // design.md §3.3 and §3.4 *Earlier-day drafts*: a worker's draft started on 30 September is still theirs to submit
    // on 2 October. Version 1 keeps the note date, and its submitted time is the real one, which GET {base} gives for
    // the read view to show beside the date (§4.4). Whether GET {base} calls it a past-day note is T18's.
    [Fact]
    public async Task A_worker_s_draft_started_on_30_September_and_submitted_on_2_October_keeps_its_date()
    {
        var clock = new FakeTimeProvider(On30SeptemberAt914);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await StartNoteAsync(client, participant.Id, EarlierDay, "Jane made toast.");
        clock.SetUtcNow(On2OctoberAt412);

        using var response = await SubmitAsync(
            client, participant.Id, EarlierDay, Guid.NewGuid(), Body("Jane made toast."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submittedAtUtc = On2OctoberAt412.UtcDateTime;
        Assert.Equal((1, submittedAtUtc, (string?)null), await AnswerAsync(response));
        var noteId = await LiveNoteIdAsync(participant.Id, EarlierDay);
        var note = await StoredNoteAsync(noteId);
        Assert.Equal(
            ((byte)NoteStatus.Submitted, 1, EarlierDay, On30SeptemberAt914.UtcDateTime, submittedAtUtc),
            (note.Status, note.CurrentVersionNumber, note.NoteDate, note.CreatedAtUtc, note.FirstSubmittedAtUtc));
        Assert.Equal(submittedAtUtc, Assert.Single(await StoredVersionsAsync(noteId)).CreatedAtUtc);
        var read = await client.GetFromJsonAsync<JsonElement>(
            NotePath(participant.Id, EarlierDay), TestContext.Current.CancellationToken);
        Assert.Equal(
            ("Submitted", "2026-10-02T06:12:00Z"),
            (read.GetProperty("status").GetString(), read.GetProperty("firstSubmittedAtUtc").GetString()));
    }

    // design.md §3.7: "A draft already started can still be submitted" once its participant is archived, and on a
    // later day too, where nobody can start a note for them any more: its author reads it, saves it and submits it.
    [Fact]
    public async Task A_draft_for_a_participant_archived_since_is_read_saved_and_submitted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(On30SeptemberAt914);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var clientId = Guid.NewGuid();
        using var worker = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await StartNoteAsync(worker, participant.Id, EarlierDay, "Jane made toast.", clientId);
        using var manager = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using (var archived = await manager.PostWithTokenAsync(
                   $"/api/admin/participants/{participant.Id}/archive",
                   await manager.FetchTokenAsync(),
                   ifMatch: participant.ETag))
        {
            Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);
        }

        clock.SetUtcNow(On1OctoberAt900);

        var form = await worker.GetFromJsonAsync<JsonElement>(DraftPath(participant.Id, EarlierDay), cancellationToken);
        using var saved = await worker.PutWithTokenAsync(
            DraftPath(participant.Id, EarlierDay),
            await worker.FetchTokenAsync(),
            new
            {
                clientId,
                seq = 2,
                narrative = "Jane made toast and tea.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);
        using var submitted = await SubmitAsync(
            worker, participant.Id, EarlierDay, Guid.NewGuid(), Body("Jane made toast and tea."));

        Assert.Equal(
            (true, "Jane made toast."),
            (form.GetProperty("isSaved").GetBoolean(), form.GetProperty("narrative").GetString()));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var noteId = await LiveNoteIdAsync(participant.Id, EarlierDay);
        var note = await StoredNoteAsync(noteId);
        Assert.Equal(
            ((byte)NoteStatus.Submitted, EarlierDay, On1OctoberAt900.UtcDateTime),
            (note.Status, note.NoteDate, note.FirstSubmittedAtUtc));
        Assert.Equal("Jane made toast and tea.", Assert.Single(await StoredVersionsAsync(noteId)).Narrative);
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
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
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
    // a negative base version, and a body without its narrative, ticks or picks, or with one null. Each case breaks the
    // one member of an otherwise valid body.
    [Theory]
    [InlineData("baseVersion", "-1")]
    [InlineData("narrative", null)]
    [InlineData("narrative", "null")]
    [InlineData("tickedGoalIds", null)]
    [InlineData("tickedGoalIds", "null")]
    [InlineData("pickedGroupIds", null)]
    [InlineData("pickedGroupIds", "null")]
    [InlineData("tickedCommonItemIds", null)]
    [InlineData("tickedCommonItemIds", "null")]
    public async Task A_body_that_breaks_a_rule_is_refused_under_that_field_and_writes_nothing(
        string field, string? value)
    {
        var body = JsonSerializer.SerializeToNode(Body("Jane made toast."))!.AsObject();
        if (value is null)
        {
            body.Remove(field);
        }
        else
        {
            body[field] = JsonNode.Parse(value);
        }

        var (participantId, _) = await AddDraftOfTheWorkerAsync();
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var request = new HttpRequestMessage(HttpMethod.Post, VersionsPath(participantId, NoteDate))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
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
            await factory.Services.DiscardAsync(seeded.A, noteId, SavedAtUtc);
        }

        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participant.Id, NoteDate, Guid.NewGuid(), Body("Jane made toast."));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredRowsAsync());
    }

    // design.md §5.8 step 5 and §5.3: Submit writes one group row for each group of the snapshot, Every note first, with
    // the picks sent, and one common item row for each of its common items, by group and then item, naming its group by
    // that group's place, ticked as sent, those of a group that is not picked unticked. The snapshot's names are
    // copied, not the live groups', which a manager may have renamed since the draft started (S02.05.04).
    [Fact]
    public async Task Submit_copies_every_group_and_every_common_item_of_the_snapshot()
    {
        var (participantId, noteId, lists) = await AddDraftWithGroupsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client,
            participantId,
            NoteDate,
            Guid.NewGuid(),
            PicksBody([lists.Outing, lists.Outing], [lists.EveryNoteItem, lists.OutingItemB, lists.OutingItemB]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = Assert.Single(await StoredVersionsAsync(noteId));
        var organisationId = seeded.A.OrganisationId;
        Assert.Equal(
            [
                new GroupRow(version.Id, 0, organisationId, seeded.A.EveryNoteGroupId, "Every note", true, false),
                new GroupRow(version.Id, 1, organisationId, lists.Outing, "Outings as they were", false, true),
                new GroupRow(version.Id, 2, organisationId, lists.Care, "Personal care", false, false),
                new GroupRow(version.Id, 3, organisationId, lists.Empty, "Household", false, false),
            ],
            await StoredGroupsAsync(version.Id));
        Assert.Equal(
            [
                GoalRow(version.Id, 0, lists.Goal, "Makes own breakfast", false),
                CommonRow(version.Id, 0, lists.EveryNoteItem, 0, "Took medication", true),
                CommonRow(version.Id, 1, lists.OutingItemA, 1, "Caught the bus", false),
                CommonRow(version.Id, 2, lists.OutingItemB, 1, "Visited the library", true),
                CommonRow(version.Id, 3, lists.CareItem, 2, "Showered", false),
            ],
            await StoredItemsAsync(version.Id));
    }

    // design.md §5.7 and A45: a note may be submitted with nothing picked, and then keeps every group unpicked.
    [Fact]
    public async Task Submit_with_nothing_picked_is_saved()
    {
        var (participantId, noteId, _) = await AddDraftWithGroupsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), PicksBody([], []));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var version = Assert.Single(await StoredVersionsAsync(noteId));
        Assert.All(await StoredGroupsAsync(version.Id), group => Assert.False(group.IsPicked));
        Assert.All(
            (await StoredItemsAsync(version.Id)).Where(row => row.Section == (byte)NoteSection.CommonItem),
            row => Assert.False(row.IsTicked));
    }

    // design.md §5.7: the picks and the ticks are checked against the snapshot, in this order, before the header moves:
    // Every note, a group with no item and an ID that is not a group of the snapshot, one added since the draft
    // started included, cannot be picked; an item that is not in Every note or a picked group, one added to Every note
    // since included, cannot be ticked.
    [Theory]
    [InlineData("every note", "pickedGroupIds")]
    [InlineData("an empty group", "pickedGroupIds")]
    [InlineData("an unknown group", "pickedGroupIds")]
    [InlineData("a group added since", "pickedGroupIds")]
    [InlineData("an item of an unpicked group", "tickedCommonItemIds")]
    [InlineData("an unknown item", "tickedCommonItemIds")]
    [InlineData("an item added to every note since", "tickedCommonItemIds")]
    public async Task A_pick_or_a_tick_outside_the_snapshot_is_refused_and_creates_nothing(string change, string field)
    {
        var (participantId, noteId, lists) = await AddDraftWithGroupsAsync();
        var organisationId = seeded.A.OrganisationId;
        var added = await factory.Services.AddGroupAsync(organisationId, new("Meal preparation", 3));
        await factory.Services.AddItemsAsync(organisationId, added.Id, new TestCommonItem("Cooked dinner", 0));
        var addedToEveryNote = await factory.Services.AddItemsAsync(
            organisationId, seeded.A.EveryNoteGroupId, new TestCommonItem("Locked the door", 1));
        var body = change switch
        {
            "every note" => PicksBody([seeded.A.EveryNoteGroupId], []),
            "an empty group" => PicksBody([lists.Empty], []),
            "an unknown group" => PicksBody([Guid.NewGuid()], []),
            "a group added since" => PicksBody([added.Id], []),
            "an item of an unpicked group" => PicksBody([lists.Outing], [lists.CareItem]),
            "an unknown item" => PicksBody([], [Guid.NewGuid()]),
            _ => PicksBody([], [addedToEveryNote[0]]),
        };
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), body);

        await ReadValidationProblemAsync(response, field);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(1, await WorkingCopiesAsync(noteId));
        Assert.Equal((byte)NoteStatus.Draft, (await StoredNoteAsync(noteId)).Status);
    }

    // Save changes (design.md §5.8, S02.06.01 and S02.06.02): a manager's pending edit of the worker's note, saved at
    // its base, 1, is version 2, written by the manager, while the note keeps its author, status and first submitted
    // time; its flag, over an unflagged version 1, puts the note in To review with version 2 its flagged version
    // (S03.01.02). The manager's working copy goes and the worker's, their own, stays; one note.edited event holds the
    // version number and no text.
    [Fact]
    public async Task A_manager_s_save_changes_makes_version_2_by_the_manager_and_keeps_the_author_and_the_other_copy()
    {
        var (participantId, noteId, goalId) = await AddSubmittedNoteAsync();
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var manager = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        await BeginEditAsync(worker, participantId, Guid.NewGuid(), EditBody(1, "Jane made toast and jam."));
        var content = EditBody(1, "Jane made toast and tea.", true, "Jane seemed unwell.", [goalId]);
        await BeginEditAsync(manager, participantId, Guid.NewGuid(), content);
        var key = Guid.NewGuid();
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        var first = Assert.Single(await StoredVersionsAsync(noteId));

        using var response = await SubmitAsync(manager, participantId, NoteDate, key, content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await AnswerAsync(response);
        Assert.Equal((2, "ToReview"), (saved.VersionNumber, saved.FlagStatus));
        Assert.Equal(
            note with
            {
                CurrentVersionNumber = 2,
                FlagStatus = (byte)FlagStatus.ToReview,
                FlaggedVersionNumber = 2,
            },
            await StoredNoteAsync(noteId));
        var versions = await StoredVersionsAsync(noteId);
        Assert.Equal(first, versions[0]);
        var second = versions[1];
        Assert.Equal(
            new VersionRow(
                second.Id, seeded.A.OrganisationId, noteId, 2, "Jane made toast and tea.", true, "Jane seemed unwell.",
                seeded.A.ManagerId, saved.CreatedAtUtc, key),
            second);
        Assert.Equal(
            [GoalRow(second.Id, 0, goalId, "Makes own breakfast", true)], await StoredItemsAsync(second.Id));
        Assert.Equal([seeded.A.WorkerId], await WorkingCopyEditorsAsync(noteId));
        var written = Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0));
        Assert.Equal(
            new WrittenEvent(
                AuditEventTypes.Note.Edited, seeded.A.OrganisationId, seeded.A.ManagerId, AuditEntityTypes.Note,
                noteId, participantId, """{"versionNumber":2}"""),
            written);
        var after = await StoredRowsAsync();
        Assert.Equal(
            (before.Notes, before.Drafts - 1, before.Versions + 1),
            (after.Notes, after.Drafts, after.Versions));
    }

    // design.md §5.8 step 5: a later version's one header update sets the current version, and the flag status and
    // flagged version only when the version raises the flag, leaving the status and the first submitted time as they
    // are. Read from the command as sent.
    [Theory]
    [InlineData(false, new[] { "CurrentVersionNumber" })]
    [InlineData(true, new[] { "CurrentVersionNumber", "FlagStatus", "FlaggedVersionNumber" })]
    public async Task The_header_s_update_of_a_later_version_sets_the_flag_only_when_it_is_raised(
        bool flagged, string[] columns)
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var content = flagged
            ? EditBody(1, "Jane made toast and tea.", true, "Jane seemed unwell.")
            : EditBody(1, "Jane made toast and tea.");
        await BeginEditAsync(worker, participantId, Guid.NewGuid(), content);
        var updates = new RecordsTheHeaderUpdatesOf(noteId);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(updates))));
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(columns, SetColumns(Assert.Single(updates.Commands)));
    }

    // R10 and A69: a Save changes with nothing changed is still a version.
    [Fact]
    public async Task A_save_changes_with_nothing_changed_adds_a_version()
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var content = EditBody(1, "Jane made toast.");
        await BeginEditAsync(client, participantId, Guid.NewGuid(), content);

        using var response = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            [(1, "Jane made toast."), (2, "Jane made toast.")],
            (await StoredVersionsAsync(noteId)).Select(version => (version.VersionNumber, version.Narrative)));
    }

    // design.md §5.3: versions are only ever added, so after three Save changes versions 1 and 2, and their group and
    // item rows, are as they were first written, field by field.
    [Fact]
    public async Task After_three_Save_changes_versions_1_and_2_and_their_rows_are_as_they_were_written()
    {
        var (participantId, noteId, lists) = await AddDraftWithGroupsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using (var submitted = await SubmitAsync(
                   client, participantId, NoteDate, Guid.NewGuid(), PicksBody([lists.Outing], [lists.OutingItemA])))
        {
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        List<string>? firstTwo = null;
        for (var baseVersion = 1; baseVersion <= 3; baseVersion++)
        {
            var content = EditBody(
                baseVersion, $"Edit {baseVersion}", false, null, [lists.Goal], [lists.Care], [lists.CareItem]);
            await BeginEditAsync(client, participantId, Guid.NewGuid(), content);
            using var saved = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), content);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            if (baseVersion == 1)
            {
                firstTwo = await StoredVersionContentsAsync(noteId);
            }
        }

        var all = await StoredVersionContentsAsync(noteId);
        Assert.Equal(4, all.Count);
        Assert.Equal(firstTwo, all.Take(2));
        Assert.Equal(4, (await StoredNoteAsync(noteId)).CurrentVersionNumber);
    }

    // S02.06.01 and note-form item 30i: an edit that changes only the picks puts the newly picked group, with its
    // items, in the read of the current version.
    [Fact]
    public async Task A_save_changes_that_only_picks_a_group_shows_the_group_in_the_note_s_current_version()
    {
        var (participantId, _, lists) = await AddDraftWithGroupsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using (var submitted = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), PicksBody([], [])))
        {
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        Assert.Equal(["Every note"], await ShownGroupNamesAsync(client, participantId));
        var content = EditBody(1, "Jane made toast.", false, null, [], [lists.Care], [lists.CareItem]);
        await BeginEditAsync(client, participantId, Guid.NewGuid(), content);

        using var saved = await SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), content);

        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var note = await client.GetFromJsonAsync<JsonElement>(
            NotePath(participantId, NoteDate), TestContext.Current.CancellationToken);
        var current = note.GetProperty("current");
        Assert.Equal(2, current.GetProperty("versionNumber").GetInt32());
        var care = Assert.Single(
            current.GetProperty("commonItemGroups").EnumerateArray(),
            group => group.GetProperty("name").GetString() == "Personal care");
        var item = Assert.Single(care.GetProperty("items").EnumerateArray());
        Assert.Equal(
            ("Showered", true), (item.GetProperty("text").GetString(), item.GetProperty("isTicked").GetBoolean()));
    }

    // design.md §5.8 step 3: the key finds the version, so a retry of a Save changes that committed answers 200 with
    // it, though the note's version has moved on from the retry's base, and creates no second version.
    [Fact]
    public async Task A_retry_of_a_save_changes_after_the_base_moved_answers_200_with_the_version()
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var key = Guid.NewGuid();
        await BeginEditAsync(client, participantId, Guid.NewGuid(), EditBody(1, "Jane made toast and tea."));
        using var first = await SubmitAsync(
            client, participantId, NoteDate, key, EditBody(1, "Jane made toast and tea."));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var saved = await AnswerAsync(first);
        var before = await StoredRowsAsync();

        using var retry = await SubmitAsync(
            client, participantId, NoteDate, key, EditBody(1, "Jane made toast, tea and jam."));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(saved, await AnswerAsync(retry));
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(2, (await StoredVersionsAsync(noteId)).Count);
    }

    // design.md §6.7 and S02.06.03: a working copy at another base than the request's, or a request whose base is not
    // the current version, is a stale save; it creates nothing and keeps the copy, and names the current version, its
    // creator and when it was saved.
    [Fact]
    public async Task A_save_changes_with_no_working_copy_at_its_base_is_refused_with_the_latest_version()
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        await BeginEditAsync(worker, participantId, Guid.NewGuid(), EditBody(1, "Jane made toast and jam."));
        var editedAtUtc = SavedAtUtc.AddHours(1);
        await factory.Services.AddVersion2Async(
            seeded.A, noteId, editedAtUtc, flagReason: null, flaggedVersionNumber: 1);
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        var latest = (2, await DisplayNameOfAsync(seeded.A.ManagerId), editedAtUtc);

        using var atTheCurrentVersion = await SubmitAsync(
            worker, participantId, NoteDate, Guid.NewGuid(), EditBody(2, "Jane made toast and jam."));
        using var atTheCopysBase = await SubmitAsync(
            worker, participantId, NoteDate, Guid.NewGuid(), EditBody(1, "Jane made toast and jam."));

        Assert.Equal(latest, await ReadConflictAsync(atTheCurrentVersion));
        Assert.Equal(latest, await ReadConflictAsync(atTheCopysBase));
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
        Assert.Equal([seeded.A.WorkerId], await WorkingCopyEditorsAsync(noteId));
    }

    // A draft has no version to name (LatestVersion is null), so a Save changes on one is a conflict with no
    // extensions, and its working copy stays.
    [Fact]
    public async Task A_save_at_base_1_on_a_draft_is_a_conflict_with_no_extensions_and_changes_nothing()
    {
        var (participantId, noteId) = await AddDraftOfTheWorkerAsync();
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), EditBody(1, "Jane made toast."));

        await ReadConflictWithoutExtensionsAsync(response);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
        Assert.Equal(1, await WorkingCopiesAsync(noteId));
    }

    // S02.06.03: two editors hold a pending edit on version 2. The manager's Save changes is held just before the
    // header's update while the worker's saves version 3; the manager's update then changes no row, so it is refused
    // with version 3, who saved it and when, and the manager keeps the copy. Start again, a save at base 3, then
    // Save changes, makes version 4.
    [Fact]
    public async Task The_second_of_two_editors_is_refused_with_the_first_s_version_and_saves_after_Start_again()
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        await factory.Services.AddVersion2Async(
            seeded.A, noteId, SavedAtUtc.AddHours(1), flagReason: null, flaggedVersionNumber: 1);
        var gate = new HoldsTheHeaderUpdateOf(noteId);
        await using var gated = gate.On(factory);
        using var worker = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var manager = gated.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        var managerPage = Guid.NewGuid();
        var managersContent = EditBody(2, "Jane made toast and tea.");
        await BeginEditAsync(worker, participantId, Guid.NewGuid(), EditBody(2, "Jane made toast and jam."));
        await BeginEditAsync(manager, participantId, managerPage, managersContent);

        var held = SubmitAsync(manager, participantId, NoteDate, Guid.NewGuid(), managersContent);
        HttpResponseMessage first;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            first = await SubmitAsync(
                worker, participantId, NoteDate, Guid.NewGuid(), EditBody(2, "Jane made toast and jam."));
        }
        finally
        {
            gate.Release();
        }

        using var firstResponse = first;
        using var refused = await held;
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var third = await AnswerAsync(firstResponse);
        Assert.Equal(
            (3, await DisplayNameOfAsync(seeded.A.WorkerId), third.CreatedAtUtc), await ReadConflictAsync(refused));
        Assert.Equal(3, (await StoredNoteAsync(noteId)).CurrentVersionNumber);
        Assert.Equal([seeded.A.ManagerId], await WorkingCopyEditorsAsync(noteId));

        var startAgain = AutosaveBody(managerPage, 2, EditBody(3, "Jane made toast and tea."));
        using (var startedAgain = await SaveDraftAsync(manager, participantId, NoteDate, startAgain))
        {
            Assert.Equal(HttpStatusCode.OK, startedAgain.StatusCode);
        }

        using var fourth = await SubmitAsync(
            manager, participantId, NoteDate, Guid.NewGuid(), EditBody(3, "Jane made toast and tea."));

        Assert.Equal(HttpStatusCode.Created, fourth.StatusCode);
        Assert.Equal(4, (await AnswerAsync(fourth)).VersionNumber);
        var versions = await StoredVersionsAsync(noteId);
        Assert.Equal(
            (seeded.A.ManagerId, "Jane made toast and tea."), (versions[3].CreatedByUserId, versions[3].Narrative));
        Assert.Empty(await WorkingCopyEditorsAsync(noteId));
    }

    // S02.06.01 and design.md §3.4's lock order: an autosave of the editor's pending edit sent while the same editor's
    // Save changes is held just before the header's update waits for the working copy, then, the version saved and the
    // copy gone, gets a version conflict, with no deadlock and no second version.
    [Fact]
    public async Task An_autosave_sent_while_the_same_editor_s_save_changes_is_held_waits_then_gets_409()
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        var page = Guid.NewGuid();
        var content = EditBody(1, "Jane made toast and tea.");
        using (var seed = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded))
        {
            await BeginEditAsync(seed, participantId, page, content);
        }

        var gate = new HoldsTheHeaderUpdateOf(noteId);
        await using var gated = gate.On(factory);
        using var client = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        var held = SubmitAsync(client, participantId, NoteDate, Guid.NewGuid(), content);
        Task<HttpResponseMessage> autosave;
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            autosave = SaveDraftAsync(
                client, participantId, NoteDate, AutosaveBody(page, 2, EditBody(1, "Jane made toast, tea and jam.")));
            await WaitForALockAsync(autosave);
        }
        finally
        {
            gate.Release();
        }

        using var saved = await held;
        using var late = await autosave;
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        await ReadConflictWithoutExtensionsAsync(late);
        Assert.Equal(2, (await StoredVersionsAsync(noteId)).Count);
        Assert.Equal(0, await WorkingCopiesAsync(noteId));
    }

    // design.md §6.9: only the author or a manager may edit a submitted note, and the refusal comes before the base is
    // looked at, so a worker who did not write it gets 403 note.not_editable at base 0, a draft's, and at base 1.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task A_worker_who_did_not_write_the_note_is_refused_as_not_editable_at_any_base_and_creates_nothing(
        int baseVersion)
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        var other = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(other);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), EditBody(baseVersion, "Jane made toast and tea."));

        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Forbidden));
        Assert.Equal("note.not_editable", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    // design.md §9.2: B's manager does not find A's note to edit.
    [Fact]
    public async Task A_save_changes_for_B_s_manager_at_A_s_note_is_not_found_and_creates_nothing()
    {
        var (participantId, noteId, _) = await AddSubmittedNoteAsync();
        var before = await StoredRowsAsync();
        var note = await StoredNoteAsync(noteId);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfB, seeded);

        using var response = await SubmitAsync(
            client, participantId, NoteDate, Guid.NewGuid(), EditBody(1, "Jane made toast and tea."));

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredRowsAsync());
        Assert.Equal(note, await StoredNoteAsync(noteId));
    }

    private static string VersionsPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/versions");

    private static string DraftPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/draft");

    private static string NotePath(Guid participantId, DateOnly noteDate) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}");

    // Submit's body as the screen sends it: on base 0, the content on screen, and the flag with its reason, which it
    // sends as null while the box is unticked.
    private static object Body(
        string narrative, bool isFlagged = false, string? flagReason = null, params Guid[] tickedGoalIds) =>
        new
        {
            baseVersion = 0,
            narrative,
            tickedGoalIds,
            pickedGroupIds = Array.Empty<Guid>(),
            tickedCommonItemIds = Array.Empty<Guid>(),
            isFlagged,
            flagReason,
        };

    // The same, unflagged, with the common item groups picked and the common items ticked, and no goal ticked.
    private static object PicksBody(IReadOnlyList<Guid> pickedGroupIds, IReadOnlyList<Guid> tickedCommonItemIds) =>
        new
        {
            baseVersion = 0,
            narrative = "Jane made toast.",
            tickedGoalIds = Array.Empty<Guid>(),
            pickedGroupIds,
            tickedCommonItemIds,
            isFlagged = false,
            flagReason = (string?)null,
        };

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

    // The answer, {versionNumber, createdAtUtc} and, for a manager alone, flagStatus (null here when it is left out).
    private static async Task<(int VersionNumber, DateTime CreatedAtUtc, string? FlagStatus)> AnswerAsync(
        HttpResponseMessage response)
    {
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var hasFlagStatus = answer.TryGetProperty("flagStatus", out var flagStatus);
        Assert.Equal(
            hasFlagStatus ? ["versionNumber", "createdAtUtc", "flagStatus"] : ["versionNumber", "createdAtUtc"],
            answer.EnumerateObject().Select(property => property.Name));
        return (
            answer.GetProperty("versionNumber").GetInt32(),
            answer.GetProperty("createdAtUtc").GetDateTime(),
            hasFlagStatus ? flagStatus.GetString() : null);
    }

    // The columns that an UPDATE's SET clause sets, in its order.
    private static List<string> SetColumns(string update)
    {
        var set = update.IndexOf("SET", StringComparison.Ordinal);
        var from = update.IndexOf("FROM", set, StringComparison.Ordinal);
        return [.. AssignedColumn().Matches(update[set..from]).Select(match => match.Groups["column"].Value)];
    }

    [GeneratedRegex(@"\[(?<column>\w+)\] = ")]
    private static partial Regex AssignedColumn();

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
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
    }

    private static GoalItemRow GoalRow(Guid versionId, int position, Guid goalId, string text, bool isTicked) =>
        new(versionId, (byte)NoteSection.Goal, position, goalId, null, null, text, isTicked);

    private static GoalItemRow CommonRow(
        Guid versionId, int position, Guid itemId, int groupPosition, string text, bool isTicked) =>
        new(versionId, (byte)NoteSection.CommonItem, position, null, itemId, groupPosition, text, isTicked);

    // A participant of A's own with a draft of A's worker for NoteDate whose snapshot holds a goal, Every note,
    // Outings, Personal care and Household, as a note's first save leaves one. The live group is named "Outings
    // renamed", as if a manager renamed it since the draft started (S02.05.04), and the draft keeps its name.
    private async Task<(Guid ParticipantId, Guid NoteId, Lists Lists)> AddDraftWithGroupsAsync()
    {
        var organisationId = seeded.A.OrganisationId;
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goal = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 0));
        var everyNoteItem = await factory.Services.AddItemsAsync(
            organisationId, seeded.A.EveryNoteGroupId, new TestCommonItem("Took medication", 0));
        var groups = await factory.Services.AddGroupsAsync(
            organisationId, new("Outings renamed", 0), new("Personal care", 1), new("Household", 2));
        var outing = await factory.Services.AddItemsAsync(
            organisationId, groups[0], new("Caught the bus", 0), new("Visited the library", 1));
        var care = await factory.Services.AddItemsAsync(organisationId, groups[1], new TestCommonItem("Showered", 0));
        var lists = new Lists(
            goal.Id, everyNoteItem[0], groups[0], outing[0], outing[1], groups[1], care[0], groups[2]);
        var noteId = await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            [
                new(NoteSection.Goal, goal.Id, null, "Makes own breakfast", false),
                new(NoteSection.CommonItem, lists.EveryNoteItem, seeded.A.EveryNoteGroupId, "Took medication", false),
                new(NoteSection.CommonItem, lists.OutingItemA, lists.Outing, "Caught the bus", false),
                new(NoteSection.CommonItem, lists.OutingItemB, lists.Outing, "Visited the library", false),
                new(NoteSection.CommonItem, lists.CareItem, lists.Care, "Showered", false),
            ],
            "Jane",
            SavedAtUtc,
            Groups:
            [
                new(seeded.A.EveryNoteGroupId, "Every note", true, false),
                new(lists.Outing, "Outings as they were", false, false),
                new(lists.Care, "Personal care", false, false),
                new(lists.Empty, "Household", false, false),
            ]));
        return (participant.Id, noteId, lists);
    }

    private async Task<List<GroupRow>> StoredGroupsAsync(Guid versionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<GroupRow>($"""
                SELECT [NoteVersionId], [Position], [OrganisationId], [CommonItemGroupId], [Name], [IsEveryNote],
                       [IsPicked]
                FROM [NoteVersionGroup]
                WHERE [NoteVersionId] = {versionId}
                ORDER BY [Position]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // Save changes' and a pending edit's body as the screen sends it: on the base given, the content on screen, and the
    // flag with its reason, which it sends as null while the box is unticked.
    private static object EditBody(
        int baseVersion,
        string narrative,
        bool isFlagged = false,
        string? flagReason = null,
        IReadOnlyList<Guid>? tickedGoalIds = null,
        IReadOnlyList<Guid>? pickedGroupIds = null,
        IReadOnlyList<Guid>? tickedCommonItemIds = null) =>
        new
        {
            baseVersion,
            narrative,
            tickedGoalIds = tickedGoalIds ?? [],
            pickedGroupIds = pickedGroupIds ?? [],
            tickedCommonItemIds = tickedCommonItemIds ?? [],
            isFlagged,
            flagReason,
        };

    // An autosave's body: the content, from the page and with the number given.
    private static JsonObject AutosaveBody(Guid clientId, int seq, object content)
    {
        var body = JsonSerializer.SerializeToNode(content)!.AsObject();
        body["clientId"] = clientId;
        body["seq"] = seq;
        return body;
    }

    // Over HTTPS with the caller's antiforgery token, as the SPA autosaves (tests/README.md).
    private static async Task<HttpResponseMessage> SaveDraftAsync(
        HttpClient client, Guid participantId, DateOnly noteDate, object body) =>
        await client.PutWithTokenAsync(
            DraftPath(participantId, noteDate), await client.FetchTokenAsync(), body, ifMatch: null);

    // The first save of a pending edit, from a page of its own, which is answered 200 (the note exists already).
    private static async Task BeginEditAsync(HttpClient client, Guid participantId, Guid clientId, object content)
    {
        using var response = await SaveDraftAsync(
            client, participantId, NoteDate, AutosaveBody(clientId, 1, content));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A 409 note.version_conflict with the latest version's number, creator and time.
    private static async Task<(int CurrentVersion, string CreatedBy, DateTime CreatedAtUtc)> ReadConflictAsync(
        HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        var root = problem.RootElement;
        Assert.Equal("note.version_conflict", root.GetProperty("code").GetString());
        return (
            root.GetProperty("currentVersion").GetInt32(),
            root.GetProperty("createdBy").GetString()!,
            root.GetProperty("createdAtUtc").GetDateTime());
    }

    // A 409 note.version_conflict that names no version.
    private static async Task ReadConflictWithoutExtensionsAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        var root = problem.RootElement;
        Assert.Equal("note.version_conflict", root.GetProperty("code").GetString());
        Assert.All(
            new[] { "currentVersion", "createdBy", "createdAtUtc" },
            extension => Assert.False(root.TryGetProperty(extension, out _)));
    }

    // The names of the common item groups that the note's current version shows, in its order.
    private static async Task<List<string?>> ShownGroupNamesAsync(HttpClient client, Guid participantId)
    {
        var note = await client.GetFromJsonAsync<JsonElement>(
            NotePath(participantId, NoteDate), TestContext.Current.CancellationToken);
        return
        [
            .. note.GetProperty("current").GetProperty("commonItemGroups").EnumerateArray()
                .Select(group => group.GetProperty("name").GetString()),
        ];
    }

    // A participant of A's own with a submitted note of A's worker for NoteDate, version 1, with one unticked goal and
    // no groups; and their IDs.
    private async Task<(Guid ParticipantId, Guid NoteId, Guid GoalId)> AddSubmittedNoteAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goal = await factory.Services.AddGoalAsync(seeded.A, participant.Id, new("Makes own breakfast", 0));
        var noteId = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SavedAtUtc,
            Goals: [new(goal.Id, "Makes own breakfast", false)]));
        return (participant.Id, noteId, goal.Id);
    }

    // A user's display name as stored, read past the tenant query filter with plain SQL.
    private async Task<string> DisplayNameOfAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<string>($"SELECT [DisplayName] AS [Value] FROM [AspNetUsers] WHERE [Id] = {userId}")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // The IDs of the editors who hold a working copy of the note.
    private async Task<List<Guid>> WorkingCopyEditorsAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<Guid>($"SELECT [EditorUserId] AS [Value] FROM [NoteDraft] WHERE [NoteId] = {noteId}")
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // Each version of the note with its group and item rows, as JSON, in the order of the versions.
    private async Task<List<string>> StoredVersionContentsAsync(Guid noteId)
    {
        List<string> contents = [];
        foreach (var version in await StoredVersionsAsync(noteId))
        {
            contents.Add(JsonSerializer.Serialize(new
            {
                version,
                items = await StoredItemsAsync(version.Id),
                groups = await StoredGroupsAsync(version.Id),
            }));
        }

        return contents;
    }

    // Waits until a request on the database waits for a lock that another's transaction holds, which only the request
    // given can, as the tests of the collection run one at a time; and fails if that request ends first.
    private async Task WaitForALockAsync(Task<HttpResponseMessage> request)
    {
        while (!request.IsCompleted && !await AnyRequestWaitsForALockAsync())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        Assert.False(request.IsCompleted, "The request ended without waiting for a lock.");
    }

    private async Task<bool> AnyRequestWaitsForALockAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value]
                FROM sys.dm_exec_requests
                WHERE [wait_type] LIKE 'LCK[_]M[_]%' AND [database_id] = DB_ID()
                """)
            .SingleAsync(TestContext.Current.CancellationToken) > 0;
    }

    // A participant of A's own with a draft of A's worker for NoteDate, as its first save leaves it, with no goals.
    private async Task<(Guid ParticipantId, Guid NoteId)> AddDraftOfTheWorkerAsync()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A,
            new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane", SavedAtUtc: SavedAtUtc));
        return (participant.Id, noteId);
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

    // As a manager's review leaves the note (design.md §5.8 *Review*), in SQL, as the tests read it.
    private async Task ReviewByHandAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE [Note] SET [FlagStatus] = {(byte)FlagStatus.Reviewed} WHERE [Id] = {noteId}",
            TestContext.Current.CancellationToken);
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

    /// <summary>A <c>NoteVersionGroup</c> row.</summary>
    private sealed record GroupRow(
        Guid NoteVersionId,
        int Position,
        Guid OrganisationId,
        Guid CommonItemGroupId,
        string Name,
        bool IsEveryNote,
        bool IsPicked);

    /// <summary>
    /// The IDs of A's lists behind <c>AddDraftWithGroupsAsync</c>'s draft: its goal, Every note's item, and the groups
    /// Outings, with two items, Personal care, with one, and Household, with none, in the order of its snapshot.
    /// </summary>
    private sealed record Lists(
        Guid Goal,
        Guid EveryNoteItem,
        Guid Outing,
        Guid OutingItemA,
        Guid OutingItemB,
        Guid Care,
        Guid CareItem,
        Guid Empty);

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
    /// Records the text of each command that updates the header of the note whose ID it is given, as
    /// <see cref="HoldsTheHeaderUpdateOf"/> finds the one it holds. Every command passes.
    /// </summary>
    private sealed class RecordsTheHeaderUpdatesOf(Guid noteId) : DbCommandInterceptor
    {
        private readonly List<string> commands = [];

        /// <summary>Each command's text, in the order sent.</summary>
        public IReadOnlyList<string> Commands => commands;

        // ExecuteUpdate sends its UPDATE as a command that reads no rows, where SaveChanges reads back what it saved.
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (HoldsTheHeaderUpdateOf.UpdatesTheHeaderOf(command, noteId))
            {
                commands.Add(command.CommandText);
            }

            return ValueTask.FromResult(result);
        }
    }
}
