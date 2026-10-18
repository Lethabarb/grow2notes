using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET {base}/draft</c> (design.md §6.3), called with the test-only sign-in (<see cref="TestSignIn"/>): with no live
/// note, the template of a new note, with the participant's active goals in the manager's order, unticked, the
/// organisation's guide prompts and the lists' version (§5.7), unless the date is after today (§5.5 item 4); with the
/// caller's own draft, their working copy, with the wording it started with (A3); with anyone else's draft,
/// <c>409 note.in_progress_by_other</c> with its author's name and start time; for a participant not in the caller's
/// organisation, <c>404</c> (§9.2). Opening the form writes nothing (§3.3). The tests add their own participants,
/// goals and drafts (tests/README.md). The endpoint matrix covers who may call it, and <see cref="SignedOutTests"/>
/// what a signed-out caller gets and an address that does not parse.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteDraftEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35), on a whole second: 9:14 am and 9:30 am in Melbourne on 2 October 2026.
    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SavedAtUtc = new(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc);

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
            ["baseVersion", "isSaved", "listsVersion", "guidePrompts", "goals", "narrative"],
            form.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            (0, false, "", await GuidePromptsAsync()),
            (form.GetProperty("baseVersion").GetInt32(), form.GetProperty("isSaved").GetBoolean(),
                form.GetProperty("narrative").GetString(), form.GetProperty("guidePrompts").GetString()));
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
            ["baseVersion", "isSaved", "guidePrompts", "goals", "narrative", "savedAtUtc"],
            form.EnumerateObject().Select(property => property.Name));
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

    private static string DraftPath(Guid participantId, DateOnly noteDate) =>
        string.Create(
            CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/draft");

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
}
