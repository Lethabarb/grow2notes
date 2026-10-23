using Grow2Notes.Tests.Fixtures;
using Grow2Notes.Web.Data;
using Grow2Notes.Web.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Grow2Notes.Tests.Data;

/// <summary>
/// The <c>NoteReview</c> table on SQL Server (design.md §5.3): a review saved as written, the composite foreign keys
/// that refuse a review of another organisation's note or by another organisation's user, and the app's refusal to
/// change or delete a saved review. The tests save through the app's context, connected as <c>sa</c>, to which no deny
/// applies, so only the database's own constraints and the app's check stand between each save and the rows;
/// <see cref="RuntimeRoleTests"/> shows the database's denies. Each test adds a participant of its own, so that no list
/// that another test reads changes (tests/README.md). <see cref="NoteReviewModelTests"/> checks the model.
/// </summary>
[Collection<SqlServerCollection>]
public sealed class NoteReviewTableTests(Grow2NotesFactory factory, SqlServerFixture sqlServer)
    : IClassFixture<Grow2NotesFactory>
{
    private static readonly DateOnly NoteDate = new(2026, 10, 4);

    // 9:14 am in Melbourne on the note's date, 4:42 pm, and 9:42 am the day after.
    private static readonly DateTime StartedAtUtc = new(2026, 10, 3, 22, 14, 0, DateTimeKind.Utc);

    private static readonly DateTime SubmittedAtUtc = new(2026, 10, 4, 5, 42, 0, DateTimeKind.Utc);

    private static readonly DateTime ReviewedAtUtc = new(2026, 10, 4, 22, 42, 0, 123, DateTimeKind.Utc);

    private SeededOrganisation A => sqlServer.Seeded.A;

    private SeededOrganisation B => sqlServer.Seeded.B;

    [Fact]
    public async Task A_review_with_a_comment_of_the_full_length_is_saved_as_written()
    {
        var comment = new string('x', Limits.ReviewComment);
        var noteId = await AddFlaggedNoteAsync(A);
        var review = NewReview(A, noteId, A.ManagerId);
        review.Comment = comment;

        await AddAsync(A, review);

        Assert.Equal(
            [new ReviewRow(review.Id, A.OrganisationId, noteId, 1, A.ManagerId, ReviewedAtUtc, comment)],
            await StoredReviewsAsync(noteId));
    }

    [Fact]
    public async Task A_review_with_no_comment_is_saved_with_a_null_comment()
    {
        var noteId = await AddFlaggedNoteAsync(A);
        var review = NewReview(A, noteId, A.ManagerId);

        await AddAsync(A, review);

        Assert.Null(Assert.Single(await StoredReviewsAsync(noteId)).Comment);
    }

    // The foreign key is composite, so a note of another organisation does not match the review's own.
    [Fact]
    public async Task A_review_of_another_organisations_note_is_refused_with_error_547()
    {
        var noteOfB = await AddFlaggedNoteAsync(B);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddAsync(A, NewReview(A, noteOfB, A.ManagerId)));

        // 547: a constraint refused the statement.
        var refusal = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(547, refusal.Number);
        Assert.Contains("\"FK_NoteReview_Note_OrganisationId_NoteId\"", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(await StoredReviewsAsync(noteOfB));
    }

    [Fact]
    public async Task A_review_by_another_organisations_user_is_refused_with_error_547()
    {
        var noteOfA = await AddFlaggedNoteAsync(A);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddAsync(A, NewReview(A, noteOfA, B.ManagerId)));

        var refusal = Assert.IsType<SqlException>(error.InnerException);
        Assert.Equal(547, refusal.Number);
        Assert.Contains(
            "\"FK_NoteReview_AspNetUsers_OrganisationId_ReviewedByUserId\"",
            refusal.Message,
            StringComparison.Ordinal);
        Assert.Empty(await StoredReviewsAsync(noteOfA));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("deleted")]
    public async Task A_saved_review_that_is_changed_or_deleted_is_refused_and_the_table_is_left_as_it_was(string verb)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var noteId = await AddFlaggedNoteAsync(A);
        await AddAsync(A, NewReview(A, noteId, A.ManagerId));
        var before = await StoredReviewsAsync(noteId);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();

        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(A.OrganisationId))
        {
            var saved = await db.NoteReviews.SingleAsync(r => r.NoteId == noteId, cancellationToken);
            if (verb == "deleted")
            {
                db.Remove(saved);
            }
            else
            {
                saved.Comment = "Changed";
            }

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.SaveChangesAsync(cancellationToken));

            Assert.StartsWith(
                $"Nothing was saved: {nameof(NoteReview)} row to be {verb} is append-only.",
                ex.Message,
                StringComparison.Ordinal);
        }

        Assert.Equal(before, await StoredReviewsAsync(noteId));
    }

    // A submitted note in To review at version 1, of a participant of the organisation's own: the row a review is of.
    private async Task<Guid> AddFlaggedNoteAsync(SeededOrganisation organisation)
    {
        var participant = await factory.Services.AddParticipantAsync(organisation);
        return await factory.Services.AddSubmittedAsync(
            organisation,
            new(
                participant.Id, NoteDate, organisation.WorkerId, StartedAtUtc, SubmittedAtUtc, IsFlagged: true,
                FlagReason: "Jane seemed unwell."));
    }

    private static NoteReview NewReview(SeededOrganisation organisation, Guid noteId, Guid reviewerId) =>
        new()
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisation.OrganisationId,
            NoteId = noteId,
            VersionNumber = 1,
            ReviewedByUserId = reviewerId,
            ReviewedAtUtc = ReviewedAtUtc,
        };

    // Through the app's context under the organisation's tenant, as the app adds rows, from a scope of its own, so that
    // a refused save leaves no tracked row behind for the next.
    private async Task AddAsync(SeededOrganisation organisation, NoteReview review)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        db.NoteReviews.Add(review);
        using (scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(organisation.OrganisationId))
        {
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    // Read in SQL from a scope of its own, so that neither the query filter nor a context's tracked rows decide what is
    // seen.
    private async Task<List<ReviewRow>> StoredReviewsAsync(Guid noteId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Grow2NotesDbContext>();
        return await db.Database.SqlQuery<ReviewRow>($"""
            SELECT [Id], [OrganisationId], [NoteId], [VersionNumber], [ReviewedByUserId], [ReviewedAtUtc], [Comment]
            FROM [NoteReview]
            WHERE [NoteId] = {noteId}
            ORDER BY [ReviewedAtUtc]
            """).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A <c>NoteReview</c> row.</summary>
    private sealed record ReviewRow(
        Guid Id,
        Guid OrganisationId,
        Guid NoteId,
        int VersionNumber,
        Guid ReviewedByUserId,
        DateTime ReviewedAtUtc,
        string? Comment);
}
