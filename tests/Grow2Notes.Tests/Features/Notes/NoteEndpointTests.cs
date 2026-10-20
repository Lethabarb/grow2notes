using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET {base}</c> (design.md §6.3), called with the test-only sign-in (<see cref="TestSignIn"/>): a submitted
/// note's author, first submitted time and current version, its goals in the snapshot's order with its wording and
/// ticks, the same for a worker and a manager, with no review data (A13, A14); a draft's header, who started it and
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
            ["narrative", "isFlagged", "flagReason", "goals", "commonItemGroups"],
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
                """{"status":"Draft","authorDisplayName":"Alex P.","startedAtUtc":"2026-09-30T23:14:00Z"}""",
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
            ["status", "authorDisplayName", "startedAtUtc", "current"],
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
            ["status", "author", "firstSubmittedAtUtc", "current"],
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
            ["narrative", "isFlagged", "flagReason", "goals", "commonItemGroups"],
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
            ["status", "authorDisplayName", "startedAtUtc"],
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

    private static string PathOf(Guid participantId) => $"/api/participants/{participantId}/notes/2026-10-01";

    private static async Task<JsonElement> ReadAsync(HttpClient client, Guid participantId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(PathOf(participantId), cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
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
