using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Grow2Notes.Web.Platform.Audit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Grow2Notes.Tests.Features.Reviews;

/// <summary>
/// <c>POST {base}/reviews</c>, Mark reviewed (design.md §3.6, §5.8 <i>Review</i>, §6.4), called with the test-only
/// sign-in (<see cref="TestSignIn"/>): for a manager, a note in To review at the version they send moved to Reviewed,
/// one <c>NoteReview</c> row with the manager, the version, the time and the trimmed comment, and <c>note.reviewed</c>,
/// together or not at all, and <c>201</c> with the stored time; and, changing nothing, <c>409 review.not_current</c>
/// for a stale version, a note already reviewed, a draft and a note never flagged, <c>422 validation.failed</c> for a
/// comment over 500 characters or a version below 1, <c>404</c> for a participant not in the caller's organisation
/// (§9.2) or a day with no live note, and a <c>403</c> for a worker. A manager may review a note they wrote (A73), and
/// a note of an archived participant. The tests add their own participants, users and notes (tests/README.md). The
/// endpoint matrix covers who may call it, and <see cref="SignedOutTests"/> what a signed-out caller gets.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteReviewEndpointTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private const string Reason = "Jane seemed unwell.";

    // Made up (A35): a note for Friday 2 October 2026, started at 9:14 am and submitted at 4:42 pm in Melbourne, and
    // edited at 10:05 am the next day.
    private static readonly DateOnly NoteDate = new(2026, 10, 2);

    private static readonly DateTime StartedAtUtc = new(2026, 10, 1, 23, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 2, 6, 42, 0, DateTimeKind.Utc);

    private static readonly DateTime EditedAtUtc = new(2026, 10, 3, 0, 5, 0, DateTimeKind.Utc);

    // 9:42 am on the Monday in Melbourne, with a part of a millisecond, which a datetime2(3) column cannot hold.
    private static readonly DateTimeOffset ReviewedAt =
        new DateTimeOffset(2026, 10, 4, 23, 42, 0, 123, TimeSpan.Zero).AddTicks(4567);

    private static readonly DateTime ReviewedAtUtc = new(2026, 10, 4, 23, 42, 0, 123, DateTimeKind.Utc);

    private readonly SeededOrganisations seeded = sqlServer.Seeded;

    // A note in To review at version 2, whose reason version 2 changed. The comment's outer spaces go and its line
    // break stays; the review holds the version, the manager and the time as stored, and the flagged version stays as
    // it was. The event holds the version number alone, no comment (A28).
    [Fact]
    public async Task A_managers_review_of_a_note_in_To_review_marks_it_reviewed_with_one_review_and_one_event()
    {
        await using var app = AppAtTheReview();
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAtVersion2Async(participant.Id);
        var before = await StoredEventsAsync();
        using var client = app.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 2, "  Called Sam's mum.\nGP booked.  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var answer = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(["reviewedAtUtc"], answer.EnumerateObject().Select(member => member.Name));
        Assert.Equal(ReviewedAtUtc, answer.GetProperty("reviewedAtUtc").GetDateTimeOffset().UtcDateTime);
        Assert.Equal((FlagStatus.Reviewed, 2, 2), await StoredFlagAsync(noteId));
        var review = Assert.Single(await StoredReviewsAsync(noteId));
        Assert.Equal(
            new ReviewRow(
                seeded.A.OrganisationId, noteId, 2, seeded.A.ManagerId, ReviewedAtUtc, "Called Sam's mum.\nGP booked."),
            review);
        var written = Assert.Single(await EventsAfterAsync(before));
        Assert.Equal(
            new WrittenEvent(
                AuditEventTypes.Note.Reviewed, seeded.A.OrganisationId, seeded.A.ManagerId, AuditEntityTypes.Note,
                noteId, participant.Id, """{"versionNumber":2}"""),
            written);
        Assert.DoesNotContain("Sam", written.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n ")]
    [InlineData(null)]
    public async Task A_blank_or_omitted_comment_is_stored_as_null(string? comment)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, comment);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(Assert.Single(await StoredReviewsAsync(noteId)).Comment);
    }

    [Fact]
    public async Task A_comment_left_out_of_the_body_is_stored_as_null()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await client.PostWithTokenAsync(
            ReviewsPath(participant.Id), await client.FetchTokenAsync(), new { versionNumber = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(Assert.Single(await StoredReviewsAsync(noteId)).Comment);
    }

    // A73: any manager may review any flagged note, one they wrote or edited included.
    [Fact]
    public async Task A_manager_may_review_a_note_they_wrote()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.ManagerId);
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            (seeded.A.ManagerId, FlagStatus.Reviewed),
            (Assert.Single(await StoredReviewsAsync(noteId)).ReviewedByUserId, (await StoredFlagAsync(noteId)).Status));
    }

    // The route's policy is the role check, so a worker is refused before the endpoint runs.
    [Fact]
    public async Task A_workers_review_is_refused_with_a_403_and_changes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.WorkerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        await AssertNothingChangedAsync(noteId, before, FlagStatus.ToReview);
    }

    [Fact]
    public async Task Another_organisations_manager_gets_a_404_and_changes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfB, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertNothingChangedAsync(noteId, before, FlagStatus.ToReview);
    }

    // The limit applies to the comment as sent, before it is trimmed: 501 characters are refused, 500 are kept.
    [Fact]
    public async Task A_comment_of_501_characters_is_a_422_under_comment_and_one_of_500_is_reviewed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using (var tooLong = await ReviewAsync(client, participant.Id, 1, new string('x', Limits.ReviewComment + 1)))
        {
            await ReadValidationProblemAsync(tooLong, "comment");
        }

        await AssertNothingChangedAsync(noteId, before, FlagStatus.ToReview);
        using var atTheLimit = await ReviewAsync(client, participant.Id, 1, new string('x', Limits.ReviewComment));
        Assert.Equal(HttpStatusCode.Created, atTheLimit.StatusCode);
        Assert.Equal(Limits.ReviewComment, Assert.Single(await StoredReviewsAsync(noteId)).Comment?.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_version_below_1_is_a_422_under_versionNumber_and_changes_nothing(int versionNumber)
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, versionNumber, "Fine.");

        await ReadValidationProblemAsync(response, "versionNumber");
        await AssertNothingChangedAsync(noteId, before, FlagStatus.ToReview);
    }

    // A manager who opened the note at version 1 while an edit made version 2.
    [Fact]
    public async Task A_stale_version_is_a_409_review_not_current_and_changes_nothing()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAtVersion2Async(participant.Id);
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        await ReadNotCurrentAsync(response);
        await AssertNothingChangedAsync(noteId, before, FlagStatus.ToReview, flaggedVersion: 2, currentVersion: 2);
    }

    // The same press sent twice, as a retry after a lost answer is: the first reviews, and the second finds the note
    // reviewed. One review and one event, whatever the number of tries.
    [Fact]
    public async Task The_same_request_twice_is_a_201_then_a_409_with_one_review_and_one_event()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var first = await ReviewAsync(client, participant.Id, 1, "Fine.");
        using var second = await ReviewAsync(client, participant.Id, 1, "Fine.");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await ReadNotCurrentAsync(second);
        Assert.Single(await StoredReviewsAsync(noteId));
        Assert.Single(await EventsAfterAsync(before));
    }

    [Fact]
    public async Task A_draft_gets_a_409_review_not_current()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddDraftAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, Narrative: "Jane"));
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        await ReadNotCurrentAsync(response);
        await AssertNothingChangedAsync(noteId, before, FlagStatus.None, flaggedVersion: null, currentVersion: 0);
    }

    [Fact]
    public async Task A_note_never_flagged_gets_a_409_review_not_current()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(
            seeded.A, new(participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc));
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        await ReadNotCurrentAsync(response);
        await AssertNothingChangedAsync(noteId, before, FlagStatus.None, flaggedVersion: null);
    }

    [Fact]
    public async Task A_note_already_reviewed_gets_a_409_review_not_current()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await factory.Services.AddSubmittedAsync(
            seeded.A,
            new(
                participant.Id, NoteDate, seeded.A.WorkerId, StartedAtUtc, SubmittedAtUtc, IsFlagged: true,
                FlagReason: Reason, FlagStatus: FlagStatus.Reviewed));
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        await ReadNotCurrentAsync(response);
        await AssertNothingChangedAsync(noteId, before, FlagStatus.Reviewed);
    }

    [Fact]
    public async Task A_day_with_no_live_note_and_an_unknown_participant_each_get_a_404()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var before = await StoredEventsAsync();
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var noNote = await ReviewAsync(client, participant.Id, 1, "Fine.");
        using var unknown = await ReviewAsync(client, Guid.NewGuid(), 1, "Fine.");

        Assert.Equal(HttpStatusCode.NotFound, noNote.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Empty(await EventsAfterAsync(before));
    }

    // There is no archive check, as Save changes has none: a note leaves To review only through a review (§3.7).
    [Fact]
    public async Task A_note_of_an_archived_participant_is_reviewed()
    {
        var participant = await factory.Services.AddParticipantAsync(seeded.A, archived: true);
        var noteId = await AddFlaggedAsync(participant.Id, seeded.A.WorkerId);
        using var client = AppAtTheReview().CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);

        using var response = await ReviewAsync(client, participant.Id, 1, "Fine.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(FlagStatus.Reviewed, (await StoredFlagAsync(noteId)).Status);
    }

    // The note was reviewed at version 1, edited and flagged again as version 2, and reviewed again: two reviews of one
    // note, set up by hand, as the read view shows them, newest first, with the note Reviewed.
    [Fact]
    public async Task A_note_reviewed_twice_gives_a_manager_flagStatus_Reviewed_and_both_reviews_newest_first()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var participant = await factory.Services.AddParticipantAsync(seeded.A);
        var noteId = await AddFlaggedAtVersion2Async(participant.Id);
        var secondManager = await factory.Services.AddUserAsync(seeded.A, UserRole.Manager, "Alex Morgan");
        var firstAt = new DateTime(2026, 10, 2, 7, 0, 0, DateTimeKind.Utc);
        var secondAt = new DateTime(2026, 10, 3, 1, 0, 0, DateTimeKind.Utc);
        await AddReviewsAsync(
            new NoteReview
            {
                OrganisationId = seeded.A.OrganisationId,
                NoteId = noteId,
                VersionNumber = 2,
                ReviewedByUserId = secondManager,
                ReviewedAtUtc = secondAt,
                Comment = "Followed up.",
            },
            new NoteReview
            {
                OrganisationId = seeded.A.OrganisationId,
                NoteId = noteId,
                VersionNumber = 1,
                ReviewedByUserId = seeded.A.ManagerId,
                ReviewedAtUtc = firstAt,
            });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database.ExecuteSqlAsync(
                $"UPDATE [Note] SET [FlagStatus] = {(byte)FlagStatus.Reviewed} WHERE [Id] = {noteId}",
                cancellationToken);
        }

        using var client = factory.CreateHttpsClient().SignInAs(Caller.ManagerOfA, seeded);
        var note = await client.GetFromJsonAsync<JsonElement>(NotePath(participant.Id), cancellationToken);

        Assert.Equal("Reviewed", note.GetProperty("flagStatus").GetString());
        var reviews = note.GetProperty("reviews").EnumerateArray().ToList();
        Assert.Equal(2, reviews.Count);
        Assert.Equal(
            (2, secondManager, "Alex Morgan", secondAt, "Followed up."),
            (
                reviews[0].GetProperty("versionNumber").GetInt32(),
                reviews[0].GetProperty("reviewedBy").GetProperty("id").GetGuid(),
                reviews[0].GetProperty("reviewedBy").GetProperty("displayName").GetString(),
                reviews[0].GetProperty("reviewedAtUtc").GetDateTimeOffset().UtcDateTime,
                reviews[0].GetProperty("comment").GetString()));
        Assert.Equal(
            (1, seeded.A.ManagerId, firstAt, JsonValueKind.Null),
            (
                reviews[1].GetProperty("versionNumber").GetInt32(),
                reviews[1].GetProperty("reviewedBy").GetProperty("id").GetGuid(),
                reviews[1].GetProperty("reviewedAtUtc").GetDateTimeOffset().UtcDateTime,
                reviews[1].GetProperty("comment").ValueKind));
    }

    private static string NotePath(Guid participantId) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/participants/{participantId}/notes/{NoteDate:yyyy-MM-dd}");

    private static string ReviewsPath(Guid participantId) => $"{NotePath(participantId)}/reviews";

    // Over HTTPS with the caller's antiforgery token, as the screen sends it (tests/README.md).
    private static async Task<HttpResponseMessage> ReviewAsync(
        HttpClient client, Guid participantId, int versionNumber, string? comment) =>
        await client.PostWithTokenAsync(
            ReviewsPath(participantId), await client.FetchTokenAsync(), new { versionNumber, comment });

    // A 409 review.not_current (design.md §6.9).
    private static async Task ReadNotCurrentAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.Conflict));
        Assert.Equal("review.not_current", problem.RootElement.GetProperty(ErrorCode.Extension).GetString());
    }

    // A 422 validation.failed whose errors name the field alone (design.md §6.1).
    private static async Task ReadValidationProblemAsync(HttpResponseMessage response, string field)
    {
        using var problem = JsonDocument.Parse(await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity));
        Assert.Equal("validation.failed", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal([field], problem.RootElement.GetProperty("errors").EnumerateObject().Select(error => error.Name));
    }

    // The app on a clock at ReviewedAt, which the review stores as its time.
    private WebApplicationFactory<Program> AppAtTheReview() =>
        factory.WithWebHostBuilder(builder => builder.UseClock(new FakeTimeProvider(ReviewedAt)));

    // A submitted note in To review at version 1, flagged by version 1.
    private Task<Guid> AddFlaggedAsync(Guid participantId, Guid authorId) =>
        factory.Services.AddSubmittedAsync(
            seeded.A,
            new(participantId, NoteDate, authorId, StartedAtUtc, SubmittedAtUtc, IsFlagged: true, FlagReason: Reason));

    // A submitted note in To review at version 2, which flagged it again with a different reason.
    private async Task<Guid> AddFlaggedAtVersion2Async(Guid participantId)
    {
        var noteId = await AddFlaggedAsync(participantId, seeded.A.WorkerId);
        await factory.Services.AddVersion2Async(
            seeded.A, noteId, EditedAtUtc, "Jane seemed worse.", flaggedVersionNumber: 2);
        return noteId;
    }

    private async Task AssertNothingChangedAsync(
        Guid noteId, long eventsBefore, FlagStatus status, int? flaggedVersion = 1, int currentVersion = 1)
    {
        Assert.Equal((status, flaggedVersion, currentVersion), await StoredFlagAsync(noteId));
        Assert.Empty(await StoredReviewsAsync(noteId));
        Assert.Empty(await EventsAfterAsync(eventsBefore));
    }

    private async Task AddReviewsAsync(params NoteReview[] reviews)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.NoteReviews.AddRange(reviews);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(seeded.A.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // Each read below is in SQL from a scope of its own, so that neither the query filter, which would need a tenant,
    // nor a context's tracked rows decide what is seen.
    private async Task<(FlagStatus Status, int? FlaggedVersion, int CurrentVersion)> StoredFlagAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<FlagRow>($"""
                SELECT [FlagStatus] AS [Status], [FlaggedVersionNumber], [CurrentVersionNumber]
                FROM [Note]
                WHERE [Id] = {noteId}
                """)
            .SingleAsync(TestContext.Current.CancellationToken);
        return ((FlagStatus)row.Status, row.FlaggedVersionNumber, row.CurrentVersionNumber);
    }

    private async Task<List<ReviewRow>> StoredReviewsAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<ReviewRow>($"""
                SELECT [OrganisationId], [NoteId], [VersionNumber], [ReviewedByUserId], [ReviewedAtUtc], [Comment]
                FROM [NoteReview]
                WHERE [NoteId] = {noteId}
                ORDER BY [ReviewedAtUtc]
                """)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    // The last event's ID, 0 when there is none; the tests of the collection run one at a time, so the events after it
    // are the test's alone.
    private async Task<long> StoredEventsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>().Database
            .SqlQuery<long>($"SELECT ISNULL(MAX([Id]), 0) AS [Value] FROM [AuditEvent]")
            .SingleAsync(TestContext.Current.CancellationToken);
    }

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

    /// <summary>A <c>Note</c> row's flag status, as its stored <c>tinyint</c>, and its version numbers.</summary>
    private sealed record FlagRow(byte Status, int? FlaggedVersionNumber, int CurrentVersionNumber);

    /// <summary>A <c>NoteReview</c> row.</summary>
    private sealed record ReviewRow(
        Guid OrganisationId,
        Guid NoteId,
        int VersionNumber,
        Guid ReviewedByUserId,
        DateTime ReviewedAtUtc,
        string? Comment);

    /// <summary>An audit entry's fields, its <c>Details</c> as the JSON stored.</summary>
    private sealed record WrittenEvent(
        string EventType,
        Guid OrganisationId,
        Guid? ActorUserId,
        string? EntityType,
        Guid? EntityId,
        Guid? ParticipantId,
        string? Details);
}
