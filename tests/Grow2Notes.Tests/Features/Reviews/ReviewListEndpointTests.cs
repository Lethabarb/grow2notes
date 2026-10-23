using System.Data.SqlTypes;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Features.Notes;
using Grow2Notes.Web.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Reviews;

/// <summary>
/// <c>GET /api/reviews?status=reviewed</c> (design.md §6.4), the Reviewed list, is covered after the To review list's
/// tests: a page of at most 30 reviews, newest first, for every manager of the organisation and for none of another,
/// with the cursor that pages it, the reason of the flag each review cleared, and no write. The tests of its paging and
/// of what a list holds exactly add an organisation of their own (<see cref="TestOrganisations"/>), as the seeded
/// organisations' reviews are shared with other tests.
/// <c>GET /api/reviews?status=toReview</c> (design.md §6.4), the To review list, called with the test-only sign-in
/// (<see cref="TestSignIn"/>): the organisation's submitted notes in To review, for every manager of it and for none of
/// another (§5.9 item 7), never a draft, flagged or not, an unflagged note or a reviewed one (§3.6); each with its
/// participant, date and author, the reason and time of its flagged version, whatever a later version says, and
/// whether the current version removed the flag (A15); oldest flag first, then by note ID; an archived participant's
/// too, with a deactivated author's name (§3.7); <c>400</c> for a missing or unknown <c>status</c>; as JSON that is
/// never stored (§6.1), and reading writes nothing (A28). The tests add their own participants, users and notes
/// (tests/README.md), and read their own participants' rows alone, as other tests flag notes in A. The endpoint matrix
/// covers who may call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class ReviewListEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string ToReviewPath = "/api/reviews?status=toReview";

    private const string ReviewedPath = "/api/reviews?status=reviewed";

    private const string Reason = "Jane seemed unwell.";

    // 4:42 pm on Tuesday 29 September 2026 in Melbourne, still in standard time (UTC+10), with a part of a millisecond,
    // which the datetime2(3) columns cannot hold, when a test's submit saves version 1.
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 29, 6, 42, 13, 456, TimeSpan.Zero)
        .AddTicks(7890);

    // Made up (A35): a note for 29 September 2026, started at 9:14 am and submitted at 4:42 pm in Melbourne, and edited
    // at 10:05 am the next day.
    private static readonly DateOnly NoteDate = new(2026, 9, 29);

    private static readonly DateTime StartedAtUtc = new(2026, 9, 28, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 9, 29, 6, 42, 0, DateTimeKind.Utc);

    private static readonly DateTime EditedAtUtc = new(2026, 9, 30, 0, 5, 0, DateTimeKind.Utc);

    // 9:42 am on the Monday after, in Melbourne, to the millisecond, as a datetime2(3) column holds it.
    private static readonly DateTimeOffset ReviewedAt = new(2026, 10, 4, 23, 42, 0, 123, TimeSpan.Zero);

    private static readonly DateTime ReviewedAtUtc = ReviewedAt.UtcDateTime;

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // §4.6 item 2: a note that a worker submits flagged reaches every manager of the organisation, a second one added
    // to A as well as the seeded one, each listing it with its participant's names, its date, its author, the reason
    // and version 1's time as stored, and its current version, whose flag is not removed; and no manager of another
    // organisation. The row holds nothing of what the note says but its reason (A9).
    [Fact]
    public async Task A_flagged_submit_is_listed_for_every_manager_of_the_organisation_and_for_no_other()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Priya Nair");
        var secondManager = await factory.Services.AddUserAsync(seeded.A, UserRole.Manager, "Alex Morgan");
        await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, author, StartedAtUtc, Narrative: "Jane"));
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(Now)));
        using (var worker = app.CreateHttpsClient().SignInAs(author))
        using (var submitted = await SubmitFlaggedAsync(worker, participant.Id, Reason))
        {
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        }

        foreach (var manager in new[] { seeded.A.ManagerId, secondManager })
        {
            var row = Assert.Single(RowsOf(await ToReviewAsync(app, manager), participant.Id));

            Assert.Equal(
                [
                    "participantId", "givenName", "familyName", "noteDate", "authorDisplayName", "flagReason",
                    "flaggedAtUtc", "currentVersionNumber", "flagRemovedLater",
                ],
                row.EnumerateObject().Select(property => property.Name));
            Assert.Equal(
                ("Jane", "Citizen", "2026-09-29", "Priya Nair", Reason, "2026-09-29T06:42:13.456Z", 1, false),
                (
                    row.GetProperty("givenName").GetString(),
                    row.GetProperty("familyName").GetString(),
                    row.GetProperty("noteDate").GetString(),
                    row.GetProperty("authorDisplayName").GetString(),
                    row.GetProperty("flagReason").GetString(),
                    row.GetProperty("flaggedAtUtc").GetString(),
                    row.GetProperty("currentVersionNumber").GetInt32(),
                    row.GetProperty("flagRemovedLater").GetBoolean()));
            Assert.DoesNotContain("toast", row.GetRawText(), StringComparison.Ordinal);
        }

        Assert.Empty(RowsOf(await ToReviewAsync(app, seeded.B.ManagerId), participant.Id));
    }

    // S03.01.01 end to end: Submit refuses a flagged note with no reason or a blank one (§3.6), so the note never
    // reaches To review (§14 M3 Scope: "the required flag reason").
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_flagged_submit_refused_for_a_blank_reason_is_never_listed(string? reason)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane"));
        using var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var submitted = await SubmitFlaggedAsync(worker, participant.Id, reason);

        await submitted.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Empty(RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), participant.Id));
    }

    // Only a submitted note in To review is listed (§3.6, §6.4): never a draft saved with the flag ticked, as a flag on
    // a draft is not seen, a note submitted unflagged, or one already reviewed; nor a draft or a discarded draft whose
    // flag status says To review, set by hand, as no save sets it on either. A flagged submit beside them is listed, so
    // the test cannot pass on a list that holds nothing.
    [Fact]
    public async Task Only_a_submitted_note_in_To_review_is_listed()
    {
        var flaggedDraft = await factory.Services.AddParticipantAsync(seeded.A);
        using (var worker = factory.CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded))
        using (var saved = await SaveFlaggedDraftAsync(worker, flaggedDraft.Id))
        {
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        }

        var unflagged = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(
            seeded.A, new(unflagged.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        var reviewed = await factory.Services.AddParticipantAsync(seeded.A);
        await factory.Services.AddSubmittedAsync(seeded.A, new(
            reviewed.Id,
            NoteDate,
            seeded.A.WorkerId,
            StartedAtUtc,
            SubmittedAtUtc,
            IsFlagged: true,
            FlagReason: Reason,
            FlagStatus: FlagStatus.Reviewed));
        var draftSetByHand = await factory.Services.AddParticipantAsync(seeded.A);
        await SetToReviewByHandAsync(await factory.Services.AddDraftAsync(
            seeded.A, new(draftSetByHand.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc)));
        var discardedSetByHand = await factory.Services.AddParticipantAsync(seeded.A);
        var discarded = await factory.Services.AddDraftAsync(
            seeded.A, new(discardedSetByHand.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc));
        await factory.Services.DiscardAsync(seeded.A, discarded, StartedAtUtc.AddHours(1));
        await SetToReviewByHandAsync(discarded);
        var flagged = await factory.Services.AddParticipantAsync(seeded.A);
        await AddFlaggedAsync(flagged.Id, NoteDate, SubmittedAtUtc);

        var rows = RowsOf(
            await ToReviewAsync(factory, seeded.A.ManagerId),
            flaggedDraft.Id,
            unflagged.Id,
            reviewed.Id,
            draftSetByHand.Id,
            discardedSetByHand.Id,
            flagged.Id);

        Assert.Equal([flagged.Id], ParticipantIds(rows));
    }

    // §6.4 and A15: the reason and time are the flagged version's, version 1 here, while version 2, added by hand as
    // Save changes will add it, removes the flag, or changes the reason with FlaggedVersionNumber left at 1 by hand, so
    // that only the flagged version can give version 1's; and flagRemovedLater says whether version 2, the current
    // one, is not flagged.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_reason_and_time_stay_the_flagged_version_s_when_a_later_one_changes_or_removes_the_flag(
        bool laterIsFlagged)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, NoteDate, SubmittedAtUtc);
        await factory.Services.AddVersion2Async(
            seeded.A,
            noteId,
            EditedAtUtc,
            laterIsFlagged ? "Jane seemed unwell after lunch." : null,
            flaggedVersionNumber: 1);

        var row = Assert.Single(RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), participant.Id));

        Assert.Equal((Reason, SubmittedAtUtc, 2, !laterIsFlagged), FlagOf(row));
    }

    // §5.8's flag rules: version 2, flagged with another reason, is the note's flagged version, whose reason and time
    // are then listed, its flag not removed.
    [Fact]
    public async Task Once_version_2_is_the_flagged_version_its_reason_and_time_are_listed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, NoteDate, SubmittedAtUtc);
        await factory.Services.AddVersion2Async(
            seeded.A, noteId, EditedAtUtc, "Jane seemed unwell after lunch.", flaggedVersionNumber: 2);

        var row = Assert.Single(RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), participant.Id));

        Assert.Equal(("Jane seemed unwell after lunch.", EditedAtUtc, 2, false), FlagOf(row));
    }

    // §4.6 item 2's "oldest first" is by the time each note was flagged, which its row shows (S03.03.01's Notes): here
    // 9:01 am, 11:20 am and 4:12 pm on 1 October 2026 in Melbourne, though their note dates, and the order in which
    // they were added, which their IDs follow, give others.
    [Fact]
    public async Task Rows_are_listed_oldest_flag_first_whatever_their_note_dates()
    {
        var afternoon = await factory.Services.AddParticipantAsync(seeded.A);
        var morning = await factory.Services.AddParticipantAsync(seeded.A);
        var midday = await factory.Services.AddParticipantAsync(seeded.A);
        await AddFlaggedAsync(afternoon.Id, new(2026, 9, 30), new(2026, 10, 1, 6, 12, 0, DateTimeKind.Utc));
        await AddFlaggedAsync(morning.Id, new(2026, 10, 1), new(2026, 9, 30, 23, 1, 0, DateTimeKind.Utc));
        await AddFlaggedAsync(midday.Id, new(2026, 9, 29), new(2026, 10, 1, 1, 20, 0, DateTimeKind.Utc));

        var rows = RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), afternoon.Id, morning.Id, midday.Id);

        Assert.Equal([morning.Id, midday.Id, afternoon.Id], ParticipantIds(rows));
    }

    // §6.4: the time is the flagged version's, not the note's first submit or its start: a note submitted flagged at
    // 4:42 pm on 29 September 2026 in Melbourne, then flagged again with another reason in version 2 at 10:05 am the
    // next day, as a re-flag leaves it (§5.8's flag rules), is listed after one flagged at 8:30 am that day, though it
    // was started, submitted and added first.
    [Fact]
    public async Task A_note_flagged_again_in_version_2_is_listed_by_that_version_s_time_not_its_first_submit()
    {
        var flaggedAgain = await factory.Services.AddParticipantAsync(seeded.A);
        var flaggedBetween = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(flaggedAgain.Id, NoteDate, SubmittedAtUtc);
        await factory.Services.AddVersion2Async(
            seeded.A, noteId, EditedAtUtc, "Jane seemed unwell after lunch.", flaggedVersionNumber: 2);
        await AddFlaggedAsync(flaggedBetween.Id, NoteDate, new(2026, 9, 29, 22, 30, 0, DateTimeKind.Utc));

        var rows = RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), flaggedAgain.Id, flaggedBetween.Id);

        Assert.Equal([flaggedBetween.Id, flaggedAgain.Id], ParticipantIds(rows));
    }

    // Two notes flagged in the same millisecond are listed by note ID, in SQL Server's order of a uniqueidentifier, so
    // that each call lists them alike. SQL Server gives them in that order without the ORDER BY's Id step too, so these
    // rows cannot show the step is there: ReviewListOrderTests reads it in the query.
    [Fact]
    public async Task Notes_flagged_in_the_same_millisecond_are_listed_by_note_ID()
    {
        var first = await factory.Services.AddParticipantAsync(seeded.A);
        var second = await factory.Services.AddParticipantAsync(seeded.A);
        var notes = new[]
        {
            (ParticipantId: first.Id, NoteId: await AddFlaggedAsync(first.Id, NoteDate, SubmittedAtUtc)),
            (ParticipantId: second.Id, NoteId: await AddFlaggedAsync(second.Id, NoteDate, SubmittedAtUtc)),
        };

        var rows = RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), first.Id, second.Id);

        Assert.Equal(
            notes.OrderBy(note => new SqlGuid(note.NoteId)).Select(note => note.ParticipantId), ParticipantIds(rows));
    }

    // A flag leaves To review only through a review (§3.6, A15), and archiving changes no history (§3.7).
    [Fact]
    public async Task An_archived_participant_s_flagged_note_is_listed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);
        await AddFlaggedAsync(participant.Id, NoteDate, SubmittedAtUtc);

        var rows = RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), participant.Id);

        Assert.Equal([participant.Id], ParticipantIds(rows));
    }

    // §3.7: a deactivated user's notes keep their name.
    [Fact]
    public async Task A_deactivated_author_keeps_their_name_on_the_row()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var author = await factory.Services.AddUserAsync(seeded.A, UserRole.Worker, "Sam Lee");
        await AddFlaggedAsync(participant.Id, NoteDate, SubmittedAtUtc, author);
        await DeactivateAsync(author);

        var row = Assert.Single(RowsOf(await ToReviewAsync(factory, seeded.A.ManagerId), participant.Id));

        Assert.Equal("Sam Lee", row.GetProperty("authorDisplayName").GetString());
    }

    // status is required, and toReview, as §6.4 writes it, its one value until T19 adds reviewed: a request without
    // it, or with another value, in another case too, gets a 400 problem.
    [Theory]
    [InlineData("")]
    [InlineData("?status=")]
    [InlineData("?status=ToReview")]
    [InlineData("?status=flagged")]
    public async Task A_missing_or_unknown_status_gets_a_400_problem_that_is_never_stored(string query)
    {
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.GetAsync("/api/reviews" + query, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task The_answer_is_a_JSON_array_that_is_never_stored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.GetAsync(ToReviewPath, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(JsonValueKind.Array, answer.ValueKind);
    }

    // design.md §5.4 lists no event for reading (A28).
    [Fact]
    public async Task Reading_writes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        await AddFlaggedAsync(participant.Id, NoteDate, SubmittedAtUtc);
        var lastEventId = await LastEventIdAsync();

        await AddReviewedAsync(seeded.A, (await factory.Services.AddParticipantAsync(seeded.A)).Id, ReviewedAtUtc);

        foreach (var manager in new[] { seeded.A.ManagerId, seeded.B.ManagerId })
        {
            await ToReviewAsync(factory, manager);
            using var client = factory.CreateHttpsClient().SignInAs(manager);
            await ReviewedPagesAsync(client);
        }

        Assert.Equal(lastEventId, await LastEventIdAsync());
    }

    // §6.4 and §2: the Reviewed list is the managers' of the organisation, for every one of them and for no other
    // (§5.9 item 7): a signed-out caller gets the session cookie's 401 and a worker a 403, both never stored; A's
    // manager lists A's review across the pages, and B's manager none of A's.
    [Theory]
    [InlineData(Caller.SignedOut, HttpStatusCode.Unauthorized, false)]
    [InlineData(Caller.WorkerOfA, HttpStatusCode.Forbidden, false)]
    [InlineData(Caller.ManagerOfA, HttpStatusCode.OK, true)]
    [InlineData(Caller.ManagerOfB, HttpStatusCode.OK, false)]
    public async Task The_Reviewed_list_is_the_managers_of_the_organisation_s_alone(
        Caller caller, HttpStatusCode expected, bool listsTheReview)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var reviewId = await AddReviewedAsync(seeded.A, participant.Id, ReviewedAtUtc);
        using var client = factory.CreateHttpsClient().SignInAs(caller, seeded);

        if (expected == HttpStatusCode.OK)
        {
            var items = ItemsOf(await ReviewedPagesAsync(client));

            Assert.Equal(listsTheReview, items.Any(item => item.GetProperty("reviewId").GetGuid() == reviewId));
            Assert.Equal(
                listsTheReview, items.Any(item => item.GetProperty("participantId").GetGuid() == participant.Id));
        }
        else
        {
            using var response = await client.GetAsync(ReviewedPath, TestContext.Current.CancellationToken);

            await response.ReadProblemAsync(expected);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        }
    }

    // The cursor is both values or neither: one alone is a 400 problem, as an unknown status is.
    [Theory]
    [InlineData("&before=2026-10-04T23:42:00.123Z")]
    [InlineData("&beforeId=3f2504e0-4f89-41d3-9a0c-0305e82c3301")]
    public async Task Only_one_of_before_and_beforeId_gets_a_400_problem_that_is_never_stored(string cursor)
    {
        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.GetAsync(ReviewedPath + cursor, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    // The answer is the page, {items, hasMore}, never stored, and an item has the ten properties of §6.4 and the
    // review's ID, in order: the reason whole, with its line break; the reviewer's name as a string; the time with its
    // Z, to the millisecond; the comment with its line break, or null; and nothing of what the note says (A9).
    [Fact]
    public async Task A_review_is_listed_with_its_note_s_participant_author_reason_reviewer_time_and_comment()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation);
        var other = await factory.Services.AddParticipantAsync(organisation);
        var author = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Sam Lee");
        var reviewer = await factory.Services.AddUserAsync(organisation, UserRole.Manager, "Jo Smith");
        var reason = "Jane seemed unwell.\nShe went home early.";
        var commented = await AddReviewedAsync(
            organisation, participant.Id, ReviewedAtUtc, "Called Sam's mum.\nBooked the GP.", author, reviewer, reason);
        var uncommented = await AddReviewedAsync(
            organisation, other.Id, ReviewedAtUtc.AddMinutes(-1), comment: null, author, reviewer);
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        using var response = await client.GetAsync(ReviewedPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("ETag"));
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["items", "hasMore"], page.EnumerateObject().Select(property => property.Name));
        Assert.False(page.GetProperty("hasMore").GetBoolean());
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([commented, uncommented], items.Select(item => item.GetProperty("reviewId").GetGuid()));
        var first = items[0];
        Assert.Equal(
            [
                "reviewId", "participantId", "givenName", "familyName", "noteDate", "authorDisplayName", "flagReason",
                "reviewedBy", "reviewedAtUtc", "comment",
            ],
            first.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            (participant.Id, "Jane", "Citizen", "2026-09-29", "Sam Lee", reason, "Jo Smith"),
            (
                first.GetProperty("participantId").GetGuid(),
                first.GetProperty("givenName").GetString(),
                first.GetProperty("familyName").GetString(),
                first.GetProperty("noteDate").GetString(),
                first.GetProperty("authorDisplayName").GetString(),
                first.GetProperty("flagReason").GetString(),
                first.GetProperty("reviewedBy").GetString()));
        Assert.Equal("2026-10-04T23:42:00.123Z", first.GetProperty("reviewedAtUtc").GetString());
        Assert.Equal("Called Sam's mum.\nBooked the GP.", first.GetProperty("comment").GetString());
        Assert.Equal(JsonValueKind.Null, items[1].GetProperty("comment").ValueKind);
        Assert.DoesNotContain("toast", page.GetRawText(), StringComparison.Ordinal);
    }

    // One row per review, not per note: a note reviewed twice is listed twice, and all are newest first.
    [Fact]
    public async Task Reviews_are_listed_one_per_review_newest_first()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var first = await factory.Services.AddParticipantAsync(organisation);
        var second = await factory.Services.AddParticipantAsync(organisation);
        var firstNote = await AddReviewedNoteAsync(organisation, first.Id);
        var secondNote = await AddReviewedNoteAsync(organisation, second.Id);
        var oldest = await factory.Services.AddReviewAsync(
            organisation, firstNote, 1, organisation.ManagerId, ReviewedAtUtc.AddHours(-3));
        var newest = await factory.Services.AddReviewAsync(
            organisation, firstNote, 1, organisation.ManagerId, ReviewedAtUtc);
        var middle = await factory.Services.AddReviewAsync(
            organisation, secondNote, 1, organisation.ManagerId, ReviewedAtUtc.AddHours(-1));
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        var items = ItemsOf(await ReviewedPagesAsync(client));

        Assert.Equal([newest, middle, oldest], items.Select(item => item.GetProperty("reviewId").GetGuid()));
        Assert.Equal(
            [first.Id, second.Id, first.Id], items.Select(item => item.GetProperty("participantId").GetGuid()));
    }

    // §3.7: archiving changes no history, and a deactivated user's name stays on what they did.
    [Fact]
    public async Task An_archived_participant_s_review_is_listed_and_deactivated_users_keep_their_names()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation, archived: true);
        var author = await factory.Services.AddUserAsync(organisation, UserRole.Worker, "Sam Lee");
        var reviewer = await factory.Services.AddUserAsync(organisation, UserRole.Manager, "Jo Smith");
        await AddReviewedAsync(organisation, participant.Id, ReviewedAtUtc, comment: null, author, reviewer);
        await DeactivateAsync(author);
        await DeactivateAsync(reviewer);
        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);

        var item = Assert.Single(ItemsOf(await ReviewedPagesAsync(client)));

        Assert.Equal(
            (participant.Id, "Sam Lee", "Jo Smith"),
            (
                item.GetProperty("participantId").GetGuid(),
                item.GetProperty("authorDisplayName").GetString(),
                item.GetProperty("reviewedBy").GetString()));
    }

    // A15 and S03.03.02, through the real endpoints: a review keeps the reason of the flag it cleared, whatever the
    // note is flagged with next. Flagged with the first reason and reviewed, the note is flagged again with another:
    // it is back in To review with the second, and Reviewed keeps the earlier row with the first. Reviewed again, it
    // has both rows, the second reason's newest.
    [Fact]
    public async Task A_note_flagged_again_after_a_review_keeps_the_earlier_row_with_its_reason()
    {
        var clock = new FakeTimeProvider(ReviewedAt);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, NoteDate, SubmittedAtUtc);
        await factory.Services.AddPendingEditAsync(seeded.A, noteId, seeded.A.ManagerId, EditedAtUtc);
        using var manager = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using (var reviewed = await ReviewAsync(manager, participant.Id, 1))
        {
            Assert.Equal(HttpStatusCode.Created, reviewed.StatusCode);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        using (var saved = await SaveChangesAsync(manager, participant.Id, 1, "Jane seemed worse."))
        {
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        }

        var inToReview = Assert.Single(RowsOf(await ToReviewAsync(app, seeded.A.ManagerId), participant.Id));
        Assert.Equal("Jane seemed worse.", inToReview.GetProperty("flagReason").GetString());
        Assert.Equal([Reason], await ReviewedReasonsAsync(manager, participant.Id));
        clock.Advance(TimeSpan.FromMinutes(1));

        using (var reviewedAgain = await ReviewAsync(manager, participant.Id, 2))
        {
            Assert.Equal(HttpStatusCode.Created, reviewedAgain.StatusCode);
        }

        Assert.Equal(["Jane seemed worse.", Reason], await ReviewedReasonsAsync(manager, participant.Id));
    }

    // A15: version 2 took the tick off the flag version 1 raised, and the note stayed in To review with version 1's
    // reason. The review of version 2 shows that reason, the one To review showed, though version 2 has none.
    [Fact]
    public async Task A_review_of_a_version_that_removed_the_flag_shows_the_reason_To_review_showed()
    {
        var clock = new FakeTimeProvider(ReviewedAt);
        await using var app = factory.WithWebHostBuilder(builder => builder.UseClock(clock));
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, NoteDate, SubmittedAtUtc);
        await factory.Services.AddPendingEditAsync(seeded.A, noteId, seeded.A.ManagerId, EditedAtUtc);
        using var manager = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        using (var saved = await SaveChangesAsync(manager, participant.Id, 1, flagReason: null))
        {
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        }

        var inToReview = Assert.Single(RowsOf(await ToReviewAsync(app, seeded.A.ManagerId), participant.Id));
        Assert.Equal(Reason, inToReview.GetProperty("flagReason").GetString());
        Assert.True(inToReview.GetProperty("flagRemovedLater").GetBoolean());
        clock.Advance(TimeSpan.FromMinutes(1));

        using (var reviewed = await ReviewAsync(manager, participant.Id, 2))
        {
            Assert.Equal(HttpStatusCode.Created, reviewed.StatusCode);
        }

        Assert.Equal([Reason], await ReviewedReasonsAsync(manager, participant.Id));
    }

    // S03.03.02 criterion 2: at most 30 a page, and the cursor, the last item's time and ID, gives the next with
    // nothing skipped or repeated. 61 reviews, a second apart, come as 30, 30 and 1, the last without more.
    [Fact]
    public async Task Sixty_one_reviews_come_in_pages_of_30_30_and_1_each_review_once_newest_first()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation);
        var noteId = await AddReviewedNoteAsync(organisation, participant.Id);
        var expected = new List<Guid>();
        for (var i = 0; i < 61; i++)
        {
            expected.Add(await factory.Services.AddReviewAsync(
                organisation, noteId, 1, organisation.ManagerId, ReviewedAtUtc.AddSeconds(-i)));
        }

        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);
        var pages = await ReviewedPagesAsync(client);

        Assert.Equal(
            [(30, true), (30, true), (1, false)],
            pages.Select(PageShape));
        Assert.Equal(expected, ItemsOf(pages).Select(item => item.GetProperty("reviewId").GetGuid()));
    }

    // The cursor is exclusive and ordered as the list is, so a run of reviews of one millisecond that runs over a page
    // break is neither skipped nor repeated: 33 reviews, the 29th to 32nd in the same millisecond, come as 30, the page
    // ending in the run, then 3. SQL Server orders the run by ID in its own order, which differs from .NET's, so the
    // test does not assume one: it needs every review once and the times never rising.
    [Fact]
    public async Task Reviews_of_one_millisecond_over_a_page_break_are_neither_skipped_nor_repeated()
    {
        var organisation = await factory.Services.AddOrganisationAsync();
        var participant = await factory.Services.AddParticipantAsync(organisation);
        var noteId = await AddReviewedNoteAsync(organisation, participant.Id);
        var times = Enumerable.Range(1, 28).Select(i => ReviewedAtUtc.AddSeconds(i))
            .Concat(Enumerable.Repeat(ReviewedAtUtc, 4))
            .Append(ReviewedAtUtc.AddSeconds(-1))
            .ToList();
        var reviews = new List<Guid>();
        foreach (var time in times)
        {
            reviews.Add(await factory.Services.AddReviewAsync(
                organisation, noteId, 1, organisation.ManagerId, time));
        }

        using var client = factory.CreateHttpsClient().SignInAs(organisation.ManagerId);
        var pages = await ReviewedPagesAsync(client);

        Assert.Equal(
            [(30, true), (3, false)],
            pages.Select(PageShape));
        var items = ItemsOf(pages);
        Assert.Equal(
            ReviewedAtUtc,
            pages[0].GetProperty("items").EnumerateArray().Last().GetProperty("reviewedAtUtc").GetDateTime());
        List<Guid> listed = [.. items.Select(item => item.GetProperty("reviewId").GetGuid())];
        Assert.Equal([.. reviews.Order()], [.. listed.Order()]);
        var reviewedAt = items.Select(item => item.GetProperty("reviewedAtUtc").GetDateTime()).ToList();
        Assert.Equal(reviewedAt.OrderDescending(), reviewedAt);
    }

    // Every page of the Reviewed list as the manager's client gets them, the next by the last item's time and ID, as
    // the screen asks, until one says there is no more.
    private static async Task<List<JsonElement>> ReviewedPagesAsync(HttpClient client)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var pages = new List<JsonElement>();
        string? cursor = "";
        while (cursor is not null)
        {
            using var response = await client.GetAsync(ReviewedPath + cursor, cancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            pages.Add(page);
            cursor = page.GetProperty("hasMore").GetBoolean()
                ? CursorAfter(page.GetProperty("items").EnumerateArray().Last())
                : null;
        }

        return pages;
    }

    private static string CursorAfter(JsonElement item) =>
        $"&before={Uri.EscapeDataString(item.GetProperty("reviewedAtUtc").GetString()!)}"
        + $"&beforeId={item.GetProperty("reviewId").GetGuid()}";

    private static List<JsonElement> ItemsOf(IEnumerable<JsonElement> pages) =>
        [.. pages.SelectMany(page => page.GetProperty("items").EnumerateArray())];

    private static (int Items, bool HasMore) PageShape(JsonElement page) =>
        (page.GetProperty("items").GetArrayLength(), page.GetProperty("hasMore").GetBoolean());

    // The reasons of the participant's reviews, newest first, of every page.
    private static async Task<List<string?>> ReviewedReasonsAsync(HttpClient client, Guid participantId) =>
        [.. ItemsOf(await ReviewedPagesAsync(client))
            .Where(item => item.GetProperty("participantId").GetGuid() == participantId)
            .Select(item => item.GetProperty("flagReason").GetString())];

    // Over HTTPS with the caller's antiforgery token, as the screen sends it (tests/README.md).
    private static async Task<HttpResponseMessage> ReviewAsync(
        HttpClient client, Guid participantId, int versionNumber) =>
        await client.PostWithTokenAsync(
            NotePath(participantId, NoteDate, "reviews"), await client.FetchTokenAsync(), new { versionNumber });

    // Save changes at the base given, flagged with the reason, or not flagged when that is null, as the SPA sends it:
    // over HTTPS, with the caller's antiforgery token and a key of its own.
    private static async Task<HttpResponseMessage> SaveChangesAsync(
        HttpClient client, Guid participantId, int baseVersion, string? flagReason)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, NotePath(participantId, NoteDate, "versions"))
        {
            Content = JsonContent.Create(new
            {
                baseVersion,
                narrative = "Jane made toast.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = flagReason is not null,
                flagReason,
            }),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string NotePath(Guid participantId, DateOnly noteDate, string action) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"/api/participants/{participantId}/notes/{noteDate:yyyy-MM-dd}/{action}");

    // Submit of the participant's note for NoteDate on base 0, with its text and the flag ticked with the reason given,
    // as the screen sends it (tests/README.md): over HTTPS, with the caller's antiforgery token and a key of its own.
    private static async Task<HttpResponseMessage> SubmitFlaggedAsync(
        HttpClient client, Guid participantId, string? flagReason)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, NotePath(participantId, NoteDate, "versions"))
        {
            Content = JsonContent.Create(new
            {
                baseVersion = 0,
                narrative = "Jane made toast.",
                tickedGoalIds = Array.Empty<Guid>(),
                pickedGroupIds = Array.Empty<Guid>(),
                tickedCommonItemIds = Array.Empty<Guid>(),
                isFlagged = true,
                flagReason,
            }),
        };
        request.Headers.Add(AntiforgeryTokens.Header, await client.FetchTokenAsync());
        request.Headers.Add(NoteVersionEndpoints.IdempotencyKeyHeader, Guid.NewGuid().ToString());
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    // The first save of the participant's note for today in Melbourne, by the app's own clock, with the flag ticked and
    // a reason, as the form sends it: with the lists' version of the template that the form opens with (§5.7).
    private async Task<HttpResponseMessage> SaveFlaggedDraftAsync(HttpClient client, Guid participantId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var draftPath = NotePath(
            participantId, factory.Services.GetRequiredService<MelbourneClock>().Today(), "draft");
        var form = await client.GetFromJsonAsync<JsonElement>(draftPath, cancellationToken);
        return await client.PutWithTokenAsync(
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
                isFlagged = true,
                flagReason = Reason,
            },
            ifMatch: null);
    }

    // The rows of the participants given, in the list's order.
    private static List<JsonElement> RowsOf(JsonElement list, params Guid[] participantIds) =>
        [.. list.EnumerateArray().Where(row => participantIds.Contains(row.GetProperty("participantId").GetGuid()))];

    private static List<Guid> ParticipantIds(IEnumerable<JsonElement> rows) =>
        [.. rows.Select(row => row.GetProperty("participantId").GetGuid())];

    // A row's reason and flag time, the number of its current version, and whether that version removed the flag.
    private static (string? FlagReason, DateTime FlaggedAtUtc, int CurrentVersionNumber, bool FlagRemovedLater) FlagOf(
        JsonElement row) =>
        (
            row.GetProperty("flagReason").GetString(),
            row.GetProperty("flaggedAtUtc").GetDateTime(),
            row.GetProperty("currentVersionNumber").GetInt32(),
            row.GetProperty("flagRemovedLater").GetBoolean());

    // The To review list as the user of the ID given gets it, over HTTPS, as the SPA calls the /api group
    // (tests/README.md).
    private static async Task<JsonElement> ToReviewAsync(WebApplicationFactory<Program> app, Guid userId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = app.CreateHttpsClient().SignInAs(userId);

        using var response = await client.GetAsync(ToReviewPath, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    // The participant's note for the day, started an hour before the time given and submitted flagged with Reason then,
    // by A's worker or the author given, as Submit leaves it: in To review, with version 1 its flagged version.
    private Task<Guid> AddFlaggedAsync(
        Guid participantId, DateOnly noteDate, DateTime flaggedAtUtc, Guid? authorUserId = null) =>
        factory.Services.AddSubmittedAsync(
            seeded.A,
            new(
                participantId,
                noteDate,
                authorUserId ?? seeded.A.WorkerId,
                flaggedAtUtc.AddHours(-1),
                flaggedAtUtc,
                IsFlagged: true,
                FlagReason: Reason));

    // A note of the participant's that was flagged with the reason given and reviewed: submitted, in Reviewed, with
    // version 1 its flagged version, as a review leaves it; and gives its ID. The review itself is added apart.
    private Task<Guid> AddReviewedNoteAsync(
        SeededOrganisation organisation, Guid participantId, Guid? authorUserId = null, string reason = Reason) =>
        factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participantId,
                NoteDate,
                authorUserId ?? organisation.WorkerId,
                StartedAtUtc,
                SubmittedAtUtc,
                IsFlagged: true,
                FlagReason: reason,
                FlagStatus: FlagStatus.Reviewed));

    // A reviewed note, as AddReviewedNoteAsync adds one, and its one review of version 1, by the organisation's manager
    // or the user given, at the time given; and gives the review's ID.
    private async Task<Guid> AddReviewedAsync(
        SeededOrganisation organisation,
        Guid participantId,
        DateTime reviewedAtUtc,
        string? comment = null,
        Guid? authorUserId = null,
        Guid? reviewedByUserId = null,
        string reason = Reason) =>
        await factory.Services.AddReviewAsync(
            organisation,
            await AddReviewedNoteAsync(organisation, participantId, authorUserId, reason),
            1,
            reviewedByUserId ?? organisation.ManagerId,
            reviewedAtUtc,
            comment);

    // The note's flag status set to To review by hand, through the app's context under A's tenant.
    private async Task SetToReviewByHandAsync(Guid noteId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            var note = await db.Notes.SingleAsync(n => n.Id == noteId, cancellationToken);
            note.FlagStatus = FlagStatus.ToReview;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // The user deactivated as deactivation leaves them (§8.6), through the app's UserManager, a user the test added, as
    // no test deactivates a seeded user (tests/README.md).
    private async Task DeactivateAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        Assert.NotNull(user);
        user.Status = UserStatus.Deactivated;
        user.DeactivatedAtUtc = EditedAtUtc;
        var updated = await users.UpdateAsync(user);
        Assert.True(updated.Succeeded, string.Join(" ", updated.Errors.Select(error => error.Code)));
    }

    // The ID of the last event written, by any test: the tests of the collection run one at a time, so none writes one
    // while a test reads it before and after its requests.
    private async Task<long?> LastEventIdAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long?>($"SELECT MAX([Id]) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
