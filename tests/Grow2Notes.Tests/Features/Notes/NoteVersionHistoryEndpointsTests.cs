using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET {base}/versions</c> and <c>GET {base}/versions/{versionNumber}</c> (design.md §6.3), called with the
/// test-only sign-in (<see cref="TestSignIn"/>): for a manager, every version of a submitted note, newest first, with
/// who saved it, when, and whether it is flagged, and one version in the shape of the note's current version plus who
/// saved it and when, showing only Every note and the groups that version picked (D47); a worker's <c>403</c>;
/// <c>404</c> for no live note, a discarded draft, a draft, another organisation's note (§9.2) and a number the note
/// does not have; and reading writes nothing (A28). The tests add their own participants, users, groups and notes
/// (tests/README.md). The endpoint matrix covers who may call them, and <see cref="SignedOutTests"/> what a
/// signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteVersionHistoryEndpointsTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    // Made up (A35): a note for Thursday 1 October 2026, started at 9:14 am and submitted at 4:42 pm in Melbourne, then
    // saved again an hour and two hours later.
    private static readonly DateOnly NoteDate = new(2026, 10, 1);

    private static readonly DateTime StartedAtUtc = new(2026, 9, 30, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 1, 6, 42, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // Newest first, each with its own creator, time and flag: version 1 is the worker's and flagged, version 2 a
    // manager's and not, and version 3 the worker's again and flagged.
    [Fact]
    public async Task The_list_gives_every_version_newest_first_with_its_own_creator_time_and_flag()
    {
        var note = await AddThreeVersionNoteAsync();

        var list = await GetAsync(Caller.ManagerOfA, VersionsPath(note.ParticipantId));

        Assert.Equal(JsonValueKind.Array, list.ValueKind);
        foreach (var row in list.EnumerateArray())
        {
            Assert.Equal(
                ["versionNumber", "createdBy", "createdAtUtc", "isFlagged"],
                row.EnumerateObject().Select(property => property.Name));
            Assert.Equal(
                ["id", "displayName"],
                row.GetProperty("createdBy").EnumerateObject().Select(property => property.Name));
        }

        Assert.Equal(
            [
                (3, note.Worker, "Priya Nair", SubmittedAtUtc.AddHours(2), true),
                (2, note.Manager, "Sam Okoro", SubmittedAtUtc.AddHours(1), false),
                (1, note.Worker, "Priya Nair", SubmittedAtUtc, true),
            ],
            list.EnumerateArray().Select(row =>
            (
                row.GetProperty("versionNumber").GetInt32(),
                row.GetProperty("createdBy").GetProperty("id").GetGuid(),
                row.GetProperty("createdBy").GetProperty("displayName").GetString(),
                row.GetProperty("createdAtUtc").GetDateTime(),
                row.GetProperty("isFlagged").GetBoolean())));
    }

    // The current version's shape, as GET {base} gives it, and then who saved the version and when.
    [Fact]
    public async Task One_version_gives_the_current_shape_with_who_saved_it_and_when()
    {
        var note = await AddThreeVersionNoteAsync();

        var second = await GetAsync(Caller.ManagerOfA, VersionPath(note.ParticipantId, "2"));
        var first = await GetAsync(Caller.ManagerOfA, VersionPath(note.ParticipantId, "1"));

        Assert.Equal(
            [
                "versionNumber", "narrative", "isFlagged", "flagReason", "goals", "commonItemGroups", "createdBy",
                "createdAtUtc",
            ],
            second.EnumerateObject().Select(property => property.Name));
        Assert.Equal(2, second.GetProperty("versionNumber").GetInt32());
        Assert.Equal("Jane made toast. Then a walk to the shops.", second.GetProperty("narrative").GetString());
        Assert.False(second.GetProperty("isFlagged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("flagReason").ValueKind);
        var createdBy = second.GetProperty("createdBy");
        Assert.Equal(["id", "displayName"], createdBy.EnumerateObject().Select(property => property.Name));
        Assert.Equal(note.Manager, createdBy.GetProperty("id").GetGuid());
        Assert.Equal("Sam Okoro", createdBy.GetProperty("displayName").GetString());
        Assert.Equal(SubmittedAtUtc.AddHours(1), second.GetProperty("createdAtUtc").GetDateTime());
        Assert.True(first.GetProperty("isFlagged").GetBoolean());
        Assert.Equal("Jane seemed unwell.", first.GetProperty("flagReason").GetString());
        Assert.Equal(note.Worker, first.GetProperty("createdBy").GetProperty("id").GetGuid());
    }

    // D47 and A3: each version shows Every note and the one group it picked, with every item, ticked or not, though the
    // other version picked another group.
    [Fact]
    public async Task Each_version_shows_only_Every_note_and_the_group_it_picked_with_every_item()
    {
        var note = await AddThreeVersionNoteAsync();

        var first = await GetAsync(Caller.ManagerOfA, VersionPath(note.ParticipantId, "1"));
        var second = await GetAsync(Caller.ManagerOfA, VersionPath(note.ParticipantId, "2"));

        Assert.Equal(
            [
                ("Every note", true, "Took medication", true),
                ("Community outing", false, "Caught the bus", true),
                ("Community outing", false, "Went to the shop", false),
            ],
            CommonItemGroups(first));
        Assert.Equal(
            [
                ("Every note", true, "Took medication", false),
                ("Personal care", false, "Showered", true),
            ],
            CommonItemGroups(second));
        Assert.DoesNotContain("Personal care", first.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Community outing", second.GetRawText(), StringComparison.Ordinal);
    }

    // The policy is the manager's, so a worker is refused, whoever wrote the note.
    [Fact]
    public async Task A_worker_gets_403_on_both()
    {
        var note = await AddThreeVersionNoteAsync();
        using var client = ClientFor(Caller.WorkerOfA);

        foreach (var path in new[] { VersionsPath(note.ParticipantId), VersionPath(note.ParticipantId, "1") })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // §9.2: another organisation's manager does not find the participant. No note, a discarded draft and a draft have
    // no versions to read: a draft's first version is made by its Submit.
    [Fact]
    public async Task Each_gives_404_for_another_organisation_s_manager_no_note_a_discarded_draft_and_a_draft()
    {
        var note = await AddThreeVersionNoteAsync();
        var none = await factory.Services.AddParticipantAsync(seeded.A);
        var discarded = await factory.Services.AddParticipantAsync(seeded.A);
        var draft = await factory.Services.AddParticipantAsync(seeded.A);
        var discardedId = await factory.Services.AddDraftAsync(
            seeded.A, new(discarded.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));
        await factory.Services.DiscardAsync(seeded.A, discardedId, StartedAtUtc.AddHours(1));
        await factory.Services.AddDraftAsync(
            seeded.A, new(draft.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane made toast."));

        var cases = new[]
        {
            (Caller.ManagerOfB, note.ParticipantId),
            (Caller.ManagerOfA, none.Id),
            (Caller.ManagerOfA, discarded.Id),
            (Caller.ManagerOfA, draft.Id),
        };
        foreach (var (caller, participantId) in cases)
        {
            using var client = ClientFor(caller);
            foreach (var path in new[] { VersionsPath(participantId), VersionPath(participantId, "1") })
            {
                using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

                var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
                Assert.DoesNotContain("Jane made toast.", problem, StringComparison.Ordinal);
            }
        }
    }

    // A number above the current version is not found, before any content is read. Zero, a negative number and what is
    // not a whole number match no route, so they get the unknown path's 404.
    [Fact]
    public async Task A_number_the_note_does_not_have_gives_404()
    {
        var note = await AddThreeVersionNoteAsync();
        using var client = ClientFor(Caller.ManagerOfA);

        using var above = await client.GetAsync(
            VersionPath(note.ParticipantId, "4"), TestContext.Current.CancellationToken);
        await above.ReadProblemAsync(HttpStatusCode.NotFound);
        foreach (var number in new[] { "0", "-1", "1.5", "abc" })
        {
            using var response = await client.GetAsync(
                VersionPath(note.ParticipantId, number), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    // design.md §5.4 lists no event for reading (A28).
    [Fact]
    public async Task Reading_writes_nothing()
    {
        var note = await AddThreeVersionNoteAsync();
        var lastEventId = await LastEventIdAsync();

        await GetAsync(Caller.ManagerOfA, VersionsPath(note.ParticipantId));
        foreach (var number in new[] { "1", "2", "3" })
        {
            await GetAsync(Caller.ManagerOfA, VersionPath(note.ParticipantId, number));
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    /// <summary>
    /// A participant of A's whose note has three versions, and the two users who saved them: version 1 is the
    /// worker's, flagged, and picks Community outing; version 2 is the manager's, not flagged, and picks Personal care;
    /// version 3 is the worker's, flagged again, and picks Personal care. Every note holds an item that version 1
    /// ticks.
    /// </summary>
    private sealed record ThreeVersionNote(Guid ParticipantId, Guid Worker, Guid Manager);

    private async Task<ThreeVersionNote> AddThreeVersionNoteAsync()
    {
        var organisationId = seeded.A.OrganisationId;
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var worker = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        var manager = await factory.Services.AddUserAsync(seeded.A, UserRole.Manager, "Sam Okoro");
        var everyNote = await factory.Services.AddItemsAsync(
            organisationId, seeded.A.EveryNoteGroupId, new TestCommonItem("Took medication", 0));
        var groups = await factory.Services.AddGroupsAsync(
            organisationId, new("Community outing", 0), new("Personal care", 1));
        var outing = await factory.Services.AddItemsAsync(
            organisationId, groups[0], new("Caught the bus", 0), new("Went to the shop", 1));
        var care = await factory.Services.AddItemsAsync(organisationId, groups[1], new TestCommonItem("Showered", 0));
        var noteId = await factory.Services.AddSubmittedAsync(seeded.A, new(
            participant.Id,
            NoteDate,
            worker,
            StartedAtUtc,
            SubmittedAtUtc,
            IsFlagged: true,
            FlagReason: "Jane seemed unwell.",
            Groups:
            [
                new(seeded.A.EveryNoteGroupId, "Every note", true, false, [new(everyNote[0], "Took medication", true)]),
                new(
                    groups[0],
                    "Community outing",
                    false,
                    true,
                    [new(outing[0], "Caught the bus", true), new(outing[1], "Went to the shop", false)]),
                new(groups[1], "Personal care", false, false, [new(care[0], "Showered", false)]),
            ]));
        IReadOnlyList<TestVersionGroup> carePicked =
        [
            new(seeded.A.EveryNoteGroupId, "Every note", true, false, [new(everyNote[0], "Took medication", false)]),
            new(groups[0], "Community outing", false, false, [new(outing[0], "Caught the bus", false)]),
            new(groups[1], "Personal care", false, true, [new(care[0], "Showered", true)]),
        ];
        await factory.Services.AddVersionAsync(
            seeded.A, noteId, 2, SubmittedAtUtc.AddHours(1), flagReason: null, flaggedVersionNumber: 1, carePicked,
            manager);
        await factory.Services.AddVersionAsync(
            seeded.A, noteId, 3, SubmittedAtUtc.AddHours(2), "Still unwell.", flaggedVersionNumber: 3, carePicked,
            worker);
        return new(participant.Id, worker, manager);
    }

    // The items of a version's shown groups, in their order, each as its group's name and whether it is Every note,
    // and its own wording and tick. A tuple holding a list would compare the list by reference.
    private static List<(string? Name, bool IsEveryNote, string? Text, bool IsTicked)> CommonItemGroups(
        JsonElement version) =>
    [
        .. version.GetProperty("commonItemGroups").EnumerateArray().SelectMany(group =>
        {
            Assert.Equal(["name", "isEveryNote", "items"], group.EnumerateObject().Select(property => property.Name));
            var name = group.GetProperty("name").GetString();
            var isEveryNote = group.GetProperty("isEveryNote").GetBoolean();
            return group.GetProperty("items").EnumerateArray().Select(item =>
            {
                Assert.Equal(["text", "isTicked"], item.EnumerateObject().Select(property => property.Name));
                return (
                    name,
                    isEveryNote,
                    item.GetProperty("text").GetString(),
                    item.GetProperty("isTicked").GetBoolean());
            });
        }),
    ];

    private static string VersionsPath(Guid participantId) =>
        $"/api/participants/{participantId}/notes/2026-10-01/versions";

    private static string VersionPath(Guid participantId, string versionNumber) =>
        $"{VersionsPath(participantId)}/{versionNumber}";

    // Over HTTPS, as the SPA calls the /api group (tests/README.md).
    private HttpClient ClientFor(Caller caller) => factory.CreateHttpsClient().SignInAs(caller, seeded);

    private async Task<JsonElement> GetAsync(Caller caller, string path)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = ClientFor(caller);

        using var response = await client.GetAsync(path, cancellationToken);

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
}
