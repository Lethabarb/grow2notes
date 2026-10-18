using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET</c> and <c>PUT {base}/draft</c> (design.md §6.3), called with the test-only sign-in
/// (<see cref="TestSignIn"/>). The <c>GET</c>: with no live note, the template of a new note, with the participant's
/// active goals in the manager's order, unticked, the organisation's guide prompts and the lists' version (§5.7),
/// unless the date is after today (§5.5 item 4), or, for a worker, before yesterday (A65); with the caller's own draft,
/// their working copy, with the wording it started with (A3); and opening the form writes nothing (§3.3). The
/// <c>PUT</c>, the autosave: with no live note, the
/// note, a draft of the caller's, their working copy, the template's goals its snapshot, and
/// <c>note.draft_started</c>, together or not at all (§5.5 item 2), and <c>201</c>; with the caller's own draft, its
/// ticks, text and flag replaced, and <c>200</c> (§5.6); and the body's checks. Each, for anyone else's draft,
/// <c>409 note.in_progress_by_other</c> with its author's name and start time, and for a participant not in the
/// caller's organisation, <c>404</c> (§9.2). The tests add their own participants, goals, users, drafts and, to change
/// guide prompts, organisation (tests/README.md). The endpoint matrix covers who may call them, and
/// <see cref="SignedOutTests"/> what a signed-out caller gets and an address that does not parse.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteDraftEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35), on a whole second: 9:14 am and 9:30 am in Melbourne on 2 October 2026.
    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SavedAtUtc = new(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);

    // 9:14 am on Saturday 3 October 2026 in Melbourne, and two minutes before midnight on the day before there.
    private static readonly DateTimeOffset OnTheThird = new(2026, 10, 2, 23, 14, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset BeforeMidnight = new(2026, 10, 2, 13, 58, 0, TimeSpan.Zero);

    private static readonly DateOnly TheThird = new(2026, 10, 3);

    private static readonly DateOnly TheSecond = new(2026, 10, 2);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // Today in Melbourne, by the app's own clock.
    private DateOnly Today => factory.Services.GetRequiredService<MelbourneClock>().Today();

    [Fact]
    public async Task A_new_note_s_form_is_the_template_with_the_active_goals_in_the_list_s_order_unticked()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Phones her sister", 2),
            new("Makes own breakfast", 0),
            new("Uses the washing machine", 1, ArchivedAtUtc: TestGoals.AddedAtUtc),
            new("Catches the bus", 3, CreatedAtUtc: TestGoals.AddedAtUtc.AddDays(1)),
            new("Waters the garden", 3, CreatedAtUtc: TestGoals.AddedAtUtc));

        var form = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);

        Assert.Equal(
            ["baseVersion", "isSaved", "listsVersion", "guidePrompts", "goals", "narrative", "isFlagged", "flagReason"],
            form.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            (0, false, "", await GuidePromptsAsync()),
            (form.GetProperty("baseVersion").GetInt32(), form.GetProperty("isSaved").GetBoolean(),
                form.GetProperty("narrative").GetString(), form.GetProperty("guidePrompts").GetString()));
        Assert.Equal((false, null), Flag(form));
        Assert.Equal(
            [
                (goals[1], "Makes own breakfast", false),
                (goals[0], "Phones her sister", false),
                (goals[4], "Waters the garden", false),
                (goals[3], "Catches the bus", false),
            ],
            Goals(form));
        Assert.All(
            form.GetProperty("goals").EnumerateArray(),
            goal => Assert.Equal(["id", "text", "isTicked"], goal.EnumerateObject().Select(property => property.Name)));
        Assert.Equal(32, Convert.FromBase64String(form.GetProperty("listsVersion").GetString()!).Length);
    }

    [Fact]
    public async Task A_s_worker_and_manager_get_the_same_template()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddGoalsAsync(seeded.A, participant.Id, new TestGoal("Makes own breakfast", 0));

        var worker = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);
        var manager = await ReadFormAsync(Caller.ManagerOfA, participant.Id, Today);

        Assert.Equal(worker.GetRawText(), manager.GetRawText());
    }

    // With no goals, the form has none to tick, and the note can still be written (design.md §4.3).
    [Fact]
    public async Task A_participant_with_no_goals_gets_a_template_with_none()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);

        var form = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);

        Assert.Empty(form.GetProperty("goals").EnumerateArray());
        Assert.False(string.IsNullOrEmpty(form.GetProperty("listsVersion").GetString()));
    }

    [Fact]
    public async Task One_list_gives_one_lists_version()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));

        var first = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var second = await ListsVersionAsync(Caller.ManagerOfA, participant.Id);

        Assert.Equal(first, second);
    }

    // Each a change that the manager's Goals list makes (design.md §4.8), as the stored rows show it.
    [Theory]
    [InlineData("reworded")]
    [InlineData("added")]
    [InlineData("archived")]
    [InlineData("restored")]
    [InlineData("moved")]
    public async Task The_lists_version_changes_when_a_goal_is(string change)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Phones her sister", 1),
            new("Uses the washing machine", 2, ArchivedAtUtc: TestGoals.AddedAtUtc));
        var before = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);

        if (change == "added")
        {
            await factory.Services.AddGoalsAsync(seeded.A, participant.Id, new TestGoal("Waters the garden", 3));
        }
        else
        {
            await ChangeGoalsAsync(goals, stored =>
            {
                switch (change)
                {
                    case "reworded":
                        stored[0].Text = "Makes own lunch";
                        break;
                    case "archived":
                        stored[1].ArchivedAtUtc = TestGoals.AddedAtUtc;
                        break;
                    case "restored":
                        stored[2].ArchivedAtUtc = null;
                        break;
                    default:
                        (stored[0].SortOrder, stored[1].SortOrder) = (stored[1].SortOrder, stored[0].SortOrder);
                        break;
                }
            });
        }

        Assert.NotEqual(before, await ListsVersionAsync(Caller.WorkerOfA, participant.Id));
    }

    // 14:30 UTC on 3 October is half past midnight on 4 October in Melbourne, where it is a day later than in UTC.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task A_date_after_today_in_Melbourne_is_not_found_while_today_s_form_opens(Caller caller)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 3, 14, 30, 0, TimeSpan.Zero));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);

        using var tomorrow = await client.GetAsync(
            DraftPath(participant.Id, new(2026, 10, 5)), TestContext.Current.CancellationToken);
        using var today = await client.GetAsync(
            DraftPath(participant.Id, new(2026, 10, 4)), TestContext.Current.CancellationToken);

        await tomorrow.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.OK, today.StatusCode);
    }

    // Only a manager may start a note for a day before today (design.md §5.5 item 4), and the refusal of a worker's
    // first save names yesterday (§3.3), so a worker gets no new note's form for a day before yesterday (A65), and
    // yesterday's, of which the refusal is true, still opens.
    [Theory]
    [InlineData(Caller.WorkerOfA, -2, HttpStatusCode.NotFound)]
    [InlineData(Caller.WorkerOfA, -30, HttpStatusCode.NotFound)]
    [InlineData(Caller.WorkerOfA, -1, HttpStatusCode.OK)]
    [InlineData(Caller.ManagerOfA, -2, HttpStatusCode.OK)]
    [InlineData(Caller.ManagerOfA, -30, HttpStatusCode.OK)]
    public async Task A_new_note_s_form_for_a_day_before_yesterday_opens_for_a_manager_alone(
        Caller caller, int days, HttpStatusCode expected)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await client.GetAsync(
            DraftPath(participant.Id, Today.AddDays(days)), TestContext.Current.CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    // The rule is for a new note alone: a worker's own draft of an earlier day still opens, and another user's still
    // says who started it.
    [Fact]
    public async Task A_live_note_for_a_day_before_yesterday_opens_for_a_worker_as_for_today()
    {
        var own = await factory.Services.AddParticipantAsync(seeded.A);
        var others = await factory.Services.AddParticipantAsync(seeded.A);
        var earlier = Today.AddDays(-2);
        await factory.Services.AddDraftAsync(
            seeded.A, new(own.Id, earlier, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        await factory.Services.AddDraftAsync(seeded.A, new(others.Id, earlier, seeded.A.ManagerId, StartedAtUtc));
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        var form = await ReadFormAsync(Caller.WorkerOfA, own.Id, earlier);
        using var response = await client.GetAsync(
            DraftPath(others.Id, earlier), TestContext.Current.CancellationToken);

        Assert.Equal(
            (true, "Jane made toast."),
            (form.GetProperty("isSaved").GetBoolean(), form.GetProperty("narrative").GetString()));
        await response.ReadProblemAsync(HttpStatusCode.Conflict);
    }

    // The goals as the draft started with them, reworded and added to since, the ticks and text as saved, and the
    // prompts as they are now, which its empty box would show (design.md §4.10).
    [Fact]
    public async Task The_author_s_own_draft_is_their_working_copy_with_the_wording_it_started_with()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        await factory.Services.AddDraftAsync(seeded.A, new(
            participant.Id,
            Today,
            seeded.A.WorkerId,
            StartedAtUtc,
            [
                new(NoteSection.Goal, goals[0], null, "Makes own breakfast", true),
                new(NoteSection.Goal, goals[1], null, "Phones her sister", false),
            ],
            "Jane made toast.\nThen a walk to the shops.",
            SavedAtUtc));
        await ChangeGoalsAsync(goals, stored => stored[0].Text = "Makes own lunch");
        await factory.Services.AddGoalsAsync(seeded.A, participant.Id, new TestGoal("Waters the garden", 2));

        var form = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);

        Assert.Equal(
            ["baseVersion", "isSaved", "guidePrompts", "goals", "narrative", "isFlagged", "flagReason", "savedAtUtc"],
            form.EnumerateObject().Select(property => property.Name));
        Assert.Equal((false, null), Flag(form));
        Assert.Equal(
            (0, true, "Jane made toast.\nThen a walk to the shops.", "2026-10-01T23:30:00Z", await GuidePromptsAsync()),
            (form.GetProperty("baseVersion").GetInt32(), form.GetProperty("isSaved").GetBoolean(),
                form.GetProperty("narrative").GetString(), form.GetProperty("savedAtUtc").GetString(),
                form.GetProperty("guidePrompts").GetString()));
        Assert.Equal(
            [(goals[0], "Makes own breakfast", true), (goals[1], "Phones her sister", false)], Goals(form));
    }

    // Only the author may open a draft (design.md §6.3), so another worker, and a manager, who reads it elsewhere, get
    // who started it and when, and nothing that it holds.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task Another_user_s_draft_is_refused_as_in_progress_with_its_author_s_name_and_start_time(
        Caller caller)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, Today, author, StartedAtUtc, Narrative: "Jane made toast."));
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await client.GetAsync(
            DraftPath(participant.Id, Today), TestContext.Current.CancellationToken);

        var body = await response.ReadProblemAsync(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(
            ("note.in_progress_by_other", "Alex P.", "2026-10-01T23:14:00Z"),
            (problem.RootElement.GetProperty("code").GetString(),
                problem.RootElement.GetProperty("authorDisplayName").GetString(),
                problem.RootElement.GetProperty("startedAtUtc").GetString()));
        Assert.DoesNotContain("Jane made toast.", body, StringComparison.Ordinal);
    }

    // A discarded draft frees the day (design.md §5.5 item 5), so the form opens as a new note's.
    [Fact]
    public async Task A_discarded_draft_is_never_opened()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, Today, author, StartedAtUtc, Narrative: "Jane made toast."));
        await DiscardAsync(noteId);

        var form = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);

        Assert.Equal(
            (false, ""), (form.GetProperty("isSaved").GetBoolean(), form.GetProperty("narrative").GetString()));
    }

    [Fact]
    public async Task B_s_manager_does_not_find_A_s_participant_and_nobody_finds_one_that_does_not_exist()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var b = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfB, seeded);
        using var a = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var other = await b.GetAsync(DraftPath(participant.Id, Today), TestContext.Current.CancellationToken);
        using var none = await a.GetAsync(DraftPath(Guid.NewGuid(), Today), TestContext.Current.CancellationToken);

        await other.ReadProblemAsync(HttpStatusCode.NotFound);
        await none.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // A new note's form, the caller's own draft and another user's: none writes a note, a working copy or an event.
    [Fact]
    public async Task Opening_the_form_writes_nothing()
    {
        var fresh = await factory.Services.AddParticipantAsync(seeded.A);
        var ownDraft = await factory.Services.AddParticipantAsync(seeded.A);
        var othersDraft = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(seeded.A, new(ownDraft.Id, Today, seeded.A.WorkerId, StartedAtUtc));
        await factory.Services.AddDraftAsync(seeded.A, new(othersDraft.Id, Today, seeded.A.ManagerId, StartedAtUtc));
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        foreach (var (participantId, status) in new[]
                 {
                     (fresh.Id, HttpStatusCode.OK),
                     (ownDraft.Id, HttpStatusCode.OK),
                     (othersDraft.Id, HttpStatusCode.Conflict),
                 })
        {
            using var response = await client.GetAsync(
                DraftPath(participantId, Today), TestContext.Current.CancellationToken);
            Assert.Equal(status, response.StatusCode);
        }

        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task The_answer_is_JSON_that_is_never_stored()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await client.GetAsync(
            DraftPath(participant.Id, Today), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    // The template's goals in the manager's order, the archived one left out, a goal ID sent twice ticking it once, and
    // an event that holds no note text or goal wording (§5.3, §9.5).
    [Fact]
    public async Task The_first_save_creates_exactly_the_note_its_author_s_working_copy_and_one_event_with_IDs_only()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Phones her sister", 1),
            new("Makes own breakfast", 0),
            new("Uses the washing machine", 2, ArchivedAtUtc: TestGoals.AddedAtUtc));
        var clientId = Guid.NewGuid();
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SaveAsync(
            client, participant.Id, Today, FirstBody(listsVersion, clientId, "Jane made toast.", goals[0], goals[0]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var savedAtUtc = await SavedAtUtcAsync(response);
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal(
            (Today, seeded.A.WorkerId, NoteStatus.Draft, savedAtUtc, 0, FlagStatus.None),
            (note.NoteDate, note.AuthorUserId, note.Status, note.CreatedAtUtc, note.CurrentVersionNumber,
                note.FlagStatus));
        Assert.Equal(
            (null, null, null, null),
            (note.FirstSubmittedAtUtc, note.FlaggedVersionNumber, note.DiscardedAtUtc, note.DiscardedByUserId));
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal(
            (seeded.A.WorkerId, 0, "Jane made toast.", false, null, clientId, 1, savedAtUtc, savedAtUtc),
            (draft.EditorUserId, draft.BaseVersionNumber, draft.Narrative, draft.IsFlagged, draft.FlagReason,
                draft.LastClientId, draft.LastSeq, draft.CreatedAtUtc, draft.UpdatedAtUtc));
        Assert.Equal(
            [
                new DraftItem(NoteSection.Goal, goals[1], null, "Makes own breakfast", false),
                new DraftItem(NoteSection.Goal, goals[0], null, "Phones her sister", true),
            ],
            draft.Items);
        Assert.Empty(draft.Groups);
        Assert.Equal(
            new WrittenEvent(
                AuditEventTypes.Note.DraftStarted, seeded.A.OrganisationId, seeded.A.WorkerId, AuditEntityTypes.Note,
                note.Id, participant.Id, null),
            Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0)));
        var after = await StoredRowsAsync();
        Assert.Equal((before.Notes + 1, before.Drafts + 1), (after.Notes, after.Drafts));
    }

    // The note and the working copy are saved before the event, so only the transaction's rollback takes them away.
    [Fact]
    public async Task A_first_save_whose_event_fails_to_save_leaves_neither_the_note_nor_the_working_copy()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<Grow2NotesDbContext>(
                options => options.AddInterceptors(new FailsTheEventsOf(participant.Id)))));
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var before = await StoredRowsAsync();
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SaveAsync(
            client, participant.Id, Today, FirstBody(listsVersion, Guid.NewGuid(), "Jane made toast."));

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Empty(await NotesAsync(participant.Id));
        Assert.Equal(before, await StoredRowsAsync());
    }

    // The first change fixes the note's date and start time (design.md §3.3, A8): 14:30 UTC on 3 October is half past
    // midnight on 4 October in Melbourne. The clock's time has a part of a millisecond, which the datetime2(3) column
    // cannot hold, so the time answered is the time stored.
    [Fact]
    public async Task A_worker_s_first_save_takes_today_s_date_in_Melbourne_and_the_server_s_time()
    {
        var firstChange = new DateTimeOffset(2026, 10, 3, 14, 30, 0, 456, TimeSpan.Zero).AddTicks(789);
        await using var app = factory.WithWebHostBuilder(
            builder => builder.UseClock(new FakeTimeProvider(firstChange)));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SaveAsync(
            client, participant.Id, new(2026, 10, 4), FirstBody(listsVersion, Guid.NewGuid(), ""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var startedAtUtc = new DateTime(2026, 10, 3, 14, 30, 0, 456, DateTimeKind.Utc);
        Assert.Equal(startedAtUtc, await SavedAtUtcAsync(response));
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal((new DateOnly(2026, 10, 4), startedAtUtc), (note.NoteDate, note.CreatedAtUtc));
    }

    // A later save, as the same page's next one, and a save sent again after its answer was lost, from the page's first
    // save number: each replaces the ticks and text, and the page that saved last, and writes no event (§5.3).
    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    public async Task A_later_save_of_the_author_replaces_the_working_copy_and_answers_200_with_its_time(int seq)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 23, 14, 0, TimeSpan.Zero));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new("Makes own breakfast", 0), new("Phones her sister", 1));
        var clientId = Guid.NewGuid();
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using (var first = await SaveAsync(
                   client, participant.Id, new(2026, 10, 2), FirstBody(listsVersion, clientId, "Jane", goals[0])))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        var before = await StoredRowsAsync();
        clock.Advance(TimeSpan.FromMinutes(16));

        using var later = await SaveAsync(
            client, participant.Id, new(2026, 10, 2), Body(clientId, seq, "Jane made toast.", goals[1]));

        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        var savedAtUtc = new DateTime(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);
        Assert.Equal(savedAtUtc, await SavedAtUtcAsync(later));
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal(StartedAtUtc, note.CreatedAtUtc);
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal(
            ("Jane made toast.", clientId, seq, StartedAtUtc, savedAtUtc),
            (draft.Narrative, draft.LastClientId, draft.LastSeq, draft.CreatedAtUtc, draft.UpdatedAtUtc));
        Assert.Equal([false, true], draft.Items.Select(item => item.IsTicked));
        Assert.Equal(before.LastEventId, (await StoredRowsAsync()).LastEventId);
    }

    // Only the author may save a draft (design.md §6.3), so another worker, and a manager, get who started it and when,
    // before any check that a note's first save makes: the manager's also when the participant was archived after the
    // draft started, and with no lists' version, as no body here has one.
    [Theory]
    [InlineData(Caller.WorkerOfA, false)]
    [InlineData(Caller.ManagerOfA, false)]
    [InlineData(Caller.ManagerOfA, true)]
    public async Task A_save_on_another_user_s_draft_is_refused_as_in_progress_and_writes_nothing(
        Caller caller, bool archived)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, Today, author, StartedAtUtc, Narrative: "Jane made toast."));
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await SaveAsync(client, participant.Id, Today, Body(Guid.NewGuid(), 1, "Jane went out."));

        var body = await response.ReadProblemAsync(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(
            ("note.in_progress_by_other", "Alex P.", "2026-10-01T23:14:00Z"),
            (problem.RootElement.GetProperty("code").GetString(),
                problem.RootElement.GetProperty("authorDisplayName").GetString(),
                problem.RootElement.GetProperty("startedAtUtc").GetString()));
        Assert.Equal(before, await StoredRowsAsync());
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal("Jane made toast.", Assert.Single(await WorkingCopiesAsync(note.Id)).Narrative);
    }

    // design.md §14 M2: "A goal reworded … after a draft was created does not change that draft" (A3), which a later
    // save keeps ticking by the goal's ID.
    [Fact]
    public async Task A_goal_reworded_after_the_draft_started_keeps_its_wording_in_the_draft()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new TestGoal("Makes own breakfast", 0));
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var clientId = Guid.NewGuid();
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        using (var first = await SaveAsync(
                   client, participant.Id, Today, FirstBody(listsVersion, clientId, "", goals[0])))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        await ChangeGoalsAsync(goals, stored => stored[0].Text = "Makes own lunch");
        var reopened = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);
        using (var later = await SaveAsync(client, participant.Id, Today, Body(clientId, 2, "Jane made toast.")))
        {
            Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        }

        Assert.Equal([(goals[0], "Makes own breakfast", true)], Goals(reopened));
        var afterTheLaterSave = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);
        Assert.Equal([(goals[0], "Makes own breakfast", false)], Goals(afterTheLaterSave));
    }

    // The draft's snapshot is its goals (design.md §5.7 *Later saves*), so a goal added since cannot be ticked on it;
    // and a new note's are the template's, so an archived goal cannot be ticked on one.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_ticked_ID_that_is_not_one_of_the_note_s_goals_is_refused_and_changes_nothing(bool started)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A,
            participant.Id,
            new("Makes own breakfast", 0),
            new("Uses the washing machine", 1, ArchivedAtUtc: TestGoals.AddedAtUtc));
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var clientId = Guid.NewGuid();
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var notTheNotes = goals[1];
        if (started)
        {
            using var first = await SaveAsync(
                client, participant.Id, Today, FirstBody(listsVersion, clientId, "Jane", goals[0]));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            notTheNotes = (await factory.Services.AddGoalsAsync(
                seeded.A, participant.Id, new TestGoal("Waters the garden", 2)))[0];
        }

        var before = await StoredRowsAsync();

        using var response = await SaveAsync(
            client,
            participant.Id,
            Today,
            started
                ? Body(clientId, 2, "Jane made toast.", goals[0], notTheNotes)
                : FirstBody(listsVersion, clientId, "Jane made toast.", goals[0], notTheNotes));

        await ReadValidationProblemAsync(response, "tickedGoalIds");
        Assert.Equal(before, await StoredRowsAsync());
    }

    // design.md §4.3 *No goals set up*: the note can still be written.
    [Fact]
    public async Task A_note_of_a_participant_with_no_goals_is_saved()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SaveAsync(
            client, participant.Id, Today, FirstBody(listsVersion, Guid.NewGuid(), "Jane made toast."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = Assert.Single(await NotesAsync(participant.Id));
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal(("Jane made toast.", 0), (draft.Narrative, draft.Items.Count));
    }

    // Limits.GuidedNotes, which only validation enforces, as the column has no size (design.md §5.1, A6).
    [Fact]
    public async Task Guided_notes_of_20000_characters_are_saved_and_of_20001_refused()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var clientId = Guid.NewGuid();
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);

        using var longest = await SaveAsync(
            client, participant.Id, Today, FirstBody(listsVersion, clientId, new string('x', Limits.GuidedNotes)));
        var before = await StoredRowsAsync();
        using var tooLong = await SaveAsync(
            client, participant.Id, Today, Body(clientId, 2, new string('y', Limits.GuidedNotes + 1)));

        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
        await ReadValidationProblemAsync(tooLong, "narrative");
        Assert.Equal(before, await StoredRowsAsync());
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal(new string('x', Limits.GuidedNotes), Assert.Single(await WorkingCopiesAsync(note.Id)).Narrative);
    }

    // design.md §3.6 and §5.3: the working copy keeps the flag and its reason, from the note's first save and a later
    // one, and the form gives them again as it opens (conditional-reveal.md *Behaviour*, a saved draft loaded).
    [Fact]
    public async Task A_flag_and_its_reason_are_saved_and_read_back()
    {
        const string reason = "Jane seemed unwell.";
        const string longerReason = "Jane seemed unwell.\nShe slept after lunch.";
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var clientId = Guid.NewGuid();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using (var first = await SaveAsync(
                   client, participant.Id, Today, FlagBody(listsVersion, clientId, 1, true, reason)))
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        var afterTheFirst = await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today);

        using var later = await SaveAsync(
            client, participant.Id, Today, FlagBody(null, clientId, 2, true, longerReason));

        Assert.Equal(HttpStatusCode.OK, later.StatusCode);
        Assert.Equal((true, reason), Flag(afterTheFirst));
        Assert.Equal((true, longerReason), Flag(await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today)));
        var note = Assert.Single(await NotesAsync(participant.Id));
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal((true, longerReason), (draft.IsFlagged, draft.FlagReason));
    }

    // design.md §5.3: the reason is null unless the note is flagged, whatever the body holds: on the note's first save,
    // and on a later one that unticks a flag saved with a reason.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_unflagged_save_stores_no_reason(bool started)
    {
        const string reason = "Jane seemed unwell.";
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var clientId = Guid.NewGuid();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        if (started)
        {
            using var flagged = await SaveAsync(
                client, participant.Id, Today, FlagBody(listsVersion, clientId, 1, true, reason));
            Assert.Equal(HttpStatusCode.Created, flagged.StatusCode);
        }

        using var response = await SaveAsync(
            client,
            participant.Id,
            Today,
            started ? FlagBody(null, clientId, 2, false, reason) : FlagBody(listsVersion, clientId, 1, false, reason));

        Assert.Equal(started ? HttpStatusCode.OK : HttpStatusCode.Created, response.StatusCode);
        var note = Assert.Single(await NotesAsync(participant.Id));
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal((false, null), (draft.IsFlagged, draft.FlagReason));
        Assert.Equal((false, null), Flag(await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today)));
    }

    // Only Submit needs a reason (design.md §3.4; conditional-reveal.md *Notes*), so a flagged draft is saved without
    // one, as the form sends it while the reason is blank.
    [Fact]
    public async Task A_flag_with_no_reason_is_saved()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SaveAsync(
            client, participant.Id, Today, FlagBody(listsVersion, Guid.NewGuid(), 1, true, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = Assert.Single(await NotesAsync(participant.Id));
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal((true, null), (draft.IsFlagged, draft.FlagReason));
        Assert.Equal((true, null), Flag(await ReadFormAsync(Caller.WorkerOfA, participant.Id, Today)));
    }

    // Limits.FlagReason (A6), the column's size, which validation enforces before the database would.
    [Fact]
    public async Task A_reason_of_200_characters_is_saved_and_of_201_refused()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var clientId = Guid.NewGuid();
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);

        using var longest = await SaveAsync(
            client,
            participant.Id,
            Today,
            FlagBody(listsVersion, clientId, 1, true, new string('x', Limits.FlagReason)));
        var before = await StoredRowsAsync();
        using var tooLong = await SaveAsync(
            client, participant.Id, Today, FlagBody(null, clientId, 2, true, new string('y', Limits.FlagReason + 1)));

        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);
        await ReadValidationProblemAsync(tooLong, "flagReason");
        Assert.Equal(before, await StoredRowsAsync());
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal(new string('x', Limits.FlagReason), Assert.Single(await WorkingCopiesAsync(note.Id)).FlagReason);
    }

    // The validation filter's checks (DraftSaveRequest), before the endpoint looks for the participant or a note.
    [Theory]
    [InlineData("""{"clientId":"5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13","seq":1,"tickedGoalIds":[]}""", "narrative")]
    [InlineData(
        """{"clientId":"5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13","seq":1,"narrative":null,"tickedGoalIds":[]}""",
        "narrative")]
    [InlineData("""{"clientId":"5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13","seq":1,"narrative":""}""", "tickedGoalIds")]
    [InlineData(
        """{"clientId":"5b0e7c3a-1f42-4d8e-9a61-2c7f0b9d4e13","seq":1,"narrative":"","tickedGoalIds":null}""",
        "tickedGoalIds")]
    public async Task A_body_without_its_narrative_or_ticks_is_refused_under_that_field_and_writes_nothing(
        string body, string field)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var request = new HttpRequestMessage(HttpMethod.Put, DraftPath(participant.Id, Today))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await ReadValidationProblemAsync(response, field);
        Assert.Equal(before, await StoredRowsAsync());
    }

    [Fact]
    public async Task A_save_for_B_s_manager_at_A_s_participant_or_for_one_that_does_not_exist_is_not_found()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var before = await StoredRowsAsync();
        using var b = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfB, seeded);
        using var a = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var other = await SaveAsync(b, participant.Id, Today, Body(Guid.NewGuid(), 1, "Jane made toast."));
        using var none = await SaveAsync(a, Guid.NewGuid(), Today, Body(Guid.NewGuid(), 1, "Jane made toast."));

        await other.ReadProblemAsync(HttpStatusCode.NotFound);
        await none.ReadProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(before, await StoredRowsAsync());
    }

    // design.md §5.5 item 4 (D35): nobody starts a note for a day after today in Melbourne, and only a manager one for
    // a day before it, today being the server's.
    [Theory]
    [InlineData(Caller.WorkerOfA, -1, "note.past_date_manager_only")]
    [InlineData(Caller.WorkerOfA, 1, "note.future_date")]
    [InlineData(Caller.ManagerOfA, 1, "note.future_date")]
    public async Task A_first_save_for_a_day_that_the_caller_may_not_start_a_note_for_is_refused_and_creates_nothing(
        Caller caller, int days, string code)
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new(OnTheThird)));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var before = await StoredRowsAsync();
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await SaveAsync(
            client,
            participant.Id,
            TheThird.AddDays(days),
            FirstBody(listsVersion, Guid.NewGuid(), "Jane made toast."));

        await ReadCodeAsync(response, HttpStatusCode.UnprocessableEntity, code);
        Assert.Equal(before, await StoredRowsAsync());
    }

    // A manager may start a note for any day up to today, and becomes its author (design.md §5.5 item 4); T18 builds
    // its screens.
    [Fact]
    public async Task A_manager_s_first_save_for_an_earlier_day_creates_the_note_with_them_as_its_author()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new(OnTheThird)));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.ManagerOfA, participant.Id);
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await SaveAsync(
            client, participant.Id, TheSecond, FirstBody(listsVersion, Guid.NewGuid(), "Jane made toast."));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal(
            (TheSecond, seeded.A.ManagerId, OnTheThird.UtcDateTime),
            (note.NoteDate, note.AuthorUserId, note.CreatedAtUtc));
    }

    // design.md §3.3: the form opened at 11:58 pm, and its first change came at 12:01 am, when the note's date is
    // yesterday's, which a worker may not start.
    [Fact]
    public async Task A_worker_s_first_save_at_12_01_am_of_a_form_opened_at_11_58_pm_is_refused_and_creates_nothing()
    {
        var clock = new FakeTimeProvider(BeforeMidnight);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var listsVersion = await ListsVersionAsync(client, participant.Id, TheSecond);
        var before = await StoredRowsAsync();
        clock.Advance(TimeSpan.FromMinutes(3));

        using var response = await SaveAsync(
            client, participant.Id, TheSecond, FirstBody(listsVersion, Guid.NewGuid(), "Jane made toast."));

        await ReadCodeAsync(response, HttpStatusCode.UnprocessableEntity, "note.past_date_manager_only");
        Assert.Equal(before, await StoredRowsAsync());
    }

    // design.md §5.5 item 4: "The check applies only when the note is created."
    [Fact]
    public async Task A_draft_started_at_11_58_pm_is_saved_at_12_01_am_and_keeps_its_date()
    {
        var clock = new FakeTimeProvider(BeforeMidnight);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var clientId = Guid.NewGuid();
        using var client = app.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using (var started = await SaveAsync(
                   client, participant.Id, TheSecond, FirstBody(listsVersion, clientId, "Jane")))
        {
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        }

        clock.Advance(TimeSpan.FromMinutes(3));

        using var response = await SaveAsync(
            client, participant.Id, TheSecond, Body(clientId, 2, "Jane made toast."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal((TheSecond, BeforeMidnight.UtcDateTime), (note.NoteDate, note.CreatedAtUtc));
        Assert.Equal("Jane made toast.", Assert.Single(await WorkingCopiesAsync(note.Id)).Narrative);
    }

    // An archived participant gets no new notes (design.md §3.7, §5.5 item 4), from a manager either.
    [Theory]
    [InlineData(Caller.WorkerOfA)]
    [InlineData(Caller.ManagerOfA)]
    public async Task An_archived_participant_s_first_save_is_refused_and_creates_nothing(Caller caller)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);
        var listsVersion = await ListsVersionAsync(caller, participant.Id);
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await SaveAsync(
            client, participant.Id, Today, FirstBody(listsVersion, Guid.NewGuid(), "Jane made toast."));

        await ReadCodeAsync(response, HttpStatusCode.UnprocessableEntity, "note.participant_archived");
        Assert.Equal(before, await StoredRowsAsync());
    }

    // design.md §5.7: a goal added since the form opened, so the template has changed, or a first save with no lists'
    // version at all, which never matches.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_first_save_with_a_stale_lists_version_or_none_is_refused_and_creates_nothing(bool stale)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new TestGoal("Makes own breakfast", 0));
        var opened = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        await factory.Services.AddGoalsAsync(seeded.A, participant.Id, new TestGoal("Phones her sister", 1));
        var before = await StoredRowsAsync();
        using var client = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await SaveAsync(
            client,
            participant.Id,
            Today,
            stale
                ? FirstBody(opened, Guid.NewGuid(), "Jane made toast.", goals[0])
                : Body(Guid.NewGuid(), 1, "Jane made toast.", goals[0]));

        await ReadCodeAsync(response, HttpStatusCode.Conflict, "note.lists_changed");
        Assert.Equal(before, await StoredRowsAsync());
    }

    // The creating save's checks in turn (T08's Notes): §5.5 item 4's rules in that item's order, then the lists'
    // version, then the ticks. Each body breaks every check after the one that refuses it: an archived participant, no
    // lists' version, and a ticked ID that is not one of the template's goals.
    [Theory]
    [InlineData(Caller.WorkerOfA, 1, true, "note.future_date")]
    [InlineData(Caller.WorkerOfA, -1, true, "note.past_date_manager_only")]
    [InlineData(Caller.ManagerOfA, -1, true, "note.participant_archived")]
    [InlineData(Caller.ManagerOfA, 0, false, "note.lists_changed")]
    public async Task A_first_save_is_refused_for_the_first_check_that_it_fails(
        Caller caller, int days, bool archived, string code)
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new(OnTheThird)));
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived);
        var before = await StoredRowsAsync();
        using var client = app.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await SaveAsync(
            client, participant.Id, TheThird.AddDays(days), Body(Guid.NewGuid(), 1, "Jane", Guid.NewGuid()));

        await ReadCodeAsync(
            response,
            code == "note.lists_changed" ? HttpStatusCode.Conflict : HttpStatusCode.UnprocessableEntity,
            code);
        Assert.Equal(before, await StoredRowsAsync());
    }

    // design.md §5.5 item 3: two users' first saves for one participant and day, each finding no live note, the first
    // to arrive held just before it saves the note while the second starts the note at 9:14 am. The held save then
    // meets the one-live-note index, and is answered as a save on another user's draft, writing nothing.
    [Fact]
    public async Task Two_users_first_saves_at_once_make_one_note_and_the_other_gets_who_started_it_and_when()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(StartedAtUtc.AddMinutes(-1)));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var noteDate = new DateOnly(2026, 10, 2);
        var before = await StoredRowsAsync();
        var gate = new HoldsTheFirstNoteOf(participant.Id);
        await using var gated = gate.On(app);
        using var worker = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var other = gated.CreateHttpsClient().SignInAs(author);

        var held = SaveAsync(worker, participant.Id, noteDate, FirstBody(listsVersion, Guid.NewGuid(), "Jane"));
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromMinutes(1));
            using var first = await SaveAsync(
                other, participant.Id, noteDate, FirstBody(listsVersion, Guid.NewGuid(), "Jane went out."));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await held;
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        Assert.Equal(
            ("note.in_progress_by_other", "Alex P.", "2026-10-01T23:14:00Z"),
            (problem.RootElement.GetProperty("code").GetString(),
                problem.RootElement.GetProperty("authorDisplayName").GetString(),
                problem.RootElement.GetProperty("startedAtUtc").GetString()));
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal((author, StartedAtUtc), (note.AuthorUserId, note.CreatedAtUtc));
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal((author, "Jane went out."), (draft.EditorUserId, draft.Narrative));
        Assert.Equal(
            new WrittenEvent(
                AuditEventTypes.Note.DraftStarted, seeded.A.OrganisationId, author, AuditEntityTypes.Note, note.Id,
                participant.Id, null),
            Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0)));
    }

    // design.md §5.5 item 3: the same person's first saves from two devices, the first to arrive held while the second
    // starts the note; the held save, finding the note theirs, is applied as a later save, so the note holds it.
    [Fact]
    public async Task One_user_s_two_first_saves_at_once_make_one_note_that_holds_the_save_applied_last()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var goals = await factory.Services.AddGoalsAsync(
            seeded.A, participant.Id, new TestGoal("Makes own breakfast", 0));
        var listsVersion = await ListsVersionAsync(Caller.WorkerOfA, participant.Id);
        var before = await StoredRowsAsync();
        var gate = new HoldsTheFirstNoteOf(participant.Id);
        await using var gated = gate.On(factory);
        using var phone = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        using var laptop = gated.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);
        var phoneId = Guid.NewGuid();

        var held = SaveAsync(
            phone, participant.Id, Today, FirstBody(listsVersion, phoneId, "Jane made toast.", goals[0]));
        try
        {
            await gate.Reached.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            using var first = await SaveAsync(
                laptop, participant.Id, Today, FirstBody(listsVersion, Guid.NewGuid(), "Jane"));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }
        finally
        {
            gate.Release();
        }

        using var response = await held;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var note = Assert.Single(await NotesAsync(participant.Id));
        Assert.Equal(seeded.A.WorkerId, note.AuthorUserId);
        var draft = Assert.Single(await WorkingCopiesAsync(note.Id));
        Assert.Equal(("Jane made toast.", phoneId), (draft.Narrative, draft.LastClientId));
        Assert.Equal([true], draft.Items.Select(item => item.IsTicked));
        Assert.Single(await EventsAfterAsync(before.LastEventId ?? 0));
    }

    // design.md §4.10: "Changes apply straight away to every empty Guided notes box, including open drafts", as the
    // prompts come with each GET {base}/draft (T03's hand-off (2)). In an organisation of the test's own, so that no
    // seeded organisation's prompts change (tests/README.md).
    [Fact]
    public async Task Guide_prompts_saved_after_a_draft_started_come_with_its_next_opening()
    {
        const string newPrompts = "What did Jane enjoy today?\nWho did she see?";
        var managerId = await AddOrganisationAsync();
        using var manager = factory.CreateHttpsClient().SignInAs(managerId);
        var participantId = await AddParticipantThroughTheAppAsync(manager);
        var today = Today;
        var listsVersion = await ListsVersionAsync(manager, participantId, today);
        using (var started = await SaveAsync(
                   manager, participantId, today, FirstBody(listsVersion, Guid.NewGuid(), "")))
        {
            Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        }

        await SaveGuidePromptsAsync(manager, newPrompts);

        using var reopened = await manager.GetAsync(
            DraftPath(participantId, today), TestContext.Current.CancellationToken);
        var form = await reopened.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            (true, "", newPrompts),
            (form.GetProperty("isSaved").GetBoolean(), form.GetProperty("narrative").GetString(),
                form.GetProperty("guidePrompts").GetString()));
    }

    private static string DraftPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/draft");

    // A save's body as the form sends it once one of its saves has been answered, with no lists' version, which only a
    // save that creates the note reads, and the flag unticked, which sends no reason.
    private static object Body(Guid clientId, int seq, string narrative, params Guid[] tickedGoalIds) =>
        new { clientId, seq, narrative, tickedGoalIds, isFlagged = false, flagReason = (string?)null };

    // A note's first save as the form sends it: from a page that has not saved the note before, with the lists'
    // version of the template that the form opened with (design.md §5.7), and the flag unticked.
    private static object FirstBody(
        string? listsVersion, Guid clientId, string narrative, params Guid[] tickedGoalIds) =>
        new
        {
            clientId,
            seq = 1,
            listsVersion,
            narrative,
            tickedGoalIds,
            isFlagged = false,
            flagReason = (string?)null,
        };

    // A save with the flag and the reason given, which the form sends as null while the box is unticked or the reason
    // is blank, and with the lists' version given, which only the note's first save sends.
    private static object FlagBody(
        string? listsVersion, Guid clientId, int seq, bool isFlagged, string? flagReason) =>
        new
        {
            clientId,
            seq,
            listsVersion,
            narrative = "Jane made toast.",
            tickedGoalIds = Array.Empty<Guid>(),
            isFlagged,
            flagReason,
        };

    // Over HTTPS with the caller's antiforgery token, as the SPA saves (tests/README.md).
    private static async Task<HttpResponseMessage> SaveAsync(
        HttpClient client, Guid participantId, DateOnly noteDate, object body) =>
        await client.PutWithTokenAsync(
            DraftPath(participantId, noteDate), await client.FetchTokenAsync(), body, ifMatch: null);

    // The save's answer, {savedAtUtc} alone.
    private static async Task<DateTime> SavedAtUtcAsync(HttpResponseMessage response)
    {
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["savedAtUtc"], answer.EnumerateObject().Select(property => property.Name));
        return answer.GetProperty("savedAtUtc").GetDateTime();
    }

    // A problem with the status and code given (design.md §6.9).
    private static async Task ReadCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(status));
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
    }

    // A 422 validation.failed whose errors name the field alone (design.md §6.1).
    private static async Task ReadValidationProblemAsync(HttpResponseMessage response, string field)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal([field], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
    }

    // The form's flag and its reason, a JSON null, and never left out, when there is none.
    private static (bool IsFlagged, string? FlagReason) Flag(JsonElement form) =>
        (form.GetProperty("isFlagged").GetBoolean(), form.GetProperty("flagReason").GetString());

    // The form's goals, in its order.
    private static List<(Guid Id, string? Text, bool IsTicked)> Goals(JsonElement form) =>
    [
        .. form.GetProperty("goals").EnumerateArray().Select(goal => (
            goal.GetProperty("id").GetGuid(), goal.GetProperty("text").GetString(),
            goal.GetProperty("isTicked").GetBoolean())),
    ];

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private async Task<JsonElement> ReadFormAsync(Caller caller, Guid participantId, DateOnly noteDate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        using var response = await client.GetAsync(DraftPath(participantId, noteDate), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private async Task<string?> ListsVersionAsync(Caller caller, Guid participantId) =>
        (await ReadFormAsync(caller, participantId, Today)).GetProperty("listsVersion").GetString();

    // The lists' version of the template of the participant's new note for the day, as the form opens with it.
    private static async Task<string?> ListsVersionAsync(HttpClient client, Guid participantId, DateOnly noteDate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(DraftPath(participantId, noteDate), cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var form = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return form.GetProperty("listsVersion").GetString();
    }

    // A's guide prompts as they are stored now.
    private async Task<string> GuidePromptsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Organisations
            .Where(organisation => organisation.Id == seeded.A.OrganisationId)
            .Select(organisation => organisation.GuidePrompts)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    // Changes the goals, as A's manager's Goals list changes them, given in the order of their IDs.
    private async Task ChangeGoalsAsync(IReadOnlyList<Guid> goalIds, Action<IReadOnlyList<Goal>> change)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            var stored = await db.Goals.Where(goal => goalIds.Contains(goal.Id)).ToListAsync(cancellationToken);
            change([.. goalIds.Select(id => stored.Single(goal => goal.Id == id))]);
            await db.SaveChangesAsync(cancellationToken);
        }
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

    // The note's working copies, through the app's context under A's tenant.
    private async Task<List<NoteDraft>> WorkingCopiesAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            return await db.NoteDrafts.AsNoTracking()
                .Where(draft => draft.NoteId == noteId)
                .ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    // Read in SQL, so the query filter, which would need a tenant, does not decide what is seen. The tests of the
    // collection run one at a time, so the events after the one given are the test's alone.
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

    // An organisation of the test's own, with one Active manager, as setup leaves them apart from the passkey or
    // password, which the test-only sign-in does not need; and gives the manager's ID. An organisation is the tenant,
    // not tenant-owned, and AspNetUsers is not filtered, so this needs no tenant.
    private async Task<Guid> AddOrganisationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        var organisation = new Organisation { Name = "Note draft test", CreatedAtUtc = StartedAtUtc };
        db.Organisations.Add(organisation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var email = $"{Guid.NewGuid():N}@example.org";
        var manager = new ApplicationUser
        {
            OrganisationId = organisation.Id,
            DisplayName = "Quinn Harper",
            Role = UserRole.Manager,
            Status = UserStatus.Active,
            InvitedAtUtc = StartedAtUtc,
            ActivatedAtUtc = StartedAtUtc,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(manager);
        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Code)));
        return manager.Id;
    }

    // A participant whom the manager adds, as the Participants screen does, and gives their ID.
    private static async Task<Guid> AddParticipantThroughTheAppAsync(HttpClient manager)
    {
        using var added = await manager.PostWithTokenAsync(
            "/api/admin/participants",
            await manager.FetchTokenAsync(),
            new { givenName = "Jane", familyName = "Citizen", dateOfBirth = "1987-03-07" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var body = await added.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    // The manager's organisation's guide prompts saved as text, as the Guide prompts screen saves them: loaded, then
    // put with the ETag that came with them.
    private static async Task SaveGuidePromptsAsync(HttpClient manager, string text)
    {
        const string path = "/api/admin/guide-prompts";
        string etag;
        using (var loaded = await manager.GetAsync(path, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
            etag = loaded.Headers.ETag!.ToString();
        }

        using var saved = await manager.PutWithTokenAsync(path, await manager.FetchTokenAsync(), new { text }, etag);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
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
                       (SELECT MAX([UpdatedAtUtc]) FROM [NoteDraft]) AS [LastSavedAtUtc],
                       (SELECT MAX([Id]) FROM [AuditEvent]) AS [LastEventId]
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>How many notes and working copies there are, the last save of any, and the last event's ID.</summary>
    private sealed record StoredRows(int Notes, int Drafts, DateTime? LastSavedAtUtc, long? LastEventId);

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
    /// Holds back the first save, made inside a transaction, that adds a note of the participant whose ID it is given,
    /// until the test lets it go: a note's first save, once it has found no live note and passed its checks, just
    /// before it inserts the note. Every other save passes, so a test's second first save meanwhile starts the note.
    /// </summary>
    private sealed class HoldsTheFirstNoteOf(Guid participantId) : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int held;

        /// <summary>Completes once the save is held.</summary>
        public Task Reached => reached.Task;

        /// <summary><paramref name="app"/>, with this interceptor on every context it makes.</summary>
        public WebApplicationFactory<Program> On(WebApplicationFactory<Program> app) =>
            app.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<Grow2NotesDbContext>(options => options.AddInterceptors(this))));

        /// <summary>Lets the held save go on.</summary>
        public void Release() => released.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is { Database.CurrentTransaction: not null } context
                && context.ChangeTracker.Entries<Note>().Any(
                    entry => entry.State == EntityState.Added && entry.Entity.ParticipantId == participantId)
                && Interlocked.Exchange(ref held, 1) == 0)
            {
                reached.SetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
