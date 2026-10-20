using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Notes;

/// <summary>
/// <c>GET /api/me/drafts</c> (design.md §6.3), Your unfinished drafts (§4.2 item 2, A17), called with the test-only
/// sign-in (<see cref="TestSignIn"/>): the caller's own working copies of live notes, drafts and pending edits, each
/// as its participant, note date, kind and last save, whatever the date, today's included, and whether or not the
/// participant is archived since (§3.7); never another user's, a discarded draft's kept working copy, a draft after
/// its discard, or another organisation's (§5.3, §9.2); in no promised order, as JSON that is never stored (§6.1), and
/// reading writes nothing (A28). The tests add their own participants, users and notes, and call as users they add, so
/// that each knows every working copy of its caller's (tests/README.md). The endpoint matrix covers who may call it,
/// and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class MyDraftsEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string MyDrafts = "/api/me/drafts";

    // Made up (A35): Wednesday 30 September 2026 at 9:42 am in Melbourne, then Thursday 1 October at 9:00 am, both
    // before daylight saving starts on 4 October.
    private static readonly DateTimeOffset On30SeptemberAt942 = new(2026, 9, 29, 23, 42, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset On1OctoberAt900 = new(2026, 9, 30, 23, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly NoteDate = new(2026, 9, 30);

    private static readonly DateTime StartedAtUtc = new(2026, 9, 29, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 9, 30, 6, 42, 0, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // design.md §4.2 item 2 and A17: a draft started on 30 September is still the worker's on 1 October, listed by its
    // note date, as is the draft they start for today, which Today's list shows already and the client drops
    // (today.md). Each row holds §6.3's members in its order, and nothing of what the note says (A9); its last save is
    // the working copy's, as the save stored it.
    [Fact]
    public async Task A_draft_of_an_earlier_day_and_one_of_today_are_listed_with_their_dates_kind_and_last_saves()
    {
        var clock = new FakeTimeProvider(On30SeptemberAt942);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var worker = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        var yesterdays = await factory.Services.AddParticipantAsync(seeded.A);
        var todays = await factory.Services.AddParticipantAsync(seeded.A);
        using var client = app.CreateHttpsClient().SignInAs(worker);
        await StartAsync(client, yesterdays.Id, NoteDate);
        clock.SetUtcNow(On1OctoberAt900);
        await StartAsync(client, todays.Id, NoteDate.AddDays(1));

        var drafts = await MyDraftsAsync(client);

        Assert.Equal(
            [
                (yesterdays.Id, "Jane", "Citizen", "2026-09-30", "draft", "2026-09-29T23:42:00Z"),
                (todays.Id, "Jane", "Citizen", "2026-10-01", "draft", "2026-09-30T23:00:00Z"),
            ],
            drafts.Select(Row).OrderBy(row => row.NoteDate, StringComparer.Ordinal));
    }

    // design.md §3.7: a draft already started can still be finished once its participant is archived, so it stays
    // listed, as nothing else shows it once they are off Today.
    [Fact]
    public async Task A_draft_for_a_participant_archived_since_is_listed()
    {
        var worker = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);
        await factory.Services.AddDraftAsync(seeded.A, new(participant.Id, NoteDate, worker, StartedAtUtc));
        using var client = factory.CreateHttpsClient().SignInAs(worker);

        var draft = Assert.Single(await MyDraftsAsync(client));

        Assert.Equal(
            (participant.Id, "Jane", "Citizen", "2026-09-30", "draft", "2026-09-29T23:14:00Z"), Row(draft));
    }

    // design.md §5.3: once a note is submitted, its author and any manager may each have a pending edit of their own,
    // as T14 will make them (seeded here), listed to each of them alone as a pending edit, with its own last save.
    [Fact]
    public async Task A_pending_edit_is_listed_to_its_editor_alone_as_a_pending_edit()
    {
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        var manager = await factory.Services.AddUserAsync(seeded.A, UserRole.Manager, "Sam Lee");
        var reader = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, author, StartedAtUtc, SubmittedAtUtc));
        await factory.Services.AddPendingEditAsync(seeded.A, noteId, author, SubmittedAtUtc.AddDays(1));
        await factory.Services.AddPendingEditAsync(seeded.A, noteId, manager, SubmittedAtUtc.AddDays(2));

        var editors = new[] { (author, "2026-10-01T06:42:00Z"), (manager, "2026-10-02T06:42:00Z") };
        foreach (var (editor, savedAtUtc) in editors)
        {
            using var client = factory.CreateHttpsClient().SignInAs(editor);

            var edit = Assert.Single(await MyDraftsAsync(client));

            Assert.Equal((participant.Id, "Jane", "Citizen", "2026-09-30", "pendingEdit", savedAtUtc), Row(edit));
        }

        using var readerClient = factory.CreateHttpsClient().SignInAs(reader);
        Assert.Empty(await MyDraftsAsync(readerClient));
    }

    // design.md §5.3's NoteDraft rules and §9.2: the caller's own live draft alone, never another user's draft, the
    // working copy that a discarded draft keeps, or a draft in another organisation, whose user B's list holds alone.
    [Fact]
    public async Task Another_user_s_draft_a_discarded_draft_and_another_organisation_s_draft_are_never_listed()
    {
        var worker = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        var other = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Alex P.");
        var workerOfB = await factory.Services.AddUserAsync(seeded.B, UserRole.Worker, "Chris Ng");
        var own = await factory.Services.AddParticipantAsync(seeded.A);
        var others = await factory.Services.AddParticipantAsync(seeded.A);
        var discarded = await factory.Services.AddParticipantAsync(seeded.A);
        var ofB = await factory.Services.AddParticipantAsync(seeded.B);
        await factory.Services.AddDraftAsync(seeded.A, new(own.Id, NoteDate, worker, StartedAtUtc));
        await factory.Services.AddDraftAsync(seeded.A, new(others.Id, NoteDate, other, StartedAtUtc));
        var discardedId = await factory.Services.AddDraftAsync(
            seeded.A, new(discarded.Id, NoteDate, worker, StartedAtUtc, Narrative: "Jane made toast."));
        await factory.Services.DiscardAsync(seeded.A, discardedId, StartedAtUtc.AddHours(1));
        await factory.Services.AddDraftAsync(seeded.B, new(ofB.Id, NoteDate, workerOfB, StartedAtUtc));
        using var client = factory.CreateHttpsClient().SignInAs(worker);
        using var clientOfB = factory.CreateHttpsClient().SignInAs(workerOfB);

        var drafts = await MyDraftsAsync(client);
        var draftsOfB = await MyDraftsAsync(clientOfB);

        Assert.Equal([own.Id], drafts.Select(draft => draft.GetProperty("participantId").GetGuid()));
        Assert.Equal([ofB.Id], draftsOfB.Select(draft => draft.GetProperty("participantId").GetGuid()));
    }

    // design.md §5.5 item 5: Discard draft frees the day, and its working copy, which it keeps, is listed no more.
    [Fact]
    public async Task A_draft_discarded_through_Discard_draft_is_listed_no_more()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var worker = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(seeded.A, new(participant.Id, NoteDate, worker, StartedAtUtc));
        using var client = factory.CreateHttpsClient().SignInAs(worker);
        Assert.Single(await MyDraftsAsync(client));
        using var discard = new HttpRequestMessage(HttpMethod.Post, $"{NotePath(participant.Id, NoteDate)}/discard");
        discard.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        using (var discarded = await client.SendAsync(discard, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, discarded.StatusCode);
        }

        Assert.Empty(await MyDraftsAsync(client));
    }

    // design.md §6.1, and §5.4, which lists no event for reading (A28). A caller with no working copy gets an empty
    // list.
    [Fact]
    public async Task The_answer_is_JSON_that_is_never_stored_and_reading_writes_nothing()
    {
        var worker = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        using var client = factory.CreateHttpsClient().SignInAs(worker);
        var lastEventId = await LastEventIdAsync();

        using var response = await client.GetAsync(MyDrafts, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    private static string NotePath(Guid participantId, DateOnly noteDate) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}");

    // The caller's working copies, over HTTPS, as the SPA calls the /api group (tests/README.md).
    private static async Task<List<JsonElement>> MyDraftsAsync(HttpClient client)
    {
        var drafts = await client.GetFromJsonAsync<JsonElement>(MyDrafts, TestContext.Current.CancellationToken);
        return [.. drafts.EnumerateArray()];
    }

    // A row of the list, which holds design.md §6.3's members in its order and nothing more, with its last save as
    // written.
    private static (Guid ParticipantId, string? GivenName, string? FamilyName, string? NoteDate, string? Kind,
        string? SavedAtUtc) Row(JsonElement draft)
    {
        Assert.Equal(
            ["participantId", "givenName", "familyName", "noteDate", "kind", "savedAtUtc"],
            draft.EnumerateObject().Select(property => property.Name));
        return (
            draft.GetProperty("participantId").GetGuid(),
            draft.GetProperty("givenName").GetString(),
            draft.GetProperty("familyName").GetString(),
            draft.GetProperty("noteDate").GetString(),
            draft.GetProperty("kind").GetString(),
            draft.GetProperty("savedAtUtc").GetString());
    }

    // The participant's note for the day started by the client's first save, which the client's clock must take for
    // that day, as the form sends it: from a page of its own, with the template's lists' version, the participant
    // having no goals.
    private static async Task StartAsync(HttpClient client, Guid participantId, DateOnly noteDate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = $"{NotePath(participantId, noteDate)}/draft";
        var form = await client.GetFromJsonAsync<JsonElement>(path, cancellationToken);
        using var started = await client.PutWithTokenAsync(
            path,
            await client.FetchTokenAsync(),
            new
            {
                clientId = Guid.NewGuid(),
                seq = 1,
                listsVersion = form.GetProperty("listsVersion").GetString(),
                narrative = "Jane made toast.",
                tickedGoalIds = Array.Empty<Guid>(),
                isFlagged = false,
                flagReason = (string?)null,
            },
            ifMatch: null);
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes
    // one while a test reads it before and after its request.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
